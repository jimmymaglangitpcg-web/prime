import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import {
  Alert, Button, Card, Checkbox, DatePicker, Descriptions, Form, Input, Modal, Progress, Select, Space, Statistic, Table, Tag, Tooltip, Typography,
} from 'antd';
import dayjs from 'dayjs';
import {
  useAssignGeneralRevisionInspection, useCancelGeneralRevision, useExcludeGeneralRevisionItem, useIncludeGeneralRevisionItem, useGeneralRevision, useGeneralRevisionItems, useGeneralRevisionRunIssues,
  useLiftGeneralRevisionSuspension, useRecordGeneralRevisionInspection, useStartGeneralRevisionRun, useSuspendGeneralRevision,
  useUpdateGeneralRevisionReferences,
} from '../../api/generalRevision';
import { useUsers } from '../../api/offices';
import { GeneralRevisionCompletionCard } from './GeneralRevisionCompletionCard';
import { GeneralRevisionRecordsCard } from './GeneralRevisionRecordsCard';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import type { WorkflowStatus } from '../../lib/types';
import {
  batchModes, generalRevisionStatusColor, itemStatusColor, runModeLabel, suspensionKindLabel, type GeneralRevisionDto, type GeneralRevisionInspectionFilter,
  type GeneralRevisionItemDto, type GeneralRevisionItemStatus, type GeneralRevisionRunDto, type GeneralRevisionRunMode, type GeneralRevisionSuspensionKind,
} from '../../lib/generalRevisionTypes';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const workflowColor: Partial<Record<WorkflowStatus, string>> = { Draft: 'default', PendingReview: 'gold', Approved: 'blue', Posted: 'green', Rejected: 'red', Cancelled: 'red' };

/** What each batch action acts on, for its confirmation. */
const batchHelp: Partial<Record<GeneralRevisionRunMode, string>> = {
  Submit: 'Draft assessments are submitted for review.',
  Approve: 'You sign the next approval step of each assessment pending review, through the configured chain. Assessments you made are refused (maker-checker) and listed as issues.',
  Reject: 'Assessments pending review are returned with your reason; they can then be corrected and valued again.',
  Post: 'Approved assessments are posted in PIN order; each prepares its draft Tax Declaration, so TD numbers follow the tax map.',
  SubmitTaxDeclarations: 'The draft Tax Declarations declaring the posted assessments are submitted for review.',
  ApproveTaxDeclarations: 'You sign the next approval step of each of those Tax Declarations; on final approval it replaces the previous TD.',
};

/**
 * One general revision (docs/analysis/smv-preparation-general-revision.md §4.6): compile the units in scope, value them
 * as of the revision's effectivity under its SMV, field review, batch review, approval and posting, and the new TDs, with
 * every unit's old and new values, failures and progress.
 */
