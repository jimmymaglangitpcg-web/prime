import { useState } from 'react';
import { Alert, Button, Empty, Form, Input, Modal, Segmented, Select, Space, Table, Tag, Typography, message } from 'antd';
import {
  useApproveSignUp, useRejectSignUp, useSignUpOptions, useSignUpRequests, type SignUpRequestDto,
} from '../../api/accounts';
import { useCurrentUser } from '../../api/offices';
import { ApiRequestError } from '../../lib/apiClient';
import type { WorkflowStatus } from '../../lib/types';

const PROVINCE_WIDE = '__province-wide__';
const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const statusTag = (s: WorkflowStatus) =>
  <Tag color={s === 'Approved' ? 'green' : s === 'Rejected' ? 'red' : 'gold'}>{s === 'PendingReview' ? 'Awaiting decision' : s}</Tag>;

/**
 * Sign-up requests (docs/analysis/workflow-security.md §4.2, Q5): a system administrator approves each new account,
 * giving its office and roles in the same step (in force at once), or rejects it with a reason. An administrator cannot
 * decide their own request. Everything is kept.
 */
export function SignUpRequestsPage() {
  const [filter, setFilter] = useState<WorkflowStatus | 'all'>('PendingReview');
  const requests = useSignUpRequests(filter === 'all' ? undefined : filter);
  const options = useSignUpOptions();
  const me = useCurrentUser();
  const approve = useApproveSignUp();
  const reject = useRejectSignUp();
  const [toast, context] = message.useMessage();
  const [approving, setApproving] = useState<SignUpRequestDto | null>(null);
  const [rejecting, setRejecting] = useState<SignUpRequestDto | null>(null);
  const [approveForm] = Form.useForm();
  const [rejectForm] = Form.useForm();
  const officeValue = Form.useWatch('officeId', approveForm);

  const provinceWide = officeValue === PROVINCE_WIDE;
  const roleOptions = (options.data?.roles ?? [])
    .filter((r) => !provinceWide || options.data?.provinceWideRoles.includes(r.code))
    .map((r) => ({ value: r.code, label: `${r.name} (${r.code})` }));

  const doApprove = async () => {
    if (!approving) return;
    const v = await approveForm.validateFields();
    approve.mutate({ id: approving.id, officeId: v.officeId === PROVINCE_WIDE ? null : v.officeId, roles: v.roles, remarks: v.remarks || null }, {
      onSuccess: () => { toast.success(`${approving.fullName} can now use PRIME.`); setApproving(null); },
      onError: (e) => toast.error(errorText(e)),
    });
  };
  const doReject = async () => {
    if (!rejecting) return;
    const v = await rejectForm.validateFields();
    reject.mutate({ id: rejecting.id, reason: v.reason }, {
      onSuccess: () => { toast.success('Request rejected.'); setRejecting(null); },
      onError: (e) => toast.error(errorText(e)),
    });
  };

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      {context}
      <div>
        <Typography.Title level={3} style={{ margin: 0 }}>Sign-up Requests</Typography.Title>
        <Typography.Paragraph type="secondary" style={{ maxWidth: 900, marginBottom: 0 }}>
          People who created an account and asked for access. Approving gives the office and roles you choose, which may differ from those
          asked, and the person can work at once. Later changes to their office or roles go through Offices → Staff.
        </Typography.Paragraph>
      </div>
      <Segmented<WorkflowStatus | 'all'> value={filter} onChange={setFilter} options={[
        { value: 'PendingReview', label: 'Awaiting decision' }, { value: 'Approved', label: 'Approved' },
        { value: 'Rejected', label: 'Rejected' }, { value: 'all', label: 'All' },
      ]} />
      {requests.isError && <Alert type="error" showIcon title="Could not load the requests" description={errorText(requests.error)} />}
      <Table<SignUpRequestDto> rowKey="id" size="small" loading={requests.isLoading} dataSource={requests.data ?? []} pagination={{ pageSize: 20 }}
        scroll={{ x: true }} locale={{ emptyText: <Empty description="No requests" /> }}
        columns={[
          { title: 'Applicant', render: (_, r) => <Space orientation="vertical" size={0}><span>{r.fullName}</span><Typography.Text type="secondary">{r.email}</Typography.Text></Space> },
          { title: 'Position', dataIndex: 'position' },
          { title: 'Office asked', render: (_, r) => r.requestedOfficeName ?? 'Province-wide' },
          { title: 'Roles asked', render: (_, r) => <Space size={4} wrap>{r.requestedRoles.map((x) => <Tag key={x}>{x}</Tag>)}</Space> },
          { title: 'Note', dataIndex: 'note', render: (v: string | null) => v ?? '—' },
          { title: 'Sent', dataIndex: 'createdAt', render: (v: string) => new Date(v).toLocaleString() },
          {
            title: 'Status', render: (_, r) => (
              <Space orientation="vertical" size={0}>
                {statusTag(r.status)}
                {r.decidedByName && <Typography.Text type="secondary">{r.decidedByName}</Typography.Text>}
                {r.decisionReason && <Typography.Text type="secondary">{r.decisionReason}</Typography.Text>}
              </Space>
            ),
          },
          {
            title: '', render: (_, r) => r.status === 'PendingReview' && r.appUserId !== me.data?.userId ? (
              <Space>
                <Button size="small" type="primary" onClick={() => setApproving(r)}>Approve</Button>
                <Button size="small" danger onClick={() => setRejecting(r)}>Reject</Button>
              </Space>
            ) : null,
          },
        ]} />
      <Modal title={approving ? `Approve — ${approving.fullName}` : ''} open={approving !== null} onCancel={() => setApproving(null)} onOk={doApprove}
        okText="Approve and activate" confirmLoading={approve.isPending} destroyOnHidden>
        <Form form={approveForm} layout="vertical" preserve={false} initialValues={approving ? {
          officeId: approving.requestedOfficeId ?? PROVINCE_WIDE, roles: approving.requestedRoles,
        } : undefined}>
          <Form.Item name="officeId" label="Office" rules={[{ required: true, message: 'Choose the office' }]}>
            <Select showSearch optionFilterProp="label" onChange={() => approveForm.setFieldValue('roles', [])} options={[
              ...(options.data?.offices ?? []).map((o) => ({ value: o.id, label: o.name })),
              { value: PROVINCE_WIDE, label: 'Province-wide' },
            ]} />
          </Form.Item>
          <Form.Item name="roles" label="Roles" rules={[{ required: true, message: 'Give at least one role' }]}>
            <Select mode="multiple" options={roleOptions} />
          </Form.Item>
          <Form.Item name="remarks" label="Remarks" rules={[{ max: 1000 }]} extra="Kept with the office assignment. It starts today.">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
      <Modal title={rejecting ? `Reject — ${rejecting.fullName}` : ''} open={rejecting !== null} onCancel={() => setRejecting(null)} onOk={doReject}
        okText="Reject" okButtonProps={{ danger: true }} confirmLoading={reject.isPending} destroyOnHidden>
        <Form form={rejectForm} layout="vertical" preserve={false}>
          <Form.Item name="reason" label="Reason" rules={[{ required: true, whitespace: true, message: 'A reason is required' }, { max: 500 }]}
            extra="The applicant sees it and may ask again.">
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
