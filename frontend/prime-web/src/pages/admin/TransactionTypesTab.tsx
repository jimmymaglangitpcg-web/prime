import { useState } from 'react';
import { Alert, Button, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Switch, Table, Tag, Typography, message } from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useApproveTransactionType, useCreateTransactionType, useTransactionTypes } from '../../api/transactions';
import { ApiRequestError } from '../../lib/apiClient';
import { transactionKinds, type TransactionTypeDto, type WorkflowStatus } from '../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const statusColor: Partial<Record<WorkflowStatus, string>> = { Draft: 'default', Approved: 'green', Cancelled: 'red' };

/**
 * The transaction catalogue (docs/FORMS-REVISION-PLAN.md §4.7): LGU/LAM
 * codes and names over PRIME's statutory kinds, each with its prerequisite
 * checklist. New versions are approved by a second user.
 */
export function TransactionTypesTab() {
  const { data = [], isLoading } = useTransactionTypes(false);
  const create = useCreateTransactionType();
  const approve = useApproveTransactionType();
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();
  const [toast, ctx] = message.useMessage();

  return (
    <div>
      {ctx}
      <Space style={{ marginBottom: 12 }} wrap>
        <Button icon={<PlusOutlined />} onClick={() => setOpen(true)}>New transaction type</Button>
        <Typography.Text type="secondary">Official codes and prerequisites come from the LAM; enter them here when available.</Typography.Text>
      </Space>
      <Table<TransactionTypeDto>
        rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }}
        columns={[
          { title: 'Code', dataIndex: 'code' },
          { title: 'Name', dataIndex: 'name' },
          { title: 'Kind', dataIndex: 'kind' },
          { title: 'Rank', dataIndex: 'rank', render: (v: number | null) => v ?? '—' },
          {
            title: 'Requirements', render: (_, t) => t.requirements.length === 0 ? '—' : (
              <ol style={{ margin: 0, paddingLeft: 18 }}>
                {t.requirements.map((r) => <li key={r.sequence}>{r.label}{r.isMandatory ? ' *' : ''}{r.legalBasis ? ` (${r.legalBasis})` : ''}</li>)}
              </ol>
            ),
          },
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
      <Modal title="New transaction type (Draft)" open={open} footer={null} width={820} destroyOnHidden onCancel={() => setOpen(false)}>
        {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not create" description={errorText(create.error)} />}
        <Form form={form} layout="vertical" initialValues={{ effectiveDate: dayjs(), requirements: [] }}
          onFinish={(v) => create.mutate({
            ...v,
            effectiveDate: v.effectiveDate.format('YYYY-MM-DD'),
            requirements: (v.requirements ?? []).map((r: { code: string; label: string; isMandatory?: boolean; legalBasis?: string }, i: number) =>
              ({ ...r, isMandatory: r.isMandatory ?? true, sequence: i + 1 })),
          }, { onSuccess: () => { form.resetFields(); setOpen(false); } })}>
          <Space wrap>
            <Form.Item name="code" label="Code" rules={[{ required: true }, { max: 20 }]}><Input style={{ width: 120 }} /></Form.Item>
            <Form.Item name="name" label="Name" rules={[{ required: true }]}><Input style={{ width: 280 }} /></Form.Item>
            <Form.Item name="kind" label="Statutory kind" rules={[{ required: true }]}>
              <Select style={{ width: 210 }} options={transactionKinds.map((k) => ({ value: k, label: k }))} />
            </Form.Item>
            <Form.Item name="rank" label="Rank"><InputNumber style={{ width: 90 }} /></Form.Item>
          </Space>
          <Form.Item name="description" label="Description"><Input /></Form.Item>
          <Typography.Text strong>Prerequisites (checklist)</Typography.Text>
          <Form.List name="requirements">
            {(fields, { add, remove }) => (
              <>
                {fields.map((field, i) => (
                  <Space key={field.key} align="baseline" wrap style={{ display: 'flex' }}>
                    <span>{i + 1}.</span>
                    <Form.Item name={[field.name, 'code']} rules={[{ required: true, message: 'Code' }]}><Input placeholder="CODE" style={{ width: 130 }} /></Form.Item>
                    <Form.Item name={[field.name, 'label']} rules={[{ required: true, message: 'Label' }]}><Input placeholder="Requirement" style={{ width: 260 }} /></Form.Item>
                    <Form.Item name={[field.name, 'legalBasis']}><Input placeholder="Legal basis" style={{ width: 150 }} /></Form.Item>
                    <Form.Item name={[field.name, 'isMandatory']} valuePropName="checked" initialValue>
                      <Switch checkedChildren="Mandatory" unCheckedChildren="Optional" />
                    </Form.Item>
                    <MinusCircleOutlined aria-label="Remove requirement" onClick={() => remove(field.name)} />
                  </Space>
                ))}
                <Button type="dashed" icon={<PlusOutlined />} onClick={() => add()} style={{ marginBottom: 12 }}>Add requirement</Button>
              </>
            )}
          </Form.List>
          <Form.Item name="legalBasis" label="Legal basis / source" rules={[{ required: true }]}><Input placeholder="e.g. LAM … — or 'DEMO'" /></Form.Item>
          <Form.Item name="effectiveDate" label="Effective date" rules={[{ required: true }]}><DatePicker /></Form.Item>
          <Button type="primary" htmlType="submit" loading={create.isPending}>Create draft</Button>
        </Form>
      </Modal>
    </div>
  );
}
