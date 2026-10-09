import { useState } from 'react';
import { Alert, Button, Checkbox, DatePicker, Form, Input, Modal, Select, Space, Table, Tag, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import {
  useDelegationActions, useDelegations, useOffices,
  type ApprovalDelegationDto, type ApprovalSubjectType, type DelegationState, type RpuType,
} from '../../api/offices';
import { ApiRequestError } from '../../lib/apiClient';
import { useCan } from '../../api/offices';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const day = (d: Dayjs | null | undefined) => (d ? d.format('YYYY-MM-DD') : '');

const subjectLabel: Record<ApprovalSubjectType, string> = {
  TaxDeclaration: 'Tax Declarations', Assessment: 'Assessments (FAAS)', PropertyTransaction: 'Property transactions',
};
const kindLabel: Record<RpuType, string> = { Land: 'Land', Building: 'Buildings', Machinery: 'Machinery', OtherImprovement: 'Other improvements' };
const stateColor: Record<DelegationState, string> = {
  Draft: 'default', Rejected: 'red', Scheduled: 'blue', InForce: 'green', Expired: 'default', Revoked: 'orange',
};
const stateLabel: Record<DelegationState, string> = {
  Draft: 'Draft', Rejected: 'Rejected', Scheduled: 'Scheduled', InForce: 'In force', Expired: 'Expired', Revoked: 'Revoked',
};

/**
 * Delegations of final approval to municipal assessors (docs/analysis/province-wide-operation.md §3.4, Q6–Q7).
 * The provincial office enters them; a second provincial user approves. While one is in force on the
 * signing date, the municipal assessor gives final approval of the records it covers.
 */
export function DelegationsTab() {
  const can = useCan();
  const rows = useDelegations();
  const offices = useOffices();
  const { create, approve, reject, revoke } = useDelegationActions();
  const [toast, context] = message.useMessage();
  const fail = (e: unknown) => toast.error(errorText(e));
  const [adding, setAdding] = useState(false);
  const [deciding, setDeciding] = useState<{ d: ApprovalDelegationDto; action: 'reject' | 'revoke' } | null>(null);
  const [form] = Form.useForm();
  const [decisionForm] = Form.useForm();

  const save = async () => {
    const v = await form.validateFields();
    create.mutate({
      officeId: v.officeId, delegatingOfficialName: v.delegatingOfficialName, delegatingOfficialPosition: v.delegatingOfficialPosition,
      instrumentReference: v.instrumentReference, instrumentDate: day(v.instrumentDate), subjectTypes: v.subjectTypes, propertyKinds: v.propertyKinds ?? [],
      validFrom: day(v.period?.[0]), validTo: day(v.period?.[1]), renewsDelegationId: v.renewsDelegationId ?? null, remarks: v.remarks,
    }, { onSuccess: () => { toast.success('Draft delegation entered; a second provincial user approves it.'); setAdding(false); }, onError: fail });
  };
  const decide = async () => {
    if (!deciding) return;
    const v = await decisionForm.validateFields();
    const done = { onSuccess: () => { toast.success(deciding.action === 'reject' ? 'Rejected.' : 'Revoked.'); setDeciding(null); }, onError: fail };
    if (deciding.action === 'reject') reject.mutate({ id: deciding.d.id, reason: v.reason }, done);
    else revoke.mutate({ id: deciding.d.id, revokedFrom: day(v.revokedFrom), reason: v.reason }, done);
  };
  const municipalOffices = (offices.data ?? []).filter((o) => o.kind === 'Municipal' && o.status === 'Active');
  const renewable = (rows.data ?? []).filter((d) => d.status === 'Approved');

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      {context}
      <Alert type="info" showIcon title="The Provincial Assessor approves FAAS and Tax Declarations unless the function is delegated"
        description="A delegation is dated, covers named records, can be renewed or revoked, and cannot be passed on: only the municipal office's Assessor signs under it." />
      <Button icon={<PlusOutlined />} onClick={() => setAdding(true)}>Enter a delegation</Button>
      <Table<ApprovalDelegationDto> rowKey="id" size="small" loading={rows.isLoading} dataSource={rows.data ?? []} pagination={{ pageSize: 20 }} scroll={{ x: true }}
        columns={[
          { title: 'Office', dataIndex: 'officeCode' },
          { title: 'Instrument', render: (_, d) => <span>{d.instrumentReference}<br /><Typography.Text type="secondary">{d.instrumentDate}</Typography.Text></span> },
          { title: 'Covers', render: (_, d) => (
            <Space size={[4, 4]} wrap>
              {d.subjectTypes.map((s) => <Tag key={s}>{subjectLabel[s]}</Tag>)}
              {d.propertyKinds.length > 0 && <Typography.Text type="secondary">({d.propertyKinds.map((k) => kindLabel[k]).join(', ')} only)</Typography.Text>}
            </Space>
          ) },
          { title: 'Period', render: (_, d) => `${d.validFrom} → ${d.validTo}${d.revokedFrom ? ` (revoked from ${d.revokedFrom})` : ''}` },
          { title: 'State', dataIndex: 'state', render: (s: DelegationState) => <Tag color={stateColor[s]}>{stateLabel[s]}</Tag> },
          {
            title: '', render: (_, d) => (
              <Space>
                {d.status === 'Draft' && can('users.approve') && (
                  <>
                    <Button size="small" loading={approve.isPending && approve.variables === d.id}
                      onClick={() => approve.mutate(d.id, { onSuccess: () => toast.success('Approved.'), onError: fail })}>Approve</Button>
                    <Button size="small" onClick={() => setDeciding({ d, action: 'reject' })}>Reject</Button>
                  </>
                )}
                {d.status === 'Approved' && !d.revokedFrom && (d.state === 'InForce' || d.state === 'Scheduled') && (
                  <Button size="small" danger onClick={() => setDeciding({ d, action: 'revoke' })}>Revoke</Button>
                )}
              </Space>
            ),
          },
        ]} />
      <Modal title="Enter a delegation of final approval" open={adding} onCancel={() => setAdding(false)} onOk={save} confirmLoading={create.isPending}
        okText="Enter draft" destroyOnHidden width={640}>
        <Form form={form} layout="vertical" initialValues={{ subjectTypes: ['TaxDeclaration', 'Assessment'] }}>
          <Form.Item name="officeId" label="Delegated to (municipal office)" rules={[{ required: true }]}>
            <Select showSearch optionFilterProp="label" options={municipalOffices.map((o) => ({ value: o.id, label: `${o.code} — ${o.name}` }))} />
          </Form.Item>
          <Space wrap style={{ width: '100%' }}>
            <Form.Item name="delegatingOfficialName" label="Delegated by (name)" rules={[{ required: true }]}><Input style={{ width: 280 }} /></Form.Item>
            <Form.Item name="delegatingOfficialPosition" label="Position" rules={[{ required: true }]}><Input style={{ width: 280 }} /></Form.Item>
          </Space>
          <Space wrap style={{ width: '100%' }}>
            <Form.Item name="instrumentReference" label="Instrument (e.g. office order no.)" rules={[{ required: true }]}><Input style={{ width: 280 }} /></Form.Item>
            <Form.Item name="instrumentDate" label="Instrument date" rules={[{ required: true }]}><DatePicker style={{ width: 280 }} /></Form.Item>
          </Space>
          <Form.Item name="subjectTypes" label="Records covered" rules={[{ required: true, message: 'Choose at least one' }]}>
            <Checkbox.Group options={(Object.keys(subjectLabel) as ApprovalSubjectType[]).map((s) => ({ value: s, label: subjectLabel[s] }))} />
          </Form.Item>
          <Form.Item name="propertyKinds" label="Property kinds" extra="Leave empty to cover every kind.">
            <Checkbox.Group options={(Object.keys(kindLabel) as RpuType[]).map((k) => ({ value: k, label: kindLabel[k] }))} />
          </Form.Item>
          <Form.Item name="period" label="Valid from – to" rules={[{ required: true }]}><DatePicker.RangePicker style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="renewsDelegationId" label="Renews (optional)">
            <Select allowClear options={renewable.map((d) => ({ value: d.id, label: `${d.officeCode}: ${d.instrumentReference} (${d.validFrom} → ${d.validTo})` }))} />
          </Form.Item>
          <Form.Item name="remarks" label="Remarks"><Input.TextArea rows={2} /></Form.Item>
        </Form>
      </Modal>
      <Modal title={deciding?.action === 'reject' ? 'Reject the draft delegation' : 'Revoke the delegation'} open={deciding !== null}
        onCancel={() => setDeciding(null)} onOk={decide} confirmLoading={reject.isPending || revoke.isPending}
        okText={deciding?.action === 'reject' ? 'Reject' : 'Revoke'} okButtonProps={{ danger: true }} destroyOnHidden>
        <Form form={decisionForm} layout="vertical">
          {deciding?.action === 'revoke' && (
            <Form.Item name="revokedFrom" label="No longer applies from" rules={[{ required: true }]}
              extra="Today or later: approvals already signed under the delegation stand.">
              <DatePicker style={{ width: '100%' }} />
            </Form.Item>
          )}
          <Form.Item name="reason" label="Reason" rules={[{ required: true }]}><Input /></Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
