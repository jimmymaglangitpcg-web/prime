import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  Alert, Button, Card, DatePicker, Descriptions, Form, Input, InputNumber, Modal, Select, Space, Spin, Table, Tag, Typography,
} from 'antd';
import dayjs from 'dayjs';
import {
  useAddSmvConsultation, useCancelSmvPreparation, useRecordSmvPreparationEvent, useSmvPreparation, useUpdateSmvPreparation,
} from '../../api/smvPreparations';
import { ApiRequestError } from '../../lib/apiClient';
import { SalesAnalysesCard, TimeFactorsCard } from './SalesAnalysisCards';
import { SubClassCriteriaCard } from './SubClassCriteriaCard';
import { PrintFormButton } from '../../components/PrintFormButton';
import {
  consultationModeLabel, eventKindLabel, nextKinds, preparationStatusColor, preparationStatusLabel, type SmvConsultationDto,
  type SmvConsultationMode, type SmvPreparationDto, type SmvPreparationEventDto, type SmvPreparationEventKind,
} from '../../lib/smvPreparationTypes';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);

/** One SMV preparation: its dates, the proposed SMV, consultations and the steps to certification and publication. */
export function SmvPreparationPage() {
  const { id = '' } = useParams();
  const { data: p, isLoading, error } = useSmvPreparation(id);
  const [warnings, setWarnings] = useState<string[]>([]);
  if (isLoading) return <Spin />;
  if (!p) return <Alert type="error" showIcon title="Could not load the preparation" description={error ? errorText(error) : undefined} />;
  const smv = p.proposedSmv;
  // The analysis is done before submission, or after a remand.
  const analysisOpen = p.editable && ['Preparing', 'PublishedForComment', 'Remanded'].includes(p.status);
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Space wrap>
          <Typography.Title level={3} style={{ margin: 0 }}>SMV {p.revisionYear} — {p.title}</Typography.Title>
          <Tag color={preparationStatusColor[p.status]}>{preparationStatusLabel[p.status]}</Tag>
        </Space>
        <Link to="/smv-preparation">All preparations</Link>
      </Space>
      {!p.editable && p.status !== 'Cancelled' && (
        <Typography.Text type="secondary">
          {['Certified', 'NotCertified', 'Published'].includes(p.status) ? 'The preparation is closed.' : 'Read only: the Provincial Assessor\'s Office prepares the SMV.'}
        </Typography.Text>
      )}
      {p.cancellationReason && <Alert type="warning" showIcon title="Cancelled" description={p.cancellationReason} />}
      {warnings.length > 0 && <Alert type="warning" showIcon closable onClose={() => setWarnings([])} title="Recorded, with warnings" description={<ul style={{ margin: 0 }}>{warnings.map((w) => <li key={w}>{w}</li>)}</ul>} />}
      {p.nextDue && (
        <Alert type={dayjs(p.nextDue.dueOn).isBefore(dayjs(), 'day') ? 'error' : 'info'} showIcon
          title={`${p.nextDue.what}: due ${p.nextDue.dueOn}`}
          description="A reminder from the statutory periods (settings); PRIME changes nothing when it passes." />
      )}
      <WorkFileCard p={p} />
      <Card size="small" title="Proposed SMV" extra={<Space><Link to="/admin/valuation">Rows (Valuation Rules)</Link><Link to="/smv-testing">Simulate &amp; test</Link></Space>}>
        <Descriptions size="small" bordered column={{ xs: 1, sm: 1, md: 2, lg: 2, xl: 2, xxl: 2 }} items={[
          { key: 'ref', label: 'Certification', children: smv.certificationReference ?? 'not yet certified' },
          { key: 'status', label: 'Status in PRIME', children: smv.status },
          { key: 'eff', label: 'Effectivity', children: smv.effectivityDate },
          { key: 'cov', label: 'Coverage', children: smv.coverage.length ? smv.coverage.join(', ') : 'The whole province' },
          { key: 'rows', label: 'Rows', children: smv.scheduleCount },
          { key: 'proposed', label: 'Proposed', children: smv.proposedOn ?? '—' },
          { key: 'pfc', label: 'Published for comment', children: smv.publishedForCommentOn ?? '—' },
          { key: 'cons', label: 'Latest consultation', children: smv.consultationsHeldOn ?? '—' },
          { key: 'sub', label: 'Submitted to the BLGF', children: smv.submittedToBlgfOn ?? '—' },
          { key: 'cert', label: 'Certified', children: smv.certifiedOn ?? '—' },
          { key: 'pub', label: 'Published', children: smv.publishedOn ? `${smv.publishedOn}${smv.publicationReference ? ` (${smv.publicationReference})` : ''}` : '—' },
        ]} />
        <Space wrap style={{ marginTop: 12 }}>
          <Typography.Text type="secondary">SMV forms:</Typography.Text>
          {[[1, 'Sub-class criteria'], [5, 'Land values'], [9, 'Agricultural land'], [10, 'Construction cost'], [11, 'Depreciation'], [12, 'Extra items']].map(([n, label]) => (
            <PrintFormButton key={n} formCode={`SMV_FORM_${n}`} subjectId={smv.id} issuable={!['Preparing', 'PublishedForComment', 'Remanded', 'Cancelled'].includes(p.status)}
              label={`Form ${n} · ${label}`} />
          ))}
        </Space>
      </Card>
      <SubClassCriteriaCard smvId={smv.id} editable={p.editable && smv.status === 'Draft'} />
      <TimeFactorsCard preparationId={p.id} editable={analysisOpen} />
      <SalesAnalysesCard preparationId={p.id} editable={analysisOpen} coverage={smv.coverage} />
      <ConsultationsCard p={p} />
      <StepsCard p={p} onWarnings={setWarnings} />
    </Space>
  );
}

