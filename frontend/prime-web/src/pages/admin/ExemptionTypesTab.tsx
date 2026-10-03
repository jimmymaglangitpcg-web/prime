import { useState } from 'react';
import { Alert, Button, Checkbox, DatePicker, Form, Input, InputNumber, Modal, Space, Switch, Table, Tag, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useApproveExemptionType, useCreateExemptionType, useExemptionTypes } from '../../api/exemptions';
import { ApiRequestError } from '../../lib/apiClient';
import { exemptionKinds, parseExemptionAppliesTo, type ExemptionTypeDto, type WorkflowStatus } from '../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const statusColor: Partial<Record<WorkflowStatus, string>> = { Draft: 'default', Approved: 'green', Cancelled: 'red' };
const money = (v: number) => v.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * Exemption types (docs/analysis/assessment-listing-exemptions.md §4.1): each with its legal basis, the kinds of unit
 * it covers, whether proof is required and any assessed-value ceiling. The province's list is loaded from its content
 * pack; every version is approved by a second user.
 */
export function ExemptionTypesTab() {
  const { data = [], isLoading } = useExemptionTypes(false);
  const create = useCreateExemptionType();
  const approve = useApproveExemptionType();
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();
  const [toast, ctx] = message.useMessage();

  return (
    <div>
      {ctx}
      <Space style={{ marginBottom: 12 }} wrap>
        <Button icon={<PlusOutlined />} onClick={() => setOpen(true)}>New exemption type</Button>
        <Typography.Text type="secondary">The province's exemption types and their legal bases are loaded from its content pack.</Typography.Text>
      </Space>
      <Table<ExemptionTypeDto>
        rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No exemption types' }}
        columns={[
          { title: 'Code', dataIndex: 'code' },
          { title: 'Name', render: (_, t) => <>{t.name}{t.description && <><br /><Typography.Text type="secondary">{t.description}</Typography.Text></>}</> },
          { title: 'Legal basis', dataIndex: 'legalBasis' },
          { title: 'Applies to', render: (_, t) => parseExemptionAppliesTo(t.appliesTo).join(', ') },
          { title: 'Proof', dataIndex: 'requiresProof', render: (v: boolean) => (v ? 'Required' : 'Not required') },
          { title: 'AV ceiling', dataIndex: 'assessedValueCeiling', render: (v: number | null) => (v == null ? '—' : money(v)) },
          { title: 'In force', render: (_, t) => `${t.effectiveDate} → ${t.endDate ?? 'open'}` },
          { title: 'Status', dataIndex: 'status', render: (s: WorkflowStatus) => <Tag color={statusColor[s]}>{s}</Tag> },
          {
            title: 'Actions', render: (_, t) => t.status === 'Draft' && (
              <Button size="small" type="primary" loading={approve.isPending}
                onClick={() => approve.mutate(t.id, { onError: (e) => toast.error(errorText(e)) })}>Approve</Button>
            ),
          },
        ]}
      />
      <Modal title="New exemption type (Draft)" open={open} footer={null} width={720} destroyOnHidden onCancel={() => setOpen(false)}>
        {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not create" description={errorText(create.error)} />}
        <Form form={form} layout="vertical" initialValues={{ effectiveDate: dayjs(), appliesTo: exemptionKinds, requiresProof: true }}
          onFinish={(v) => create.mutate({
            code: v.code, name: v.name, description: v.description || null, legalBasis: v.legalBasis, remarks: v.remarks || null,
            effectiveDate: v.effectiveDate.format('YYYY-MM-DD'), appliesTo: (v.appliesTo as string[]).join(', '),
            requiresProof: !!v.requiresProof, assessedValueCeiling: v.assessedValueCeiling ?? null,
          }, { onSuccess: () => { form.resetFields(); setOpen(false); } })}>
          <Space wrap>
            <Form.Item name="code" label="Code" rules={[{ required: true }]}><Input maxLength={50} /></Form.Item>
            <Form.Item name="name" label="Name" rules={[{ required: true }]} style={{ minWidth: 360 }}><Input maxLength={200} /></Form.Item>
          </Space>
          <Form.Item name="legalBasis" label="Legal basis" rules={[{ required: true }]}><Input maxLength={500} placeholder="e.g. the section of the law that grants it" /></Form.Item>
          <Form.Item name="description" label="Description"><Input.TextArea maxLength={1000} rows={2} /></Form.Item>
          <Form.Item name="appliesTo" label="Applies to" rules={[{ required: true, message: 'Choose at least one kind of unit' }]}>
            <Checkbox.Group options={exemptionKinds.map((k) => ({ value: k, label: k === 'OtherImprovement' ? 'Other improvement' : k }))} />
          </Form.Item>
          <Space wrap>
            <Form.Item name="requiresProof" label="Documentary proof required" valuePropName="checked"><Switch /></Form.Item>
            <Form.Item name="assessedValueCeiling" label="Assessed-value ceiling (optional)"><InputNumber min={0.01} style={{ width: 200 }} /></Form.Item>
            <Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker /></Form.Item>
          </Space>
          <Form.Item name="remarks" label="Remarks"><Input maxLength={1000} /></Form.Item>
          <Button type="primary" htmlType="submit" loading={create.isPending}>Create draft</Button>
        </Form>
      </Modal>
    </div>
  );
}