export function GeneralRevisionPage() {
  const { id = '' } = useParams();
  const { data: gr, isLoading } = useGeneralRevision(id);
  const run = useStartGeneralRevisionRun(id);
  const [modal, modalContext] = Modal.useModal();
  const [editing, setEditing] = useState(false);
  const [suspending, setSuspending] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  // Polling stops with the run; refresh everything once more (units, notices, gates) so the page shows what its last items did.
  const queryClient = useQueryClient();
  const wasActive = useRef(false);
  useEffect(() => {
    if (wasActive.current && !gr?.runActive) void queryClient.invalidateQueries({ queryKey: ['general-revision', id] });
    wasActive.current = !!gr?.runActive;
  }, [gr?.runActive, id, queryClient]);

  if (isLoading || !gr) return <Card loading />;
  const open = gr.status === 'Planned' || gr.status === 'InProgress';
  const pending = gr.itemsByStatus.Pending ?? 0;
  const failed = gr.itemsByStatus.Failed ?? 0;
  const startRun = (mode: 'Compile' | 'Value', title: string, content: string) =>
    modal.confirm({ title, content, onOk: () => run.mutateAsync({ mode, includeFailed: true }).catch(() => undefined) });

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      {modalContext}
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Space>
          <Link to="/general-revision">General Revision</Link>
          <Typography.Title level={3} style={{ margin: 0 }}>{gr.revisionYear} general revision</Typography.Title>
          <Tag color={generalRevisionStatusColor[gr.status]}>{gr.status}</Tag>
          {gr.suspended && <Tag color="red">Suspended</Tag>}
        </Space>
        {open && (
          <Space wrap>
            <Button disabled={gr.runActive || gr.suspended} onClick={() => startRun('Compile', 'Compile the units in scope?',
              'Every active unit with a current Tax Declaration in the scope that is not yet in this revision is added, with its posted assessment.')}>
              Compile units
            </Button>
            <Button type="primary" disabled={gr.runActive || gr.suspended || pending + failed === 0}
              onClick={() => startRun('Value', `Value ${pending + failed} unit(s)?`,
                `Each pending or failed unit is valued as of ${gr.effectiveDate} under SMV ${gr.smvReference} and a draft assessment is prepared. Units already in review are not touched.`)}>
              Value pending and failed ({pending + failed})
            </Button>
            <Button onClick={() => setSuspending(true)}>Suspend</Button>
            <Button onClick={() => setEditing(true)}>References</Button>
            <Button danger onClick={() => setCancelling(true)}>Cancel revision</Button>
          </Space>
        )}
      </Space>
      {run.isError && <Alert type="error" showIcon closable title="Could not start the run" description={errorText(run.error)} />}
      {gr.cancellationReason && <Alert type="error" showIcon title={`Cancelled: ${gr.cancellationReason}`} />}

      <Card>
        <Descriptions size="small" column={{ xs: 1, md: 3 }}>
          <Descriptions.Item label="Effective">{gr.effectiveDate}</Descriptions.Item>
          <Descriptions.Item label="SMV applied">{gr.smvReference}</Descriptions.Item>
          <Descriptions.Item label="Scope">{gr.scope.map((s) => s.municipalityName).join(', ')}</Descriptions.Item>
          <Descriptions.Item label="Office order">{gr.officeOrderReference ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Ordinance">{gr.ordinanceReference ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Description">{gr.description ?? '—'}</Descriptions.Item>
        </Descriptions>
        <Space wrap size="large" style={{ marginTop: 12 }}>
          <Statistic title="Units" value={gr.itemCount} />
          <Statistic title="Pending" value={pending} />
          <Statistic title="Assessed" value={gr.itemsByStatus.Assessed ?? 0} />
          <Statistic title="Failed" value={failed} styles={failed ? { content: { color: '#cf1322' } } : undefined} />
          <Statistic title="Market value — before" value={formatMoney(gr.previousMarketValue)} />
          <Statistic title="after" value={formatMoney(gr.newMarketValue)} />
          <Statistic title="Assessed value — before" value={formatMoney(gr.previousAssessedValue)} />
          <Statistic title="after" value={formatMoney(gr.newAssessedValue)} />
        </Space>
        {Object.keys(gr.assessmentsByStatus).length > 0 && (
          <div style={{ marginTop: 8 }}>
            <Typography.Text type="secondary">Assessments: </Typography.Text>
            {Object.entries(gr.assessmentsByStatus).map(([s, n]) => <Tag key={s} color={workflowColor[s as WorkflowStatus]}>{s}: {n}</Tag>)}
          </div>
        )}
      </Card>

      <RunsCard id={gr.id} runs={gr.runs} />
      {gr.suspensions.length > 0 && <SuspensionsCard gr={gr} />}
      <ItemsCard gr={gr} />
      <GeneralRevisionRecordsCard gr={gr} />
      <GeneralRevisionCompletionCard gr={gr} />

      {editing && <ReferencesModal gr={gr} onClose={() => setEditing(false)} />}
      {suspending && <SuspendModal id={gr.id} onClose={() => setSuspending(false)} />}
      {cancelling && <CancelModal id={gr.id} onClose={() => setCancelling(false)} />}
    </Space>
  );
}

