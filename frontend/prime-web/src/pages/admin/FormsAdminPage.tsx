import { useState } from 'react';
import { Alert, Button, Card, DatePicker, Form, Input, Modal, Select, Space, Switch, Table, Tabs, Tag, Typography, message } from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  useApprovalChains,
  useApproveApprovalChain,
  useApproveFormDefinition,
  useApproveNumberingScheme,
  useCreateApprovalChain,
  useCreateFormDefinition,
  useCreateNumberingScheme,
  useFormDefinition,
  useFormDefinitions,
  useNumberingSchemes,
} from '../../api/forms';
import { apiGet, ApiRequestError } from '../../lib/apiClient';
import { TransactionTypesTab } from './TransactionTypesTab';
import type {
  ApprovalChainDto,
  FormAuthority,
  FormDefinitionDto,
  FormSubjectType,
  NumberedDocumentKind,
  NumberingSchemeDto,
  WorkflowStatus,
} from '../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const statusColor: Partial<Record<WorkflowStatus, string>> = { Draft: 'default', Approved: 'green', Cancelled: 'red' };
const statusTag = (s: WorkflowStatus) => <Tag color={statusColor[s]}>{s}</Tag>;
const period = (x: { effectiveDate: string; endDate: string | null }) => `${x.effectiveDate} → ${x.endDate ?? 'open'}`;

const kinds: NumberedDocumentKind[] = ['PropertyIdentificationNumber', 'TaxDeclaration', 'TaxBill', 'Faas', 'NoticeOfAssessment', 'OfficialReceipt', 'PropertyTransaction'];
const authorities: FormAuthority[] = ['PrimeProvisional', 'Lam', 'Blgf', 'LguOrdinance', 'Other'];
const subjects: FormSubjectType[] = ['TaxBill', 'TaxDeclaration', 'NoticeOfAssessment', 'Assessment'];

/** Shared "legal basis + effective date + remarks" fields every configuration version carries. */
function HeaderFields() {
  return (
    <>
      <Form.Item name="legalBasis" label="Legal basis / source" rules={[{ required: true, message: 'Cite the source (CLAUDE.md §6)' }]}>
        <Input placeholder="e.g. LAM (DOF DC 004-2025) Form __, p. __ — or 'DEMO'" />
      </Form.Item>
      <Form.Item name="effectiveDate" label="Effective date" rules={[{ required: true }]}>
        <DatePicker style={{ width: '100%' }} />
      </Form.Item>
      <Form.Item name="remarks" label="Remarks">
        <Input />
      </Form.Item>
    </>
  );
}

function ApproveButton({ status, onApprove, loading }: { status: WorkflowStatus; onApprove: () => void; loading: boolean }) {
  return status === 'Draft' ? (
    <Button size="small" type="primary" loading={loading} onClick={onApprove}>
      Approve
    </Button>
  ) : null;
}

// --- Numbering schemes ---

function NumberingTab() {
  const { data = [], isLoading } = useNumberingSchemes();
  const create = useCreateNumberingScheme();
  const approve = useApproveNumberingScheme();
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();
  const [toast, ctx] = message.useMessage();

  return (
    <div>
      {ctx}
      <Space style={{ marginBottom: 12 }} wrap>
        <Button icon={<PlusOutlined />} onClick={() => setOpen(true)}>New numbering scheme</Button>
        <Typography.Text type="secondary">With no approved scheme for a kind, numbers are typed by hand.</Typography.Text>
      </Space>
      <Table<NumberingSchemeDto>
        rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }}
        columns={[
          { title: 'Numbers', dataIndex: 'appliesTo' },
          { title: 'Name', dataIndex: 'name' },
          { title: 'Pattern', dataIndex: 'pattern', render: (v: string) => <code>{v}</code> },
          { title: 'Example', dataIndex: 'example', render: (v: string) => <code>{v}</code> },
          { title: 'Manual entry', dataIndex: 'allowManualEntry', render: (v: boolean) => (v ? 'Allowed' : 'No') },
          { title: 'In force', render: (_, x) => period(x) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          {
            title: 'Actions', render: (_, x) => (
              <ApproveButton status={x.status} loading={approve.isPending}
                onApprove={() => approve.mutate(x.id, { onError: (e) => toast.error(errorText(e)) })} />
            ),
          },
        ]}
      />
      <Modal title="New numbering scheme (Draft)" open={open} footer={null} destroyOnHidden onCancel={() => setOpen(false)}>
        {create.isError && <Alert type="error" showIcon title="Could not create" description={errorText(create.error)} style={{ marginBottom: 12 }} />}
        <Form form={form} layout="vertical" initialValues={{ effectiveDate: dayjs(), allowManualEntry: false }}
          onFinish={(v) => create.mutate({ ...v, effectiveDate: v.effectiveDate.format('YYYY-MM-DD') }, {
            onSuccess: () => { form.resetFields(); setOpen(false); },
          })}>
          <Form.Item name="appliesTo" label="Numbers" rules={[{ required: true }]}>
            <Select options={kinds.map((k) => ({ value: k, label: k }))} />
          </Form.Item>
          <Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="pattern" label="Pattern" rules={[{ required: true }]}
            extra="Tokens: {YEAR} {PROV} {MUN} {BRGY} and exactly one {SEQ} or {SEQ:n} (zero-padded). Example: TD-{MUN}-{YEAR}-{SEQ:5}">
            <Input />
          </Form.Item>
          <Form.Item name="validationRegex" label="Format check for typed numbers (regular expression, optional)"><Input /></Form.Item>
          <Form.Item name="allowManualEntry" label="Allow typed numbers" valuePropName="checked"><Switch /></Form.Item>
          <HeaderFields />
          <Button type="primary" htmlType="submit" loading={create.isPending}>Create draft</Button>
        </Form>
      </Modal>
    </div>
  );
}

