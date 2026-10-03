import { useState } from 'react';
import { Alert, Button, Card, DatePicker, Form, Input, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import dayjs from 'dayjs';
import {
  useCreateGeneralRevisionRegisterRuns, useGeneralRevisionNotices, useGeneralRevisionRegisterRuns, useRecordGeneralRevisionNoticeService,
  useRollGates, useStartGeneralRevisionRun,
} from '../../api/generalRevision';
import { PrintFormButton } from '../../components/PrintFormButton';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import {
  noticeServiceModeLabel, registerKindLabel, type GeneralRevisionDto, type GeneralRevisionNoticeDto, type GeneralRevisionRegisterKind,
  type GeneralRevisionRegisterRunDto, type NoticeServiceMode, type NoticeServiceResultDto, type NoticeStatus, type RollGateDto,
} from '../../lib/generalRevisionTypes';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const noticeColor: Record<NoticeStatus, string> = { Draft: 'default', Issued: 'blue', Served: 'green', Cancelled: 'red' };

/**
 * The general revision's notices and records (docs/analysis/smv-preparation-general-revision.md §4.6, L6-6c): notices of
 * assessment in bulk (LGC §223), their service, the roll gate (GRI 17; Q15) and the revision's register runs.
 */
export function GeneralRevisionRecordsCard({ gr }: { gr: GeneralRevisionDto }) {
  const open = gr.status === 'Planned' || gr.status === 'InProgress';
  return (
    <Card title="Notices and records" size="small">
      <Space orientation="vertical" size="large" style={{ width: '100%' }}>
        <NoticesSection gr={gr} open={open} />
        <RollGatesSection gr={gr} />
        <RegisterRunsSection gr={gr} open={open} />
      </Space>
    </Card>
  );
}

function NoticesSection({ gr, open }: { gr: GeneralRevisionDto; open: boolean }) {
  const [status, setStatus] = useState<NoticeStatus>();
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string[]>([]);
  const [serving, setServing] = useState(false);
  const { data, isFetching } = useGeneralRevisionNotices(gr.id, { status, page, pageSize: 20 });
  const run = useStartGeneralRevisionRun(gr.id);
  const [modal, modalContext] = Modal.useModal();
  const busy = gr.runActive || gr.suspended;
  const start = (mode: 'GenerateNotices' | 'IssueNotices', title: string, content: string) =>
    modal.confirm({ title, content, onOk: () => run.mutateAsync({ mode }).catch(() => undefined) });
  return (
    <div>
      {modalContext}
      <Typography.Title level={5}>Notices of assessment</Typography.Title>
      <Space wrap style={{ marginBottom: 8 }}>
        {open && (
          <>
            <Button disabled={busy} onClick={() => start('GenerateNotices', 'Generate the notices?',
              'Each posted unit whose assessed value changed, or that is assessed for the first time, gets a draft notice (LGC §223): one combined notice per sole declared owner, else one per unit.')}>
              Generate notices
            </Button>
            <Button disabled={busy} onClick={() => start('IssueNotices', 'Issue the draft notices?', 'Each draft notice is issued and numbered, in PIN order.')}>
              Issue notices
            </Button>
            <Button disabled={selected.length === 0} onClick={() => setServing(true)}>Record service ({selected.length})</Button>
          </>
        )}
        <Select allowClear placeholder="Status" style={{ width: 150 }} value={status} onChange={(v) => { setStatus(v); setPage(1); setSelected([]); }}
          options={(['Draft', 'Issued', 'Served'] as NoticeStatus[]).map((s) => ({ value: s, label: s === 'Issued' ? 'Issued, not served' : s }))} />
      </Space>
      {run.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not start" description={errorText(run.error)} />}
      <Table<GeneralRevisionNoticeDto> rowKey="id" size="small" loading={isFetching} dataSource={data?.items ?? []}
        locale={{ emptyText: 'No notice yet: post the assessments, then generate the notices' }}
        rowSelection={open ? { selectedRowKeys: selected, preserveSelectedRowKeys: true, onChange: (k) => setSelected(k as string[]),
          getCheckboxProps: (r) => ({ disabled: r.status !== 'Issued' }) } : undefined}
        pagination={{ current: page, pageSize: 20, total: data?.totalCount ?? 0, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: 'Number', dataIndex: 'noticeNumber', render: (v: string | null) => v ?? '—' },
          { title: 'Addressee', dataIndex: 'addresseeNames' },
          { title: 'First PIN', dataIndex: 'firstPin' },
          { title: 'Units', dataIndex: 'itemCount', align: 'right' },
          { title: 'Assessed value', dataIndex: 'assessedValue', align: 'right', render: (v: number) => formatMoney(v) },
          {
            title: 'Status', render: (_, n) => (
              <span><Tag color={noticeColor[n.status]}>{n.status}</Tag>{n.issueOverdue && <Tag color="red">Issue overdue ({n.issueDueDate})</Tag>}</span>
            ),
          },
          { title: 'Received', dataIndex: 'receivedDate', render: (v: string | null, n) => (v ? `${v} (${noticeServiceModeLabel[n.serviceMode!]})` : '—') },
          { title: 'Appeal by', dataIndex: 'appealDeadline', render: (v: string | null) => v ?? '—' },
          { title: '', render: (_, n) => <PrintFormButton formCode="NOTICE_OF_ASSESSMENT" subjectId={n.id} issuable={n.status !== 'Draft'} /> },
        ]} />
      {serving && <ServiceModal id={gr.id} noticeIds={selected} onClose={(done) => { setServing(false); if (done) setSelected([]); }} />}
    </div>
  );
}

function ServiceModal({ id, noticeIds, onClose }: { id: string; noticeIds: string[]; onClose: (done: boolean) => void }) {
  const record = useRecordGeneralRevisionNoticeService(id);
  const [result, setResult] = useState<NoticeServiceResultDto>();
  return (
    <Modal open title={`Record service of ${noticeIds.length} notice(s)`} footer={null} destroyOnHidden onCancel={() => onClose(!!result)}>
      <Typography.Paragraph type="secondary">
        The same mode, receipt date and proof for every notice chosen (for instance one delivery list). Each notice&apos;s appeal period runs from its receipt.
      </Typography.Paragraph>
      {record.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not record" description={errorText(record.error)} />}
      {result ? (
        <>
          <Alert type={result.failures.length ? 'warning' : 'success'} showIcon style={{ marginBottom: 12 }}
            title={`${result.recorded} recorded${result.failures.length ? `, ${result.failures.length} refused` : ''}`} />
          {result.failures.length > 0 && (
            <Table rowKey="noticeId" size="small" pagination={false} dataSource={result.failures}
              columns={[{ title: 'Notice', dataIndex: 'noticeNumber' }, { title: 'Why', dataIndex: 'message' }]} />
          )}
          <Button style={{ marginTop: 12 }} onClick={() => onClose(true)}>Close</Button>
        </>
      ) : (
        <Form layout="vertical" initialValues={{ serviceMode: 'Personal', receivedDate: dayjs() }}
          onFinish={(v) => record.mutate({
            noticeIds, serviceMode: v.serviceMode, receivedDate: v.receivedDate.format('YYYY-MM-DD'), proofReference: v.proofReference.trim(),
            servedTo: v.servedTo?.trim() || null, notes: v.notes?.trim() || null, sentDate: v.sentDate ? v.sentDate.format('YYYY-MM-DD') : null,
          }, { onSuccess: setResult })}>
          <Form.Item name="serviceMode" label="Mode of service" rules={[{ required: true }]}>
            <Select options={(Object.keys(noticeServiceModeLabel) as NoticeServiceMode[]).filter((m) => m !== 'Email')
              .map((m) => ({ value: m, label: noticeServiceModeLabel[m] }))} />
          </Form.Item>
          <Form.Item name="receivedDate" label="Received on" rules={[{ required: true }]}><DatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} /></Form.Item>
          <Form.Item name="sentDate" label="Sent on (mail)"><DatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} /></Form.Item>
          <Form.Item name="proofReference" label="Proof (acknowledgment, registry receipt)" rules={[{ required: true }, { max: 200 }]}><Input /></Form.Item>
          <Form.Item name="servedTo" label="Received by" extra="Leave empty to use each notice's addressee."><Input maxLength={300} /></Form.Item>
          <Form.Item name="notes" label="Notes"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
          <Button type="primary" htmlType="submit" loading={record.isPending}>Record</Button>
        </Form>
      )}
    </Modal>
  );
}

