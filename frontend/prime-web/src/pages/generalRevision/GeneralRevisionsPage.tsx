import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Button, Card, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  useApproveChecklistStepDefinition, useChecklistStepDefinitions, useCreateChecklistStepDefinition, useCreateGeneralRevision, useGeneralRevisions,
} from '../../api/generalRevision';
import { useSmvs } from '../../api/valuation';
import { useAllMunicipalities } from '../../api/referenceData';
import { ApiRequestError } from '../../lib/apiClient';
import {
  gateLabel, generalRevisionStatusColor, type ChecklistStepDefinitionDto, type GeneralRevisionGate, type GeneralRevisionStatus, type GeneralRevisionSummaryDto,
} from '../../lib/generalRevisionTypes';
import type { WorkflowStatus } from '../../lib/types';
import { useCan } from '../../api/offices';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);

/**
 * General revisions of assessments (LGC §219; RA 12001 §19; LAM 2025 Book IV Ch. IV;
 * docs/analysis/smv-preparation-general-revision.md §4.6): one programme per revision year and scope, on a certified SMV.
 */
export function GeneralRevisionsPage() {
  const navigate = useNavigate();
  const { data = [], isLoading } = useGeneralRevisions();
  const [creating, setCreating] = useState(false);
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Typography.Title level={3} style={{ margin: 0 }}>General Revision</Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>New general revision</Button>
      </Space>
      <Card>
        <Table<GeneralRevisionSummaryDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
          locale={{ emptyText: 'No general revision yet' }}
          onRow={(r) => ({ onClick: () => navigate(`/general-revision/${r.id}`), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Revision year', dataIndex: 'revisionYear' },
            { title: 'Effective', dataIndex: 'effectiveDate' },
            { title: 'SMV', dataIndex: 'smvReference' },
            { title: 'Scope', dataIndex: 'scope' },
            { title: 'Units', dataIndex: 'itemCount', align: 'right' },
            { title: 'Status', dataIndex: 'status', render: (s: GeneralRevisionStatus) => <Tag color={generalRevisionStatusColor[s]}>{s}</Tag> },
          ]} />
      </Card>
      <ChecklistTemplateCard />
      {creating && <CreateModal onClose={(id) => { setCreating(false); if (id) navigate(`/general-revision/${id}`); }} />}
    </Space>
  );
}

function CreateModal({ onClose }: { onClose: (id?: string) => void }) {
  const create = useCreateGeneralRevision();
  const { data: smvs } = useSmvs();
  const { data: municipalities = [] } = useAllMunicipalities();
  // A general revision applies the SMV itself; its amendments apply through it (smv-preparation-general-revision.md §4.7).
  const approved = (smvs?.items ?? []).filter((s) => s.status === 'Approved' && s.basis !== 'Amendment');
  return (
    <Modal open title="New general revision" footer={null} width={720} destroyOnHidden onCancel={() => onClose()}>
      <Typography.Paragraph type="secondary">
        The revision applies a certified SMV, approved in PRIME and in force on the revision's effective date, to every unit in its scope (GRI 2).
      </Typography.Paragraph>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not create" description={errorText(create.error)} />}
      <Form layout="vertical" initialValues={{ revisionYear: dayjs().year() + 1, effectiveDate: dayjs().add(1, 'year').startOf('year') }}
        onFinish={(v) => create.mutate({
          revisionYear: v.revisionYear, effectiveDate: v.effectiveDate.format('YYYY-MM-DD'), smvId: v.smvId, municipalityIds: v.municipalityIds,
          officeOrderReference: v.officeOrderReference ?? null, ordinanceReference: v.ordinanceReference ?? null, description: v.description ?? null,
        }, { onSuccess: (r) => onClose(r.id) })}>
        <Space wrap>
          <Form.Item name="revisionYear" label="Revision year" rules={[{ required: true }]}><InputNumber<number> min={1990} max={2200} precision={0} /></Form.Item>
          <Form.Item name="effectiveDate" label="Effective date" rules={[{ required: true }]}><DatePicker /></Form.Item>
          <Form.Item name="smvId" label="SMV applied" rules={[{ required: true }]}>
            <Select showSearch optionFilterProp="label" style={{ width: 320 }} placeholder="Approved SMV" notFoundContent="No approved SMV"
              options={approved.map((s) => ({ value: s.id, label: `${s.reference} — effective ${s.effectivityDate}${s.coverage.length ? ` (${s.coverage.map((c) => c.municipalityName).join(', ')})` : ''}` }))} />
          </Form.Item>
        </Space>
        <Form.Item name="municipalityIds" label="Scope (cities/municipalities)" rules={[{ required: true, type: 'array', min: 1 }]}>
          <Select mode="multiple" showSearch optionFilterProp="label" options={municipalities.map((m) => ({ value: m.id, label: m.name }))} />
        </Form.Item>
        <Space wrap>
          <Form.Item name="officeOrderReference" label="Office order of the LCE (GRI 1)"><Input maxLength={200} style={{ width: 260 }} /></Form.Item>
          <Form.Item name="ordinanceReference" label="Ordinance"><Input maxLength={200} style={{ width: 260 }} /></Form.Item>
        </Space>
        <Form.Item name="description" label="Description"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={create.isPending}>Create</Button>
      </Form>
    </Modal>
  );
}