function RunsCard({ id, runs }: { id: string; runs: GeneralRevisionRunDto[] }) {
  const [issuesOf, setIssuesOf] = useState<GeneralRevisionRunDto>();
  return (
    <Card title="Runs" size="small">
      {issuesOf && <IssuesModal id={id} run={issuesOf} onClose={() => setIssuesOf(undefined)} />}
      <Table<GeneralRevisionRunDto> rowKey="id" size="small" dataSource={runs} pagination={false} locale={{ emptyText: 'No run yet: compile the units in scope first' }}
        columns={[
          { title: 'Started', dataIndex: 'startedAt', render: (v: string | null) => (v ? dayjs(v).format('YYYY-MM-DD HH:mm') : '—') },
          { title: 'Run', dataIndex: 'mode', render: (m: GeneralRevisionRunMode | null, r) => <span>{m ? runModeLabel[m] : '—'}{r.reason && <><br /><Typography.Text type="secondary">{r.reason}</Typography.Text></>}</span> },
          {
            title: 'Progress', render: (_, r) => (
              <Progress size="small" style={{ width: 220 }} status={r.status === 'Failed' ? 'exception' : r.status === 'Completed' ? 'success' : 'active'}
                percent={r.totalCount ? Math.round((r.processedCount / r.totalCount) * 100) : r.status === 'Completed' ? 100 : 0}
                format={() => `${r.processedCount}/${r.totalCount}`} />
            ),
          },
          {
            title: 'Failed', dataIndex: 'failedCount', align: 'right',
            render: (n: number, r) => (r.issueCount > 0 ? <Button type="link" size="small" onClick={() => setIssuesOf(r)}>{n} — {r.issueCount} issue(s)</Button> : n),
          },
          { title: 'Status', dataIndex: 'status' },
          { title: 'Remarks', dataIndex: 'remarks' },
        ]} />
    </Card>
  );
}