// --- Form versions ---

function FormsTab() {
  const { data = [], isLoading } = useFormDefinitions();
  const create = useCreateFormDefinition();
  const approve = useApproveFormDefinition();
  const [open, setOpen] = useState(false);
  const [viewing, setViewing] = useState<string>();
  const viewed = useFormDefinition(viewing);
  const [form] = Form.useForm();
  const [toast, ctx] = message.useMessage();
  const codes = [...new Set(data.map((d) => d.code))];

  async function startFromLatest(code: string) {
    const latest = data.filter((d) => d.code === code).sort((a, b) => b.version - a.version)[0];
    if (!latest) return;
    const full = await apiGet<FormDefinitionDto>(`/api/forms/definitions/${latest.id}`);
    form.setFieldsValue({ title: full.title, subjectType: full.subjectType, templateBody: full.templateBody });
  }

  return (
    <div>
      {ctx}
      <Space style={{ marginBottom: 12 }} wrap>
        <Button icon={<PlusOutlined />} onClick={() => setOpen(true)}>New form version</Button>
        <Typography.Text type="secondary">
          Keep LAM-derived templates out of git — they live only in this database (plan §7).
        </Typography.Text>
      </Space>
      <Table<FormDefinitionDto>
        rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }}
        columns={[
          { title: 'Code', dataIndex: 'code' },
          { title: 'v', dataIndex: 'version' },
          { title: 'Title', dataIndex: 'title' },
          { title: 'Renders', dataIndex: 'subjectType' },
          { title: 'Authority', dataIndex: 'authority', render: (a: FormAuthority) => <Tag color={a === 'PrimeProvisional' ? 'red' : 'blue'}>{a}</Tag> },
          { title: 'Source', dataIndex: 'sourceReference' },
          { title: 'In force', render: (_, x) => period(x) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          {
            title: 'Actions', render: (_, x) => (
              <Space>
                <Button size="small" onClick={() => setViewing(x.id)}>Template</Button>
                <ApproveButton status={x.status} loading={approve.isPending}
                  onApprove={() => approve.mutate(x.id, { onError: (e) => toast.error(errorText(e)) })} />
              </Space>
            ),
          },
        ]}
      />
      <Modal title={viewed.data ? `${viewed.data.code} v${viewed.data.version} template` : 'Template'} open={!!viewing} footer={null}
        width={900} onCancel={() => setViewing(undefined)} destroyOnHidden>
        <pre style={{ maxHeight: 500, overflow: 'auto', fontSize: 12, background: '#fafafa', padding: 8 }}>{viewed.data?.templateBody}</pre>
      </Modal>
      <Modal title="New form version (Draft)" open={open} footer={null} width={900} destroyOnHidden onCancel={() => setOpen(false)}>
        {create.isError && <Alert type="error" showIcon title="Could not create" description={errorText(create.error)} style={{ marginBottom: 12 }} />}
        <Form form={form} layout="vertical" initialValues={{ effectiveDate: dayjs(), authority: 'Lam' }}
          onFinish={(v) => create.mutate({ ...v, effectiveDate: v.effectiveDate.format('YYYY-MM-DD') }, {
            onSuccess: () => { form.resetFields(); setOpen(false); },
          })}>
          <Form.Item name="code" label="Form code" rules={[{ required: true }]}
            extra="An existing code adds the next version of that form; pick one to start from its current template.">
            <Select showSearch options={codes.map((c) => ({ value: c, label: c }))} onChange={(c: string) => void startFromLatest(c)} />
          </Form.Item>
          <Form.Item name="title" label="Title" rules={[{ required: true }]}><Input /></Form.Item>
          <Space wrap>
            <Form.Item name="subjectType" label="Renders" rules={[{ required: true }]}>
              <Select style={{ width: 200 }} options={subjects.map((s) => ({ value: s, label: s }))} />
            </Form.Item>
            <Form.Item name="authority" label="Authority" rules={[{ required: true }]}>
              <Select style={{ width: 200 }} options={authorities.map((a) => ({ value: a, label: a }))} />
            </Form.Item>
          </Space>
          <Form.Item name="sourceReference" label="Source reference (citation only)"><Input placeholder="e.g. LAM Form __, p. __" /></Form.Item>
          <Form.Item name="templateBody" label="Template (Liquid/HTML)" rules={[{ required: true }]}
            extra="Values are HTML-escaped; scripts never run. Filters: money, percent, date_ph.">
            <Input.TextArea rows={14} style={{ fontFamily: 'monospace', fontSize: 12 }} />
          </Form.Item>
          <HeaderFields />
          <Button type="primary" htmlType="submit" loading={create.isPending}>Create draft</Button>
        </Form>
      </Modal>
    </div>
  );
}