const statusColor: Partial<Record<WorkflowStatus, string>> = { Draft: 'default', Approved: 'green', Cancelled: 'red' };

/**
 * The general revision instructions' checklist template (configuration; Q13): normally loaded from the office's content pack,
 * approved here by a second user. A step naming a condition is checked by PRIME.
 */
function ChecklistTemplateCard() {
  const { data = [], isLoading } = useChecklistStepDefinitions();
  const approve = useApproveChecklistStepDefinition();
  const can = useCan();
  const [adding, setAdding] = useState(false);
  return (
    <Card title="Checklist template (general revision instructions)" size="small"
      extra={<Button size="small" onClick={() => setAdding(true)}>Add a step</Button>}>
      <Typography.Paragraph type="secondary">
        Loaded as content from the office&apos;s content pack (the instructions&apos; own text is never part of PRIME), or added here; a second user approves
        each version. A revision copies the steps in force when its checklist is loaded.
      </Typography.Paragraph>
      {approve.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not approve" description={errorText(approve.error)} />}
      <Table<ChecklistStepDefinitionDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={{ pageSize: 20 }} scroll={{ x: true }}
        locale={{ emptyText: 'No checklist step configured' }}
        columns={[
          { title: '#', dataIndex: 'sequence', align: 'right' },
          { title: 'Code', dataIndex: 'code' },
          { title: 'Title', dataIndex: 'title' },
          { title: 'Checked by', dataIndex: 'gate', render: (g: GeneralRevisionGate | null) => (g ? <Tag>PRIME: {gateLabel[g]}</Tag> : 'The office') },
          { title: 'From', dataIndex: 'effectiveDate' },
          { title: 'To', dataIndex: 'endDate', render: (v: string | null) => v ?? '—' },
          { title: 'Basis', dataIndex: 'legalBasis' },
          { title: 'Status', dataIndex: 'status', render: (s: WorkflowStatus) => <Tag color={statusColor[s]}>{s}</Tag> },
          {
            title: '', render: (_, r) => r.status === 'Draft' && can('config.approve') && (
              <Button size="small" loading={approve.isPending} onClick={() => approve.mutate(r.id)}>Approve</Button>
            ),
          },
        ]} />
      {adding && <AddStepModal onClose={() => setAdding(false)} />}
    </Card>
  );
}

function AddStepModal({ onClose }: { onClose: () => void }) {
  const create = useCreateChecklistStepDefinition();
  return (
    <Modal open title="Add a checklist step (draft)" footer={null} destroyOnHidden onCancel={onClose}>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not add" description={errorText(create.error)} />}
      <Form layout="vertical" initialValues={{ effectiveDate: dayjs() }}
        onFinish={(v) => create.mutate({
          legalBasis: v.legalBasis, effectiveDate: v.effectiveDate.format('YYYY-MM-DD'), remarks: v.remarks ?? null, code: v.code, sequence: v.sequence,
          title: v.title, description: v.description ?? null, gate: v.gate ?? null,
        }, { onSuccess: onClose })}>
        <Space wrap>
          <Form.Item name="code" label="Code" rules={[{ required: true }, { max: 50 }]}><Input /></Form.Item>
          <Form.Item name="sequence" label="Order" rules={[{ required: true }]}><InputNumber<number> min={1} precision={0} /></Form.Item>
          <Form.Item name="effectiveDate" label="In force from" rules={[{ required: true }]}><DatePicker /></Form.Item>
        </Space>
        <Form.Item name="title" label="Title" rules={[{ required: true }, { max: 300 }]}><Input /></Form.Item>
        <Form.Item name="description" label="Description"><Input.TextArea rows={2} maxLength={2000} /></Form.Item>
        <Form.Item name="gate" label="Checked by PRIME (optional)">
          <Select allowClear options={(Object.keys(gateLabel) as GeneralRevisionGate[]).map((g) => ({ value: g, label: gateLabel[g] }))} />
        </Form.Item>
        <Form.Item name="legalBasis" label="Basis" rules={[{ required: true }, { max: 500 }]}><Input /></Form.Item>
        <Button type="primary" htmlType="submit" loading={create.isPending}>Add as draft</Button>
      </Form>
    </Modal>
  );
}
