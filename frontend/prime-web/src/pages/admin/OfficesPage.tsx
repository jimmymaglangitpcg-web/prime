import { useState } from 'react';
import { Alert, Button, DatePicker, Form, Input, Modal, Select, Space, Table, Tabs, Tag, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import {
  useApproveAssignment, useApproveJurisdiction, useCreateAssignment, useCreateJurisdiction, useCreateOffice, useEndAssignment,
  useOfficeAssignments, useOfficeJurisdictions, useOffices, useRoles, useUpdateOffice, useUpdateUserLicence, useUsers,
  type OfficeAssignmentDto, type OfficeDto, type OfficeJurisdictionDto, type OfficeKind, type UserSummaryDto, type WorkflowStatus,
} from '../../api/offices';
import { useMunicipalities, useProvinces } from '../../api/referenceData';
import { ApiRequestError } from '../../lib/apiClient';
import { DelegationsTab } from './DelegationsTab';
import { AccountsTab } from './AccountsTab';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const day = (d: Dayjs | null | undefined) => (d ? d.format('YYYY-MM-DD') : '');
const period = (from: string, to: string | null) => `${from} → ${to ?? 'open'}`;
const statusTag = (s: WorkflowStatus) => <Tag color={s === 'Approved' ? 'green' : s === 'Draft' ? 'default' : 'orange'}>{s}</Tag>;

function useToast() {
  const [toast, context] = message.useMessage();
  return { context, ok: (t: string) => toast.success(t), fail: (e: unknown) => toast.error(errorText(e)) };
}

/**
 * Offices, the municipalities they cover and who works in them
 * (docs/analysis/province-wide-operation.md §3.1–§3.2). An office alone grants
 * nothing; jurisdictions and assignments do, so both are drafts that a second
 * user approves. Nothing is deleted: a change is a new version from its date.
 */
export function OfficesPage() {
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ margin: 0 }}>Offices</Typography.Title>
        <Typography.Paragraph type="secondary" style={{ maxWidth: 900, marginBottom: 0 }}>
          The Provincial Assessor&apos;s Office and the municipal assessors&apos; offices. Municipal offices work only in the municipalities they
          cover; the provincial office sees the whole province. Coverage and staff assignments take effect once a second user approves them.
        </Typography.Paragraph>
      </div>
      <Tabs items={[
        { key: 'offices', label: 'Offices', children: <OfficesTab /> },
        { key: 'jurisdictions', label: 'Coverage', children: <JurisdictionsTab /> },
        { key: 'assignments', label: 'Staff', children: <AssignmentsTab /> },
        { key: 'delegations', label: 'Delegations', children: <DelegationsTab /> },
        { key: 'licences', label: 'Signatory licences', children: <LicencesTab /> },
        { key: 'accounts', label: 'Accounts', children: <AccountsTab /> },
      ]} />
    </Space>
  );
}