function RollGatesSection({ gr }: { gr: GeneralRevisionDto }) {
  const { data, isFetching } = useRollGates(gr.id);
  return (
    <div>
      <Typography.Title level={5}>Assessment roll gate</Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        The revision&apos;s assessment roll is prepared once every unit is posted and its new TD approved, every notice the units need is served, and the waiting period after the
        latest receipt has passed (GRI 17).
      </Typography.Paragraph>
      <Table<RollGateDto> rowKey="municipalityId" size="small" loading={isFetching} dataSource={data ?? []} pagination={false}
        columns={[
          { title: 'City/municipality', dataIndex: 'municipalityName' },
          { title: 'Units', dataIndex: 'units', align: 'right' },
          { title: 'Not posted', dataIndex: 'unitsNotPosted', align: 'right' },
          { title: 'TD not approved', dataIndex: 'taxDeclarationsNotApproved', align: 'right' },
          { title: 'Notices needed', dataIndex: 'noticesRequired', align: 'right' },
          { title: 'Served', dataIndex: 'noticesServed', align: 'right' },
          { title: 'Latest receipt', dataIndex: 'latestReceipt', render: (v: string | null) => v ?? '—' },
          { title: 'Roll from', dataIndex: 'opensOn', render: (v: string | null) => v ?? '—' },
          {
            title: 'Gate', render: (_, g) => (g.open ? <Tag color="green">Open</Tag> : (
              <span><Tag color="gold">Closed</Tag><Typography.Text type="secondary" style={{ fontSize: 12 }}>{g.blockers.join(' ')}</Typography.Text></span>
            )),
          },
        ]} />
    </div>
  );
}