function WorkFileCard({ p }: { p: SmvPreparationDto }) {
  const [editing, setEditing] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  return (
    <Card size="small" title="Work file" extra={p.editable && (
      <Space><Button size="small" onClick={() => setEditing(true)}>Edit</Button><Button size="small" danger onClick={() => setCancelling(true)}>Cancel preparation</Button></Space>
    )}>
      <Descriptions size="small" bordered column={1} items={[
        { key: 'dov', label: 'Date of valuation', children: p.dateOfValuation ?? '—' },
        { key: 'bvd', label: 'Base valuation date', children: p.baseValuationDate ? `${p.baseValuationDate}${p.status === 'Preparing' || p.status === 'PublishedForComment' ? ' (planned; becomes the date of submission)' : ''}` : '—' },
        { key: 'notes', label: 'Team notes', children: p.notes ? <span style={{ whiteSpace: 'pre-wrap' }}>{p.notes}</span> : '—' },
      ]} />
      {editing && <EditModal p={p} onClose={() => setEditing(false)} />}
      {cancelling && <CancelModal id={p.id} onClose={() => setCancelling(false)} />}
    </Card>
  );
}

function EditModal({ p, onClose }: { p: SmvPreparationDto; onClose: () => void }) {
  const update = useUpdateSmvPreparation(p.id);
  return (
    <Modal open title="Edit the work file" footer={null} width={640} destroyOnHidden onCancel={onClose}>
      {update.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not save" description={errorText(update.error)} />}
      <Form layout="vertical" initialValues={{
        title: p.title, dateOfValuation: p.dateOfValuation ? dayjs(p.dateOfValuation) : null, baseValuationDate: p.baseValuationDate ? dayjs(p.baseValuationDate) : null,
        plannedEffectivityDate: dayjs(p.proposedSmv.effectivityDate), notes: p.notes,
      }}
        onFinish={(v) => update.mutate({
          title: v.title, dateOfValuation: v.dateOfValuation?.format('YYYY-MM-DD') ?? null, baseValuationDate: v.baseValuationDate?.format('YYYY-MM-DD') ?? null,
          plannedEffectivityDate: v.plannedEffectivityDate.format('YYYY-MM-DD'), notes: v.notes ?? null,
        }, { onSuccess: onClose })}>
        <Form.Item name="title" label="Title" rules={[{ required: true }, { max: 300 }]}><Input /></Form.Item>
        <Space wrap>
          <Form.Item name="dateOfValuation" label="Date of valuation"><DatePicker /></Form.Item>
          <Form.Item name="baseValuationDate" label="Base valuation date"><DatePicker /></Form.Item>
          <Form.Item name="plannedEffectivityDate" label="Planned effectivity" rules={[{ required: true }]}><DatePicker /></Form.Item>
        </Space>
        <Form.Item name="notes" label="Team notes"><Input.TextArea rows={3} maxLength={4000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={update.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}

function CancelModal({ id, onClose }: { id: string; onClose: () => void }) {
  const cancel = useCancelSmvPreparation(id);
  return (
    <Modal open title="Cancel the preparation" footer={null} destroyOnHidden onCancel={onClose}>
      <Typography.Paragraph>The proposed SMV is cancelled with it; the revision year can then be prepared again.</Typography.Paragraph>
      {cancel.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not cancel" description={errorText(cancel.error)} />}
      <Form layout="vertical" onFinish={(v) => cancel.mutate({ reason: v.reason }, { onSuccess: onClose })}>
        <Form.Item name="reason" label="Reason" rules={[{ required: true }, { max: 1000 }]}><Input.TextArea rows={2} /></Form.Item>
        <Button danger htmlType="submit" loading={cancel.isPending}>Cancel preparation</Button>
      </Form>
    </Modal>
  );
}

function ConsultationsCard({ p }: { p: SmvPreparationDto }) {
  const [adding, setAdding] = useState(false);
  const canAdd = p.editable && ['Preparing', 'PublishedForComment', 'Remanded'].includes(p.status);
  const short = p.consultations.length < p.minimumConsultations;
  return (
    <Card size="small" title={`Public consultations (${p.consultations.length} of at least ${p.minimumConsultations})`}
      extra={canAdd && <Button size="small" onClick={() => setAdding(true)}>Record a consultation</Button>}>
      {short && p.status === 'Preparing' && <Typography.Paragraph type="warning">Fewer consultations than required before submission.</Typography.Paragraph>}
      <Table<SmvConsultationDto> rowKey="id" size="small" pagination={false} dataSource={p.consultations} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'None recorded' }}
        columns={[
          { title: 'Held on', dataIndex: 'heldOn' },
          { title: 'Mode', dataIndex: 'mode', render: (m: SmvConsultationMode) => consultationModeLabel[m] },
          { title: 'Venue or link', dataIndex: 'venue' },
          { title: 'Attendance', dataIndex: 'attendance', align: 'right' },
          { title: 'Minutes', dataIndex: 'minutesReference' },
          { title: 'Notes', dataIndex: 'notes' },
        ]} />
      {adding && <ConsultationModal id={p.id} onClose={() => setAdding(false)} />}
    </Card>
  );
}

function ConsultationModal({ id, onClose }: { id: string; onClose: () => void }) {
  const add = useAddSmvConsultation(id);
  return (
    <Modal open title="Record a public consultation" footer={null} destroyOnHidden onCancel={onClose}>
      {add.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not record" description={errorText(add.error)} />}
      <Form layout="vertical" initialValues={{ mode: 'InPerson' }}
        onFinish={(v) => add.mutate({
          heldOn: v.heldOn.format('YYYY-MM-DD'), mode: v.mode, venue: v.venue ?? null, attendance: v.attendance ?? null,
          minutesReference: v.minutesReference ?? null, notes: v.notes ?? null,
        }, { onSuccess: onClose })}>
        <Space wrap>
          <Form.Item name="heldOn" label="Held on" rules={[{ required: true }]}><DatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} /></Form.Item>
          <Form.Item name="mode" label="Mode" rules={[{ required: true }]}>
            <Select style={{ width: 160 }} options={(Object.keys(consultationModeLabel) as SmvConsultationMode[]).map((m) => ({ value: m, label: consultationModeLabel[m] }))} />
          </Form.Item>
          <Form.Item name="attendance" label="Attendance"><InputNumber<number> min={0} precision={0} /></Form.Item>
        </Space>
        <Form.Item name="venue" label="Venue or link"><Input maxLength={300} /></Form.Item>
        <Form.Item name="minutesReference" label="Minutes reference"><Input maxLength={200} /></Form.Item>
        <Form.Item name="notes" label="Notes"><Input.TextArea rows={2} maxLength={2000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={add.isPending}>Record</Button>
      </Form>
    </Modal>
  );
}

function StepsCard({ p, onWarnings }: { p: SmvPreparationDto; onWarnings: (w: string[]) => void }) {
  const [recording, setRecording] = useState(false);
  const allowed = p.editable || p.status === 'Certified' || p.status === 'Published' ? nextKinds[p.status] : [];
  return (
    <Card size="small" title="Review and certification"
      extra={allowed.length > 0 && !p.proposedSmv.status.startsWith('Cancel') && <Button size="small" type="primary" onClick={() => setRecording(true)}>Record a step</Button>}>
      <Table<SmvPreparationEventDto> rowKey="id" size="small" pagination={false} dataSource={p.events} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No step recorded yet' }}
        columns={[
          { title: 'Date', dataIndex: 'occurredOn' },
          { title: 'Step', dataIndex: 'kind', render: (k: SmvPreparationEventKind) => eventKindLabel[k] },
          { title: 'Reference', dataIndex: 'reference' },
          { title: 'Note / reasons', dataIndex: 'note', render: (v: string | null) => v && <span style={{ whiteSpace: 'pre-wrap' }}>{v}</span> },
        ]} />
      {recording && <StepModal p={p} kinds={allowed} onClose={(w) => { setRecording(false); if (w) onWarnings(w); }} />}
    </Card>
  );
}

function StepModal({ p, kinds, onClose }: { p: SmvPreparationDto; kinds: SmvPreparationEventKind[]; onClose: (warnings?: string[]) => void }) {
  const record = useRecordSmvPreparationEvent(p.id);
  const [kind, setKind] = useState<SmvPreparationEventKind>(kinds[0]);
  const needsReference = kind === 'Certified' || kind === 'Published';
  return (
    <Modal open title="Record a step" footer={null} width={640} destroyOnHidden onCancel={() => onClose()}>
      {record.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not record" description={errorText(record.error)} />}
      <Form layout="vertical" initialValues={{ kind: kinds[0] }}
        onFinish={(v) => record.mutate({ kind: v.kind, occurredOn: v.occurredOn.format('YYYY-MM-DD'), reference: v.reference ?? null, note: v.note ?? null },
          { onSuccess: (r) => onClose(r.warnings) })}>
        <Form.Item name="kind" label="Step" rules={[{ required: true }]}>
          <Select onChange={setKind} options={kinds.map((k) => ({ value: k, label: eventKindLabel[k] }))} />
        </Form.Item>
        <Form.Item name="occurredOn" label="Date" rules={[{ required: true }]}><DatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} /></Form.Item>
        <Form.Item name="reference" label={kind === 'Certified' ? 'Certification reference' : kind === 'Published' ? 'Where published' : 'Document reference'}
          rules={[{ required: needsReference }, { max: 200 }]}><Input /></Form.Item>
        <Form.Item name="note" label={kind === 'Remanded' ? 'Reasons for the remand' : 'Note'} rules={[{ required: kind === 'Remanded' }, { max: 4000 }]}>
          <Input.TextArea rows={3} />
        </Form.Item>
        {kind === 'SubmittedToRegionalOffice' && (
          <Typography.Paragraph type="secondary">The date of submission becomes the base valuation date.</Typography.Paragraph>
        )}
        {kind === 'Published' && (
          <Typography.Paragraph type="secondary">The SMV&apos;s effectivity is set from the date of publication; it can then be approved in PRIME by a second user.</Typography.Paragraph>
        )}
        <Button type="primary" htmlType="submit" loading={record.isPending}>Record</Button>
      </Form>
    </Modal>
  );
}