function OfficesTab() {
  const offices = useOffices();
  const create = useCreateOffice();
  const update = useUpdateOffice();
  const { context, ok, fail } = useToast();
  const [editing, setEditing] = useState<OfficeDto | 'new' | null>(null);
  const [form] = Form.useForm();

  const open = (o: OfficeDto | 'new') => setEditing(o);
  const save = async () => {
    const v = await form.validateFields();
    const done = { onSuccess: () => { ok('Office saved.'); setEditing(null); }, onError: fail };
    if (editing === 'new') create.mutate(v, done);
    else if (editing) update.mutate({ id: editing.id, ...v }, done);
  };

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      {context}
      <Button icon={<PlusOutlined />} onClick={() => open('new')}>New office</Button>
      <Table<OfficeDto> rowKey="id" size="small" loading={offices.isLoading} dataSource={offices.data ?? []} pagination={false} scroll={{ x: true }}
        columns={[
          { title: 'Code', dataIndex: 'code' },
          { title: 'Name', dataIndex: 'name' },
          { title: 'Letterhead LGU', dataIndex: 'lguName', render: (v: string | null) => v ?? '—' },
          { title: 'Kind', dataIndex: 'kind', render: (k: OfficeKind) => <Tag color={k === 'Provincial' ? 'blue' : 'default'}>{k}</Tag> },
          { title: 'Head', dataIndex: 'headPosition', render: (v: string | null) => v ?? '—' },
          { title: 'Status', dataIndex: 'status', render: (s: string) => <Tag color={s === 'Active' ? 'green' : 'default'}>{s}</Tag> },
          { title: '', render: (_, o) => <Button size="small" onClick={() => open(o)}>Edit</Button> },
        ]} />
      <Modal title={editing === 'new' ? 'New office' : 'Edit office'} open={editing !== null} onCancel={() => setEditing(null)} onOk={save}
        confirmLoading={create.isPending || update.isPending} okText="Save" destroyOnHidden>
        <Form form={form} layout="vertical" initialValues={editing && editing !== 'new' ? editing : { kind: 'Municipal', status: 'Active' }}>
          <Form.Item name="code" label="Code" rules={[{ required: true }, { pattern: /^[A-Z0-9][A-Z0-9_-]*$/, message: 'Upper-case letters, digits, - or _' }]}
            extra="Never changes once created.">
            <Input disabled={editing !== 'new'} />
          </Form.Item>
          <Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="kind" label="Kind" rules={[{ required: true }]}>
            <Select disabled={editing !== 'new'} options={[{ value: 'Provincial' }, { value: 'Municipal' }]} />
          </Form.Item>
          <Form.Item name="lguName" label="Local government on the letterhead"
            extra="Printed above the office name on its forms, e.g. the province or municipality. Blank prints none.">
            <Input />
          </Form.Item>
          <Form.Item name="headPosition" label="Head's position (as printed)"><Input /></Form.Item>
          <Form.Item name="sanggunianName" label="Sanggunian cited on its Tax Declarations"
            extra="E.g. the Sangguniang Panlalawigan or Bayan whose tax ordinance the TD note cites. Blank uses the deployment's setting.">
            <Input maxLength={200} />
          </Form.Item>
          <Form.Item name="address" label="Address" extra="Printed on the office's forms."><Input /></Form.Item>
          <Form.Item name="contact" label="Contact"><Input /></Form.Item>
          {editing !== 'new' && (
            <Form.Item name="status" label="Status"><Select options={[{ value: 'Active' }, { value: 'Inactive' }]} /></Form.Item>
          )}
        </Form>
      </Modal>
    </Space>
  );
}

