import { useState } from 'react';
import { Alert, Button, Checkbox, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table, Tabs, Tag, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { useAccountMappings, useApproveAccountMapping, useCreateAccountMapping, useCreatePaymentMode, usePaymentModes } from '../../api/payments';
import { useTaxTypes } from '../../api/referenceData';
import { ApiRequestError } from '../../lib/apiClient';
import type { BillingComponent, CollectionYearCategory, PaymentModeDto, RevenueAccountMappingDto, WorkflowStatus } from '../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const components: BillingComponent[] = ['Tax', 'Discount', 'Penalty', 'Interest'];
const categories: CollectionYearCategory[] = ['Current', 'Prior', 'Advance'];

/**
 * Collection configuration (docs/analysis/collection.md §3): the modes of
 * payment the LGU accepts, and the revenue account each collection line is
 * coded to (DOF DO 054-2024 §7.1). Every code comes from the LGU's chart of
 * accounts; PRIME supplies none. Mappings are approved by another user and
 * never edited — a change is a new version from its effective date.
 */
export function CollectionSetupPage() {
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Title level={3} style={{ margin: 0 }}>Collection Setup</Typography.Title>
      <Alert type="warning" showIcon title="Enter only the LGU's own accounts and modes of payment"
        description="A payment cannot be posted until every line it creates (tax, discount, penalty, interest; current, prior or advance year) has an approved revenue account. Receipt and transaction numbering are configured under Forms & Numbering." />
      <Tabs items={[
        { key: 'accounts', label: 'Revenue accounts', children: <AccountsTab /> },
        { key: 'modes', label: 'Modes of payment', children: <ModesTab /> },
      ]} />
    </Space>
  );
}

function AccountsTab() {
  const { data = [], isLoading } = useAccountMappings();
  const approve = useApproveAccountMapping();
  const [toast, toastContext] = message.useMessage();
  const [creating, setCreating] = useState(false);
  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      {toastContext}
      <Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>New account mapping</Button>
      <Table<RevenueAccountMappingDto>
        rowKey="id"
        size="small"
        loading={isLoading}
        dataSource={data}
        pagination={{ pageSize: 50, hideOnSinglePage: true }}
        scroll={{ x: 'max-content' }}
        columns={[
          { title: 'Tax type', dataIndex: 'taxTypeCode' },
          { title: 'Component', dataIndex: 'component' },
          { title: 'Year', dataIndex: 'yearCategory' },
          { title: 'Account', key: 'account', render: (_, m) => `${m.accountCode} — ${m.accountName}` },
          { title: 'Fund', dataIndex: 'fund', render: (v: string | null) => v ?? '—' },
          { title: 'In force', key: 'period', render: (_, m) => `${m.effectiveDate} → ${m.endDate ?? 'open'}` },
          { title: 'Status', dataIndex: 'status', render: (s: WorkflowStatus) => <Tag color={s === 'Approved' ? 'green' : s === 'Draft' ? 'default' : 'orange'}>{s}</Tag> },
          {
            title: '', key: 'approve', render: (_, m) => m.status === 'Draft' && (
              <Button size="small" loading={approve.isPending} onClick={() => approve.mutate(m.id, { onError: (e) => toast.error(errorText(e)) })}>Approve</Button>
            ),
          },
        ]}
      />
      {creating && <AccountModal onClose={() => setCreating(false)} />}
    </Space>
  );
}

interface AccountForm {
  taxTypeId: string;
  components: BillingComponent[];
  yearCategories: CollectionYearCategory[];
  accountCode: string;
  accountName: string;
  fund?: string;
  legalBasis: string;
  effectiveDate: Dayjs;
  remarks?: string;
}