function RegisterRunsSection({ gr, open }: { gr: GeneralRevisionDto; open: boolean }) {
  const { data, isFetching } = useGeneralRevisionRegisterRuns(gr.id);
  const create = useCreateGeneralRevisionRegisterRuns(gr.id);
  const { data: gates } = useRollGates(gr.id);
  const [kind, setKind] = useState<GeneralRevisionRegisterKind>('AssessmentRollTaxable');
  const [reason, setReason] = useState('');
  const isRoll = kind === 'AssessmentRollTaxable' || kind === 'AssessmentRollExempt';
  const closed = isRoll && (gates ?? []).some((g) => !g.open);
  return (
    <div>
      <Typography.Title level={5}>Register runs</Typography.Title>
      {open && (
        <Space wrap style={{ marginBottom: 8 }} align="start">
          <Select style={{ width: 300 }} value={kind} onChange={setKind}
            options={(Object.keys(registerKindLabel) as GeneralRevisionRegisterKind[]).map((k) => ({ value: k, label: registerKindLabel[k] }))} />
          {closed && (
            <Input style={{ width: 380 }} maxLength={500} placeholder="Reason to run the roll before the gate opens" value={reason}
              onChange={(e) => setReason(e.target.value)} />
          )}
          <Button type="primary" loading={create.isPending} disabled={closed && !reason.trim()}
            onClick={() => create.mutate({ kind, overrideReason: closed ? reason.trim() : undefined }, { onSuccess: () => setReason('') })}>
            Prepare (every barangay{kind === 'OwnershipRecordCard' ? ' — one form per owner' : ''}), as of {gr.effectiveDate}
          </Button>
        </Space>
      )}
      {create.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not prepare" description={errorText(create.error)} />}
      <Table<GeneralRevisionRegisterRunDto> rowKey="id" size="small" loading={isFetching} dataSource={data ?? []} pagination={{ pageSize: 10 }}
        locale={{ emptyText: 'No register run for this revision yet' }}
        columns={[
          { title: 'Register', dataIndex: 'kind', render: (k: GeneralRevisionRegisterKind) => registerKindLabel[k] },
          { title: 'Barangay / owner', render: (_, r) => r.barangayName ?? r.taxpayerName ?? '—' },
          { title: 'As of', dataIndex: 'asOf' },
          { title: 'Gate override', dataIndex: 'rollGateOverrideReason', render: (v: string | null) => (v ? <Typography.Text type="warning">{v}</Typography.Text> : '—') },
          { title: 'Prepared', dataIndex: 'createdAt', render: (v: string) => dayjs(v).format('YYYY-MM-DD HH:mm') },
          { title: '', render: (_, r) => <PrintFormButton formCode={r.formCode} subjectId={r.id} issuable /> },
        ]} />
    </div>
  );
}
