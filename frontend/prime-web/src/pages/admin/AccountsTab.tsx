import { useState } from 'react';
import { Button, Form, Input, Modal, Space, Table, Tag, Typography, message } from 'antd';
import {
  useApproveUserStatus, useProposeUserStatus, useRejectUserStatus, useUserStatusChanges, type AppUserStatus, type UserStatusChangeDto,
} from '../../api/accounts';
import { useCan, useCurrentUser, useUsers, type UserSummaryDto } from '../../api/offices';
import { ApiRequestError } from '../../lib/apiClient';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const accountTag = (s: AppUserStatus) => <Tag color={s === 'Active' ? 'green' : s === 'Pending' ? 'gold' : 'red'}>{s === 'Inactive' ? 'Disabled' : s}</Tag>;

/**
 * Accounts (docs/analysis/workflow-security.md §4.2, Q6): who may sign in. Disabling or enabling a user is proposed by
 * one administrator and takes effect the moment a second user approves it; a disabled user is refused on every request.
 * Pending accounts are decided on the Sign-up Requests page.
 */
export function AccountsTab() {
  const users = useUsers();
  const changes = useUserStatusChanges();
  const me = useCurrentUser();
  const can = useCan();
  const propose = useProposeUserStatus();
  const approve = useApproveUserStatus();
  const reject = useRejectUserStatus();
  const [toast, context] = message.useMessage();
  const [proposing, setProposing] = useState<UserSummaryDto | null>(null);
  const [rejecting, setRejecting] = useState<UserStatusChangeDto | null>(null);
  const [form] = Form.useForm();
  const [rejectForm] = Form.useForm();
  const open = new Set((changes.data ?? []).filter((c) => c.status === 'Draft').map((c) => c.appUserId));

  const send = async () => {
    if (!proposing) return;
    const v = await form.validateFields();
    propose.mutate({ userId: proposing.id, newStatus: proposing.status === 'Active' ? 'Inactive' : 'Active', reason: v.reason }, {
      onSuccess: () => { toast.success('Proposed. It takes effect when a second user approves it.'); setProposing(null); },
      onError: (e) => toast.error(errorText(e)),
    });
  };
  const sendReject = async () => {
    if (!rejecting) return;
    const v = await rejectForm.validateFields();
    reject.mutate({ id: rejecting.id, reason: v.reason }, {
      onSuccess: () => { toast.success('Change rejected.'); setRejecting(null); },
      onError: (e) => toast.error(errorText(e)),
    });
  };

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      {context}
      <Table<UserSummaryDto> rowKey="id" size="small" loading={users.isLoading} dataSource={users.data ?? []} pagination={{ pageSize: 20 }} scroll={{ x: true }}
        columns={[
          { title: 'User', render: (_, u) => u.email ? `${u.displayName} (${u.email})` : u.displayName },
          { title: 'Account', dataIndex: 'status', render: accountTag },
          { title: 'Office', render: (_, u) => u.provinceWide ? 'Province-wide' : u.officeCode ?? '—' },
          { title: 'Roles', render: (_, u) => <Space size={4} wrap>{u.roles.map((r) => <Tag key={r}>{r}</Tag>)}</Space> },
          {
            title: '', render: (_, u) => can('users.manage') && u.status !== 'Pending' && u.id !== me.data?.userId && !open.has(u.id) ? (
              <Button size="small" danger={u.status === 'Active'} onClick={() => setProposing(u)}>{u.status === 'Active' ? 'Disable' : 'Enable'}</Button>
            ) : open.has(u.id) ? <Typography.Text type="secondary">Change awaiting approval</Typography.Text> : null,
          },
        ]} />
      <div>
        <Typography.Title level={5}>Disable and enable requests</Typography.Title>
        <Table<UserStatusChangeDto> rowKey="id" size="small" loading={changes.isLoading} dataSource={changes.data ?? []} pagination={{ pageSize: 10 }}
          scroll={{ x: true }}
          columns={[
            { title: 'User', dataIndex: 'userName' },
            { title: 'Change', render: (_, c) => c.newStatus === 'Inactive' ? <Tag color="red">Disable</Tag> : <Tag color="green">Enable</Tag> },
            { title: 'Reason', dataIndex: 'reason' },
            { title: 'Proposed by', render: (_, c) => `${c.createdByName ?? '—'}, ${new Date(c.createdAt).toLocaleString()}` },
            {
              title: 'Status', render: (_, c) => c.status === 'Draft' ? <Tag>Awaiting approval</Tag> : (
                <Space orientation="vertical" size={0}>
                  <Tag color={c.status === 'Approved' ? 'green' : 'orange'}>{c.status}</Tag>
                  <Typography.Text type="secondary">{c.decidedByName}{c.decisionReason ? ` — ${c.decisionReason}` : ''}</Typography.Text>
                </Space>
              ),
            },
            {
              title: '', render: (_, c) => c.status === 'Draft' && can('users.approve') && c.createdBy !== me.data?.userId && c.appUserId !== me.data?.userId ? (
                <Space>
                  <Button size="small" type="primary" loading={approve.isPending}
                    onClick={() => approve.mutate(c.id, { onSuccess: () => toast.success('Approved: in force now.'), onError: (e) => toast.error(errorText(e)) })}>
                    Approve
                  </Button>
                  <Button size="small" onClick={() => setRejecting(c)}>Reject</Button>
                </Space>
              ) : null,
            },
          ]} />
      </div>
      <Modal title={proposing ? `${proposing.status === 'Active' ? 'Disable' : 'Enable'} — ${proposing.displayName}` : ''} open={proposing !== null}
        onCancel={() => setProposing(null)} onOk={send} confirmLoading={propose.isPending} okText="Propose" destroyOnHidden>
        <Typography.Paragraph type="secondary">
          {proposing?.status === 'Active'
            ? 'Once a second user approves, this person can no longer use PRIME. Their office assignment and history stay.'
            : 'Once a second user approves, this person can use PRIME again with their office assignment.'}
        </Typography.Paragraph>
        <Form form={form} layout="vertical" preserve={false}>
          <Form.Item name="reason" label="Reason" rules={[{ required: true, whitespace: true, message: 'A reason is required' }, { max: 500 }]}>
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
      <Modal title="Reject the change" open={rejecting !== null} onCancel={() => setRejecting(null)} onOk={sendReject} confirmLoading={reject.isPending}
        okText="Reject" destroyOnHidden>
        <Form form={rejectForm} layout="vertical" preserve={false}>
          <Form.Item name="reason" label="Reason" rules={[{ required: true, whitespace: true, message: 'A reason is required' }, { max: 500 }]}>
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