/** One account for one or more (component, year) pairs of a tax type — each pair is saved as its own mapping. */
function AccountModal({ onClose }: { onClose: () => void }) {
  const [form] = Form.useForm<AccountForm>();
  const taxTypes = useTaxTypes();
  const create = useCreateAccountMapping();
  const [error, setError] = useState<string | null>(null);

  async function submit(values: AccountForm) {
    setError(null);
    try {
      for (const component of values.components) {
        for (const yearCategory of values.yearCategories) {
          await create.mutateAsync({
            taxTypeId: values.taxTypeId, component, yearCategory, accountCode: values.accountCode.trim(), accountName: values.accountName.trim(),
            fund: values.fund?.trim() || null, legalBasis: values.legalBasis.trim(), effectiveDate: values.effectiveDate.format('YYYY-MM-DD'),
            remarks: values.remarks?.trim() || null,
          });
        }
      }
      onClose();
    } catch (e) {
      setError(errorText(e) ?? 'Not saved');
    }
  }

  return (
    <Modal open title="New revenue account mapping" okText="Save as draft" onCancel={onClose} onOk={() => form.submit()} confirmLoading={create.isPending} destroyOnHidden>
      {error && <Alert type="error" showIcon title="Not saved" description={error} style={{ marginBottom: 12 }} />}
      <Form form={form} layout="vertical" onFinish={submit} initialValues={{ components: ['Tax'], yearCategories: ['Current'] }}>
        <Form.Item name="taxTypeId" label="Tax type" rules={[{ required: true }]}>
          <Select options={(taxTypes.data ?? []).map((t) => ({ value: t.id, label: `${t.code} — ${t.name}` }))} />
        </Form.Item>
        <Form.Item name="components" label="Lines" rules={[{ required: true }]}>
          <Checkbox.Group options={components} />
        </Form.Item>
        <Form.Item name="yearCategories" label="Tax year relative to the year of payment" rules={[{ required: true }]}>
          <Checkbox.Group options={categories} />
        </Form.Item>
        <Form.Item name="accountCode" label="Account code" rules={[{ required: true, max: 50 }]}><Input /></Form.Item>
        <Form.Item name="accountName" label="Account name" rules={[{ required: true, max: 300 }]}><Input /></Form.Item>
        <Form.Item name="fund" label="Fund" rules={[{ max: 100 }]}><Input /></Form.Item>
        <Form.Item name="legalBasis" label="Basis (e.g. the LGU's chart of accounts)" rules={[{ required: true, max: 500 }]}><Input /></Form.Item>
        <Form.Item name="effectiveDate" label="Effective from" rules={[{ required: true }]}><DatePicker /></Form.Item>
        <Form.Item name="remarks" label="Remarks"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
  );
}

function ModesTab() {
  const { data = [], isLoading } = usePaymentModes();
  const [creating, setCreating] = useState(false);
  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>New mode of payment</Button>
      <Table<PaymentModeDto>
        rowKey="id"
        size="small"
        loading={isLoading}
        dataSource={data}
        pagination={false}
        columns={[
          { title: 'Code', dataIndex: 'code' },
          { title: 'Name', dataIndex: 'name' },
          { title: 'Needs reference', dataIndex: 'requiresReference', render: (v: boolean) => (v ? 'Yes' : 'No') },
          { title: 'Change allowed', dataIndex: 'allowsChange', render: (v: boolean) => (v ? 'Yes' : 'No') },
          { title: 'Active', dataIndex: 'isActive', render: (v: boolean) => (v ? 'Yes' : 'No') },
        ]}
      />
      {creating && <ModeModal onClose={() => setCreating(false)} />}
    </Space>
  );
}

function ModeModal({ onClose }: { onClose: () => void }) {
  const [form] = Form.useForm<{ code: string; name: string; description?: string; requiresReference: boolean; allowsChange: boolean; sortOrder: number }>();
  const create = useCreatePaymentMode();
  return (
    <Modal open title="New mode of payment" okText="Save" onCancel={onClose} onOk={() => form.submit()} confirmLoading={create.isPending} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon title="Not saved" description={errorText(create.error)} style={{ marginBottom: 12 }} />}
      <Form form={form} layout="vertical" initialValues={{ requiresReference: false, allowsChange: false, sortOrder: 0 }}
        onFinish={(v) => create.mutate({ ...v, description: v.description?.trim() || null }, { onSuccess: onClose })}>
        <Form.Item name="code" label="Code" rules={[{ required: true, max: 50 }]}><Input /></Form.Item>
        <Form.Item name="name" label="Name" rules={[{ required: true, max: 200 }]}><Input /></Form.Item>
        <Form.Item name="description" label="Description"><Input /></Form.Item>
        <Form.Item name="requiresReference" valuePropName="checked"><Checkbox>Needs a reference (e.g. check number)</Checkbox></Form.Item>
        <Form.Item name="allowsChange" valuePropName="checked"><Checkbox>Change may be given (cash)</Checkbox></Form.Item>
        <Form.Item name="sortOrder" label="Order"><InputNumber min={0} /></Form.Item>
      </Form>
    </Modal>
  );
}