function JurisdictionsTab() {
  const rows = useOfficeJurisdictions();
  const offices = useOffices();
  const provinces = useProvinces();
  const create = useCreateJurisdiction();
  const approve = useApproveJurisdiction();
  const { context, ok, fail } = useToast();
  const [adding, setAdding] = useState(false);
  const [provinceId, setProvinceId] = useState<string>();
  const municipalities = useMunicipalities(provinceId);
  const [form] = Form.useForm();

  const save = async () => {
    const v = await form.validateFields();
    create.mutate({ ...v, effectiveDate: day(v.effectiveDate) }, { onSuccess: () => { ok('Draft coverage created; a second user approves it.'); setAdding(false); }, onError: fail });
  };

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      {context}
      <Button icon={<PlusOutlined />} onClick={() => setAdding(true)}>Assign a municipality</Button>
      <Table<OfficeJurisdictionDto> rowKey="id" size="small" loading={rows.isLoading} dataSource={rows.data ?? []} pagination={{ pageSize: 20 }} scroll={{ x: true }}
        columns={[
          { title: 'Municipality', render: (_, j) => `${j.municipalityName} (${j.municipalityPsgcCode})` },
          { title: 'Office', dataIndex: 'officeCode' },
          { title: 'Period', render: (_, j) => period(j.effectiveDate, j.endDate) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          { title: 'Basis', dataIndex: 'legalBasis', ellipsis: true },
          {
            title: '', render: (_, j) => j.status === 'Draft'
              ? <Button size="small" loading={approve.isPending && approve.variables === j.id} onClick={() => approve.mutate(j.id, { onSuccess: () => ok('Approved.'), onError: fail })}>Approve</Button>
              : null,
          },
        ]} />
      <Modal title="Assign a municipality to an office" open={adding} onCancel={() => setAdding(false)} onOk={save} confirmLoading={create.isPending} okText="Create draft" destroyOnHidden>
        <Form form={form} layout="vertical">
          <Form.Item name="officeId" label="Municipal office" rules={[{ required: true }]}>
            <Select showSearch optionFilterProp="label"
              options={(offices.data ?? []).filter((o) => o.kind === 'Municipal' && o.status === 'Active').map((o) => ({ value: o.id, label: `${o.code} — ${o.name}` }))} />
          </Form.Item>
          <Form.Item label="Province">
            <Select showSearch optionFilterProp="label" value={provinceId} onChange={(v) => { setProvinceId(v); form.setFieldValue('municipalityId', undefined); }}
              options={(provinces.data ?? []).map((p) => ({ value: p.id, label: p.name }))} />
          </Form.Item>
          <Form.Item name="municipalityId" label="City / municipality" rules={[{ required: true }]}>
            <Select showSearch optionFilterProp="label" disabled={!provinceId} options={(municipalities.data ?? []).map((m) => ({ value: m.id, label: `${m.name} (${m.psgcCode})` }))} />
          </Form.Item>
          <Form.Item name="effectiveDate" label="Effective from" rules={[{ required: true }]}
            extra="Approving it ends the office that covered the municipality before, the day before this date.">
            <DatePicker style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="legalBasis" label="Basis (order or instrument)" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="remarks" label="Remarks"><Input.TextArea rows={2} /></Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}

function AssignmentsTab() {
  const rows = useOfficeAssignments();
  const offices = useOffices();
  const users = useUsers();
  const roles = useRoles();
  const create = useCreateAssignment();
  const approve = useApproveAssignment();
  const end = useEndAssignment();
  const { context, ok, fail } = useToast();
  const [adding, setAdding] = useState(false);
  const [ending, setEnding] = useState<OfficeAssignmentDto | null>(null);
  const [form] = Form.useForm();
  const [endForm] = Form.useForm();

  const save = async () => {
    const v = await form.validateFields();
    create.mutate({ ...v, officeId: v.officeId === 'province-wide' ? null : v.officeId, effectiveDate: day(v.effectiveDate) },
      { onSuccess: () => { ok('Draft assignment created; a second user approves it.'); setAdding(false); }, onError: fail });
  };
  const saveEnd = async () => {
    const v = await endForm.validateFields();
    if (ending) end.mutate({ id: ending.id, endDate: day(v.endDate), reason: v.reason }, { onSuccess: () => { ok('Assignment ended.'); setEnding(null); }, onError: fail });
  };

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      {context}
      <Alert type="info" showIcon title="A user holds one office at a time"
        description="Roles are held within an office. Only System Administrator and Auditor may be province-wide. Nobody approves their own assignment." />
      <Button icon={<PlusOutlined />} onClick={() => setAdding(true)}>Assign a user</Button>
      <Table<OfficeAssignmentDto> rowKey="id" size="small" loading={rows.isLoading} dataSource={rows.data ?? []} pagination={{ pageSize: 20 }} scroll={{ x: true }}
        columns={[
          { title: 'User', dataIndex: 'userName' },
          { title: 'Office', render: (_, a) => a.officeCode ?? <Tag color="blue">Province-wide</Tag> },
          { title: 'Roles', render: (_, a) => a.roles.map((r) => <Tag key={r}>{r}</Tag>) },
          { title: 'Period', render: (_, a) => period(a.effectiveDate, a.endDate) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          {
            title: '', render: (_, a) => (
              <Space>
                {a.status === 'Draft' && (
                  <Button size="small" loading={approve.isPending && approve.variables === a.id}
                    onClick={() => approve.mutate(a.id, { onSuccess: () => ok('Approved.'), onError: fail })}>Approve</Button>
                )}
                {a.status === 'Approved' && !a.endDate && <Button size="small" onClick={() => setEnding(a)}>End</Button>}
              </Space>
            ),
          },
        ]} />
      <Modal title="Assign a user to an office" open={adding} onCancel={() => setAdding(false)} onOk={save} confirmLoading={create.isPending} okText="Create draft" destroyOnHidden>
        <Form form={form} layout="vertical">
          <Form.Item name="appUserId" label="User" rules={[{ required: true }]}>
            <Select showSearch optionFilterProp="label" options={(users.data ?? []).map((u) => ({ value: u.id, label: u.email ? `${u.displayName} (${u.email})` : u.displayName }))} />
          </Form.Item>
          <Form.Item name="officeId" label="Office" rules={[{ required: true }]}>
            <Select showSearch optionFilterProp="label" options={[
              { value: 'province-wide', label: 'Province-wide (System Administrator, Auditor)' },
              ...(offices.data ?? []).filter((o) => o.status === 'Active').map((o) => ({ value: o.id, label: `${o.code} — ${o.name}` })),
            ]} />
          </Form.Item>
          <Form.Item name="roles" label="Roles" rules={[{ required: true }]}>
            <Select mode="multiple" options={(roles.data ?? []).map((r) => ({ value: r.code, label: r.name }))} />
          </Form.Item>
          <Form.Item name="effectiveDate" label="Effective from" rules={[{ required: true }]} extra="Approving it ends the user's previous assignment.">
            <DatePicker style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="legalBasis" label="Basis (appointment or designation)" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="remarks" label="Remarks"><Input.TextArea rows={2} /></Form.Item>
        </Form>
      </Modal>
      <Modal title={`End ${ending?.userName ?? ''}'s assignment`} open={ending !== null} onCancel={() => setEnding(null)} onOk={saveEnd} confirmLoading={end.isPending} okText="End" destroyOnHidden>
        <Form form={endForm} layout="vertical">
          <Form.Item name="endDate" label="Last day" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="reason" label="Reason" rules={[{ required: true }]}><Input /></Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}

/**
 * Each user's Real Estate Appraiser licence (LAM Bk I p.9; docs/analysis/records-and-forms.md Q10). It is printed with
 * their signature, frozen on each approval they sign; a step marked "licensed signatory" warns when it is missing or
 * expired, and does not block, until the province confirms the rule.
 */
function LicencesTab() {
  const users = useUsers();
  const update = useUpdateUserLicence();
  const { context, ok, fail } = useToast();
  const [editing, setEditing] = useState<UserSummaryDto | null>(null);
  const [form] = Form.useForm();
  const today = dayjs().format('YYYY-MM-DD');

  const save = async () => {
    if (!editing) return;
    const v = await form.validateFields();
    const number = v.reaLicenceNumber?.trim() || null;
    update.mutate({ id: editing.id, reaLicenceNumber: number, reaLicenceValidUntil: number ? day(v.reaLicenceValidUntil) : null, reason: v.reason },
      { onSuccess: () => { ok('Licence saved.'); setEditing(null); }, onError: fail });
  };

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      {context}
      <Table<UserSummaryDto> rowKey="id" size="small" loading={users.isLoading} dataSource={users.data ?? []} pagination={{ pageSize: 20 }} scroll={{ x: true }}
        columns={[
          { title: 'User', render: (_, u) => u.email ? `${u.displayName} (${u.email})` : u.displayName },
          { title: 'Office', render: (_, u) => u.provinceWide ? 'Province-wide' : u.officeCode ?? '—' },
          { title: 'REA licence', dataIndex: 'reaLicenceNumber', render: (v: string | null) => v ?? '—' },
          {
            title: 'Valid until', dataIndex: 'reaLicenceValidUntil',
            render: (v: string | null) => v ? <Space size={4}>{v}{v < today && <Tag color="red">expired</Tag>}</Space> : '—',
          },
          { title: '', render: (_, u) => <Button size="small" onClick={() => setEditing(u)}>Edit</Button> },
        ]} />
      <Modal title={editing ? `REA licence — ${editing.displayName}` : ''} open={editing !== null} onCancel={() => setEditing(null)} onOk={save}
        confirmLoading={update.isPending} okText="Save" destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false}
          initialValues={editing ? {
            reaLicenceNumber: editing.reaLicenceNumber, reaLicenceValidUntil: editing.reaLicenceValidUntil ? dayjs(editing.reaLicenceValidUntil) : undefined,
          } : undefined}>
          <Form.Item name="reaLicenceNumber" label="Licence number" rules={[{ max: 50 }]} extra="Blank clears the licence.">
            <Input />
          </Form.Item>
          <Form.Item noStyle dependencies={['reaLicenceNumber']}>
            {({ getFieldValue }) => (
              <Form.Item name="reaLicenceValidUntil" label="Valid until"
                rules={[{ required: !!getFieldValue('reaLicenceNumber')?.trim(), message: 'Give the validity with the number' }]}>
                <DatePicker disabled={!getFieldValue('reaLicenceNumber')?.trim()} />
              </Form.Item>
            )}
          </Form.Item>
          <Form.Item name="reason" label="Reason" rules={[{ required: true, message: 'A reason is required' }, { max: 1000 }]}
            extra="Kept in the audit log. Approvals already signed keep the licence they printed.">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