function SuspensionsCard({ gr }: { gr: GeneralRevisionDto }) {
  const lift = useLiftGeneralRevisionSuspension(gr.id);
  return (
    <Card title="Suspensions" size="small">
      {lift.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not lift" description={errorText(lift.error)} />}
      <Table rowKey="id" size="small" dataSource={gr.suspensions} pagination={false}
        columns={[
          { title: 'Kind', dataIndex: 'kind', render: (k: GeneralRevisionSuspensionKind) => suspensionKindLabel[k] },
          { title: 'From', dataIndex: 'fromDate' },
          { title: 'Until', dataIndex: 'untilDate', render: (v: string | null) => v ?? 'until lifted' },
          { title: 'Reference', dataIndex: 'reference' },
          { title: 'Lifted', dataIndex: 'liftedOn', render: (v: string | null) => v ?? '—' },
          { title: '', render: (_, s) => (s.inForce ? <Tag color="red">In force</Tag> : null) },
          {
            title: '', render: (_, s) => !s.liftedOn && (
              <Button size="small" loading={lift.isPending} onClick={() => lift.mutate({ suspensionId: s.id, liftedOn: dayjs().format('YYYY-MM-DD') })}>Lift today</Button>
            ),
          },
        ]} />
    </Card>
  );
}

function ItemsCard({ gr }: { gr: GeneralRevisionDto }) {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<GeneralRevisionItemStatus>();
  const [assessmentStatus, setAssessmentStatus] = useState<WorkflowStatus>();
  const [taxDeclarationStatus, setTaxDeclarationStatus] = useState<WorkflowStatus>();
  const [inspection, setInspection] = useState<GeneralRevisionInspectionFilter>();
  const [search, setSearch] = useState('');
  const { data, isFetching } = useGeneralRevisionItems(gr.id,
    { status, assessmentStatus, taxDeclarationStatus, inspection, search: search || undefined, page, pageSize: 25 }, gr.runActive);
  const run = useStartGeneralRevisionRun(gr.id);
  const [selected, setSelected] = useState<string[]>([]);
  const [batch, setBatch] = useState<GeneralRevisionRunMode>();
  const [assigning, setAssigning] = useState(false);
  const [inspecting, setInspecting] = useState<GeneralRevisionItemDto>();
  const [excluding, setExcluding] = useState<GeneralRevisionItemDto>();
  const include = useIncludeGeneralRevisionItem(gr.id);
  const open = gr.status === 'Planned' || gr.status === 'InProgress';
  const busy = gr.runActive || gr.suspended;
  const money = (v: number | null) => (v === null ? '—' : formatMoney(v));
  return (
    <Card title="Units (in tax-map order)" size="small">
      <Space wrap style={{ marginBottom: 12 }}>
        <Input.Search allowClear placeholder="PIN or unit number" style={{ width: 260 }} onSearch={(v) => { setSearch(v); setPage(1); }} />
        <Select allowClear placeholder="Run outcome" style={{ width: 150 }} value={status} onChange={(v) => { setStatus(v); setPage(1); }}
          options={(['Pending', 'Assessed', 'Failed', 'Excluded'] as GeneralRevisionItemStatus[]).map((s) => ({ value: s, label: s }))} />
        <Select allowClear placeholder="Assessment" style={{ width: 170 }} value={assessmentStatus} onChange={(v) => { setAssessmentStatus(v); setPage(1); }}
          options={(['Draft', 'PendingReview', 'Approved', 'Posted', 'Rejected'] as WorkflowStatus[]).map((s) => ({ value: s, label: s }))} />
        <Select allowClear placeholder="Tax Declaration" style={{ width: 170 }} value={taxDeclarationStatus} onChange={(v) => { setTaxDeclarationStatus(v); setPage(1); }}
          options={(['Draft', 'PendingReview', 'Approved'] as WorkflowStatus[]).map((s) => ({ value: s, label: s }))} />
        <Select allowClear placeholder="Field review" style={{ width: 170 }} value={inspection} onChange={(v) => { setInspection(v); setPage(1); }}
          options={[{ value: 'Unassigned', label: 'Not assigned' }, { value: 'Awaiting', label: 'Awaiting inspection' }, { value: 'Inspected', label: 'Inspected' }]} />
      </Space>
      {open && (
        <Space wrap style={{ marginBottom: 12 }}>
          <Button disabled={selected.length === 0 || busy} loading={run.isPending}
            onClick={() => run.mutate({ mode: 'Value', itemIds: selected }, { onSuccess: () => setSelected([]) })}>
            Value selected again ({selected.length})
          </Button>
          <Button disabled={selected.length === 0} onClick={() => setAssigning(true)}>Assign inspection ({selected.length})</Button>
          <Typography.Text type="secondary">Batch, {selected.length ? `on the ${selected.length} selected` : 'on every unit in that state'}:</Typography.Text>
          {batchModes.map((m) => (
            <Button key={m} disabled={busy} danger={m === 'Reject'} onClick={() => setBatch(m)}>{runModeLabel[m]}</Button>
          ))}
        </Space>
      )}
      {batch && (
        <BatchModal mode={batch} selected={selected} gr={gr} onClose={(started) => { setBatch(undefined); if (started) setSelected([]); }} />
      )}
      {assigning && <AssignModal id={gr.id} itemIds={selected} onClose={(done) => { setAssigning(false); if (done) setSelected([]); }} />}
      {inspecting && <InspectionModal id={gr.id} item={inspecting} onClose={() => setInspecting(undefined)} />}
      {excluding && <ExcludeModal id={gr.id} item={excluding} onClose={() => setExcluding(undefined)} />}
      {include.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not put the unit back" description={errorText(include.error)} />}
      {run.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not start the run" description={errorText(run.error)} />}
      <Table<GeneralRevisionItemDto> rowKey="id" size="small" loading={isFetching} dataSource={data?.items ?? []} scroll={{ x: 'max-content' }}
        rowSelection={open ? { selectedRowKeys: selected, preserveSelectedRowKeys: true, onChange: (k) => setSelected(k as string[]) } : undefined}
        pagination={{ current: page, pageSize: 25, total: data?.totalCount ?? 0, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: 'PIN', dataIndex: 'pin', render: (v: string, r) => <Link to={`/properties/${r.propertyId}`}>{v}</Link> },
          { title: 'Unit', render: (_, r) => <span>{r.rpuNumber}<br /><Typography.Text type="secondary">{r.rpuType}</Typography.Text></span> },
          { title: 'Barangay', dataIndex: 'barangayName' },
          { title: 'MV before', dataIndex: 'previousMarketValue', align: 'right', render: money },
          { title: 'MV after', dataIndex: 'newMarketValue', align: 'right', render: money },
          { title: 'AV before', dataIndex: 'previousAssessedValue', align: 'right', render: money },
          { title: 'AV after', dataIndex: 'newAssessedValue', align: 'right', render: money },
          {
            title: 'AV change', dataIndex: 'assessedValueChange', align: 'right',
            render: (v: number | null) => (v === null ? '—' : <Typography.Text type={v > 0 ? 'danger' : v < 0 ? 'success' : undefined}>{v > 0 ? '+' : ''}{formatMoney(v)}</Typography.Text>),
          },
          {
            title: 'Outcome', render: (_, r) => (
              <Space orientation="vertical" size={0}>
                <Tooltip title={r.failureReason ?? r.exclusionReason}>
                  <Tag color={itemStatusColor[r.status]}>{r.status}</Tag>
                  {r.failureReason && <Typography.Text type="danger" style={{ fontSize: 12 }}>{r.failureReason.length > 60 ? `${r.failureReason.slice(0, 60)}…` : r.failureReason}</Typography.Text>}
                  {r.exclusionReason && <Typography.Text type="secondary" style={{ fontSize: 12 }}>{r.exclusionReason}</Typography.Text>}
                </Tooltip>
                {open && (r.status === 'Excluded'
                  ? <Button size="small" type="link" style={{ padding: 0 }} loading={include.isPending} onClick={() => include.mutate(r.id)}>Put back</Button>
                  : (!r.assessmentStatus || ['Draft', 'Rejected', 'Cancelled'].includes(r.assessmentStatus))
                    && <Button size="small" type="link" danger style={{ padding: 0 }} onClick={() => setExcluding(r)}>Take out</Button>)}
              </Space>
            ),
          },
          { title: 'Assessment', dataIndex: 'assessmentStatus', render: (s: WorkflowStatus | null) => (s ? <Tag color={workflowColor[s]}>{s}</Tag> : '—') },
          {
            title: 'Tax Declaration', render: (_, r) => (r.taxDeclarationStatus
              ? <span>{r.taxDeclarationNumber}<br /><Tag color={workflowColor[r.taxDeclarationStatus]}>{r.taxDeclarationStatus}</Tag></span> : '—'),
          },
          {
            title: 'Field review', render: (_, r) => (
              <Space orientation="vertical" size={0}>
                {r.inspectorName && <span>{r.inspectorName}{r.inspectionRoute && <Typography.Text type="secondary"> · {r.inspectionRoute}</Typography.Text>}</span>}
                {r.inspectedOn && (
                  <Tooltip title={r.inspectionNotes}>
                    <Typography.Text type={r.inspectionFoundChanges ? 'warning' : 'secondary'}>
                      Inspected {r.inspectedOn}{r.inspectionFoundChanges ? ' — changes' : ''}
                    </Typography.Text>
                  </Tooltip>
                )}
                {open && <Button size="small" type="link" style={{ padding: 0 }} onClick={() => setInspecting(r)}>Record inspection</Button>}
              </Space>
            ),
          },
        ]} />
    </Card>
  );
}

function ReferencesModal({ gr, onClose }: { gr: GeneralRevisionDto; onClose: () => void }) {
  const save = useUpdateGeneralRevisionReferences(gr.id);
  return (
    <Modal open title="References" footer={null} destroyOnHidden onCancel={onClose}>
      {save.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not save" description={errorText(save.error)} />}
      <Form layout="vertical" initialValues={gr}
        onFinish={(v) => save.mutate({ officeOrderReference: v.officeOrderReference ?? null, ordinanceReference: v.ordinanceReference ?? null, description: v.description ?? null },
          { onSuccess: onClose })}>
        <Form.Item name="officeOrderReference" label="Office order of the LCE (GRI 1)"><Input maxLength={200} /></Form.Item>
        <Form.Item name="ordinanceReference" label="Ordinance"><Input maxLength={200} /></Form.Item>
        <Form.Item name="description" label="Description"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={save.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}

function SuspendModal({ id, onClose }: { id: string; onClose: () => void }) {
  const suspend = useSuspendGeneralRevision(id);
  const [form] = Form.useForm();
  const kind = Form.useWatch('kind', form) as GeneralRevisionSuspensionKind | undefined;
  return (
    <Modal open title="Suspend the general revision" footer={null} destroyOnHidden onCancel={onClose}>
      <Typography.Paragraph type="secondary">
        No run starts while a suspension is in force (LAM Book IV p.125). A local calamity suspends for the configured period; an extension adds another
        period after it; a national emergency lasts until lifted.
      </Typography.Paragraph>
      {suspend.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not suspend" description={errorText(suspend.error)} />}
      <Form form={form} layout="vertical" initialValues={{ kind: 'LocalCalamity', fromDate: dayjs() }}
        onFinish={(v) => suspend.mutate({ kind: v.kind, fromDate: v.fromDate.format('YYYY-MM-DD'), reference: v.reference, remarks: v.remarks ?? null }, { onSuccess: onClose })}>
        <Form.Item name="kind" label="Ground" rules={[{ required: true }]}>
          <Select options={(Object.keys(suspensionKindLabel) as GeneralRevisionSuspensionKind[]).map((k) => ({ value: k, label: suspensionKindLabel[k] }))} />
        </Form.Item>
        {kind !== 'Extension' && <Form.Item name="fromDate" label="From" rules={[{ required: true }]}><DatePicker /></Form.Item>}
        <Form.Item name="reference" label={kind === 'Extension' ? "Secretary of Finance's approval" : 'Declaration'} rules={[{ required: true }, { max: 300 }]}>
          <Input />
        </Form.Item>
        <Form.Item name="remarks" label="Remarks"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={suspend.isPending}>Suspend</Button>
      </Form>
    </Modal>
  );
}

function CancelModal({ id, onClose }: { id: string; onClose: () => void }) {
  const cancel = useCancelGeneralRevision(id);
  const [reason, setReason] = useState('');
  return (
    <Modal open title="Cancel the general revision" okText="Cancel revision" cancelText="Back" okButtonProps={{ danger: true, disabled: !reason.trim(), loading: cancel.isPending }}
      onCancel={onClose} onOk={() => cancel.mutate(reason.trim(), { onSuccess: onClose })} destroyOnHidden>
      <Typography.Paragraph type="secondary">Possible only before any of its assessments goes for review; its drafts are cancelled with it.</Typography.Paragraph>
      {cancel.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not cancel" description={errorText(cancel.error)} />}
      <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
    </Modal>
  );
}

function BatchModal({ mode, selected, gr, onClose }: { mode: GeneralRevisionRunMode; selected: string[]; gr: GeneralRevisionDto; onClose: (started: boolean) => void }) {
  const run = useStartGeneralRevisionRun(gr.id);
  const [reason, setReason] = useState('');
  const needsReason = mode === 'Reject';
  return (
    <Modal open title={`${runModeLabel[mode]} — ${selected.length ? `${selected.length} selected unit(s)` : 'every unit in that state'}`}
      okText="Start" okButtonProps={{ danger: needsReason, disabled: needsReason && !reason.trim(), loading: run.isPending }}
      onCancel={() => onClose(false)} destroyOnHidden
      onOk={() => run.mutate({ mode, itemIds: selected.length ? selected : undefined, reason: needsReason ? reason.trim() : undefined },
        { onSuccess: () => onClose(true) })}>
      <Typography.Paragraph>{batchHelp[mode]}</Typography.Paragraph>
      <Typography.Paragraph type="secondary">
        Runs in the background, unit by unit in PIN order, as you. Units not in the state this action needs are left out; a unit the action refuses
        is listed in the run&apos;s issues and stays as it was.
      </Typography.Paragraph>
      {needsReason && <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />}
      {run.isError && <Alert type="error" showIcon style={{ marginTop: 12 }} title="Could not start" description={errorText(run.error)} />}
    </Modal>
  );
}

function IssuesModal({ id, run, onClose }: { id: string; run: GeneralRevisionRunDto; onClose: () => void }) {
  const { data, isLoading } = useGeneralRevisionRunIssues(id, run.id);
  return (
    <Modal open width={900} title={`Issues — ${run.mode ? runModeLabel[run.mode] : ''} run`} footer={null} onCancel={onClose} destroyOnHidden>
      <Table rowKey={(r) => `${r.itemId}-${r.code}`} size="small" loading={isLoading} dataSource={data ?? []} pagination={{ pageSize: 20 }}
        columns={[
          { title: 'PIN', dataIndex: 'pin' },
          { title: 'Unit', dataIndex: 'rpuNumber' },
          { title: '', dataIndex: 'failed', render: (f: boolean) => (f ? <Tag color="red">Refused</Tag> : <Tag color="gold">Note</Tag>) },
          { title: 'Code', dataIndex: 'code' },
          { title: 'Why', dataIndex: 'message' },
        ]} />
    </Modal>
  );
}

function AssignModal({ id, itemIds, onClose }: { id: string; itemIds: string[]; onClose: (done: boolean) => void }) {
  const assign = useAssignGeneralRevisionInspection(id);
  const { data: users } = useUsers();
  return (
    <Modal open title={`Assign ${itemIds.length} unit(s) for field review`} footer={null} destroyOnHidden onCancel={() => onClose(false)}>
      <Typography.Paragraph type="secondary">The appraiser who inspects the units on the ground (GRI 9–10). Leave the inspector empty to clear the assignment.</Typography.Paragraph>
      {assign.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not assign" description={errorText(assign.error)} />}
      <Form layout="vertical" onFinish={(v) => assign.mutate({ itemIds, inspectorId: v.inspectorId ?? null, route: v.route?.trim() || null }, { onSuccess: () => onClose(true) })}>
        <Form.Item name="inspectorId" label="Inspector">
          <Select allowClear showSearch optionFilterProp="label" placeholder="Appraiser"
            options={(users ?? []).filter((u) => u.status === 'Active').map((u) => ({ value: u.id, label: `${u.displayName}${u.officeCode ? ` (${u.officeCode})` : ''}` }))} />
        </Form.Item>
        <Form.Item name="route" label="Route / assignment"><Input maxLength={100} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={assign.isPending}>Assign</Button>
      </Form>
    </Modal>
  );
}

function InspectionModal({ id, item, onClose }: { id: string; item: GeneralRevisionItemDto; onClose: () => void }) {
  const record = useRecordGeneralRevisionInspection(id);
  return (
    <Modal open title={`Inspection — ${item.pin} ${item.rpuNumber}`} footer={null} destroyOnHidden onCancel={onClose}>
      <Typography.Paragraph type="secondary">
        Make the corrections found (class, use, owners, improvements) through the property&apos;s own screens. If the inspection found changes, the unit
        goes back to Pending and the next value run values it again.
      </Typography.Paragraph>
      {record.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not record" description={errorText(record.error)} />}
      <Form layout="vertical"
        initialValues={{ inspectedOn: item.inspectedOn ? dayjs(item.inspectedOn) : dayjs(), notes: item.inspectionNotes, foundChanges: item.inspectionFoundChanges ?? false }}
        onFinish={(v) => record.mutate({ itemId: item.id, inspectedOn: v.inspectedOn.format('YYYY-MM-DD'), notes: v.notes?.trim() || null, foundChanges: !!v.foundChanges },
          { onSuccess: onClose })}>
        <Form.Item name="inspectedOn" label="Inspected on" rules={[{ required: true }]}><DatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} /></Form.Item>
        <Form.Item name="notes" label="Findings"><Input.TextArea rows={3} maxLength={2000} /></Form.Item>
        <Form.Item name="foundChanges" valuePropName="checked"><Checkbox>Changes found: value the unit again</Checkbox></Form.Item>
        <Button type="primary" htmlType="submit" loading={record.isPending}>Record</Button>
      </Form>
    </Modal>
  );
}

function ExcludeModal({ id, item, onClose }: { id: string; item: GeneralRevisionItemDto; onClose: () => void }) {
  const exclude = useExcludeGeneralRevisionItem(id);
  const [reason, setReason] = useState('');
  return (
    <Modal open title={`Take ${item.pin} ${item.rpuNumber} out of the revision`} okText="Take out" okButtonProps={{ danger: true, disabled: !reason.trim(), loading: exclude.isPending }}
      onCancel={onClose} onOk={() => exclude.mutate({ itemId: item.id, reason: reason.trim() }, { onSuccess: onClose })} destroyOnHidden>
      <Typography.Paragraph type="secondary">
        For a unit the revision should not value: retired since it was compiled, or appraised outside the revision. Its draft assessment is cancelled;
        it is not counted by the gates or the roll, and the completion report lists it with the reason. It can be put back.
      </Typography.Paragraph>
      {exclude.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not take it out" description={errorText(exclude.error)} />}
      <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
    </Modal>
  );
}