// --- Approval chains ---

function ChainsTab() {
  const { data = [], isLoading } = useApprovalChains();
  const create = useCreateApprovalChain();
  const approve = useApproveApprovalChain();
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();
  const [toast, ctx] = message.useMessage();

  return (
    <div>
      {ctx}
      <Space style={{ marginBottom: 12 }} wrap>
        <Button icon={<PlusOutlined />} onClick={() => setOpen(true)}>New approval chain</Button>
        <Typography.Text type="secondary">Without an approved chain, approval is the two-person maker-checker.</Typography.Text>
      </Space>
      <Table<ApprovalChainDto>
        rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }}
        columns={[
          { title: 'Approves', dataIndex: 'subjectType' },
          { title: 'Name', dataIndex: 'name' },
          {
            title: 'Steps', render: (_, x) => (
              <ol style={{ margin: 0, paddingLeft: 18 }}>
                {x.steps.map((s) => <li key={s.sequence}>{s.label}{s.signatoryPosition ? ` — ${s.signatoryPosition}` : ''} <code>{s.stepCode}</code></li>)}
              </ol>
            ),
          },
          { title: 'In force', render: (_, x) => period(x) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          {
            title: 'Actions', render: (_, x) => (
              <ApproveButton status={x.status} loading={approve.isPending}
                onApprove={() => approve.mutate(x.id, { onError: (e) => toast.error(errorText(e)) })} />
            ),
          },
        ]}
      />
      <Modal title="New approval chain (Draft)" open={open} footer={null} width={720} destroyOnHidden onCancel={() => setOpen(false)}>
        {create.isError && <Alert type="error" showIcon title="Could not create" description={errorText(create.error)} style={{ marginBottom: 12 }} />}
        <Form form={form} layout="vertical" initialValues={{ effectiveDate: dayjs(), subjectType: 'Assessment', steps: [{}] }}
          onFinish={(v) => create.mutate({
            ...v,
            effectiveDate: v.effectiveDate.format('YYYY-MM-DD'),
            steps: v.steps.map((s: { stepCode: string; label: string; signatoryPosition?: string }, i: number) => ({ ...s, sequence: i + 1 })),
          }, { onSuccess: () => { form.resetFields(); setOpen(false); } })}>
          <Form.Item name="subjectType" label="Approves" rules={[{ required: true }]}>
            <Select options={['Assessment', 'TaxDeclaration', 'PropertyTransaction'].map((s) => ({ value: s, label: s }))} />
          </Form.Item>
          <Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item>
          <Typography.Text strong>Steps, in signing order</Typography.Text>
          <Form.List name="steps">
            {(fields, { add, remove }) => (
              <>
                {fields.map((field, i) => (
                  <Space key={field.key} align="baseline" wrap style={{ display: 'flex' }}>
                    <span>{i + 1}.</span>
                    <Form.Item name={[field.name, 'stepCode']} rules={[{ required: true, pattern: /^[A-Z][A-Z0-9_]*$/, message: 'UPPER_SNAKE_CASE' }]}>
                      <Input placeholder="STEP_CODE" />
                    </Form.Item>
                    <Form.Item name={[field.name, 'label']} rules={[{ required: true }]}><Input placeholder="Printed label" /></Form.Item>
                    <Form.Item name={[field.name, 'signatoryPosition']}><Input placeholder="Position (optional)" /></Form.Item>
                    {fields.length > 1 && <MinusCircleOutlined aria-label="Remove step" onClick={() => remove(field.name)} />}
                  </Space>
                ))}
                <Button type="dashed" icon={<PlusOutlined />} onClick={() => add()} style={{ marginBottom: 12 }}>Add step</Button>
              </>
            )}
          </Form.List>
          <HeaderFields />
          <Button type="primary" htmlType="submit" loading={create.isPending}>Create draft</Button>
        </Form>
      </Modal>
    </div>
  );
}

/**
 * docs/FORMS-REVISION-PLAN.md §4 — the configuration that lets the LAM's
 * forms, numbering and signatory chains be adopted without code changes.
 * Every change is a new Draft version approved by a second user. Role gating is Phase 12.
 */
export function FormsAdminPage() {
  return (
    <div>
      <Typography.Title level={3}>Forms &amp; Numbering</Typography.Title>
      <Card>
        <Tabs items={[
          { key: 'numbering', label: 'Numbering schemes', children: <NumberingTab /> },
          { key: 'forms', label: 'Form versions', children: <FormsTab /> },
          { key: 'chains', label: 'Approval chains', children: <ChainsTab /> },
          { key: 'transactions', label: 'Transaction types', children: <TransactionTypesTab /> },
        ]} />
      </Card>
    </div>
  );
}
