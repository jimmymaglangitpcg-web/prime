import { useState } from 'react';
import { Alert, Button, DatePicker, Empty, Input, Modal, Popconfirm, Space, Table, Tabs, Tag, Typography, message } from 'antd';
import { ReloadOutlined, SendOutlined } from '@ant-design/icons';
import { Link } from 'react-router-dom';
import dayjs, { type Dayjs } from 'dayjs';
import { MunicipalityFilter } from '../../components/MunicipalityFilter';
import { useCurrentUser } from '../../api/offices';
import {
  useApprovedDocuments, useRollActions, useRollSubmissions,
  type ApprovedDocumentDto, type RollStatus, type RollSubmissionDto, type RollSubmissionItemDto,
} from '../../api/submissions';
import { ApiRequestError } from '../../lib/apiClient';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const date = (v: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const monthLabel = (r: { year: number; month: number }) => dayjs(new Date(r.year, r.month - 1, 1)).format('MMMM YYYY');
const rollColor: Record<RollStatus, string> = { Submitted: 'blue', Acknowledged: 'green', Returned: 'orange' };
const kindLabel: Record<ApprovedDocumentDto['kind'], string> = { Land: 'Land', Building: 'Building', Machinery: 'Machinery', OtherImprovement: 'Other' };

/**
 * Submissions to the province (docs/analysis/province-wide-operation.md §3.7, LP-6). Each FAAS and TD is
 * submitted by its approval: its printed copy is frozen then. The monthly assessment roll is prepared and
 * submitted by the municipal office, and acknowledged or returned by the province.
 */
export function SubmissionsPage() {
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ margin: 0 }}>Submissions to the province</Typography.Title>
        <Typography.Paragraph type="secondary" style={{ maxWidth: 900, marginBottom: 0 }}>
          Approved FAAS and Tax Declarations reach the Provincial Assessor&apos;s Office as approved, with the printed copy frozen at
          approval. Each month, the municipal office submits its assessment roll; the province acknowledges it or returns it with remarks.
        </Typography.Paragraph>
      </div>
      <Tabs items={[
        { key: 'approved', label: 'Approved FAAS and TDs', children: <ApprovedTab /> },
        { key: 'rolls', label: 'Monthly assessment roll', children: <RollsTab /> },
      ]} />
    </Space>
  );
}

function ApprovedTab() {
  const [municipalityId, setMunicipalityId] = useState<string>();
  const [range, setRange] = useState<[Dayjs, Dayjs]>([dayjs().startOf('month'), dayjs()]);
  const docs = useApprovedDocuments(range[0].format('YYYY-MM-DD'), range[1].format('YYYY-MM-DD'), municipalityId);
  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Space wrap>
        <MunicipalityFilter value={municipalityId} onChange={setMunicipalityId} />
        <DatePicker.RangePicker aria-label="Approved between" value={range} allowClear={false}
          onChange={(v) => v?.[0] && v[1] && setRange([v[0], v[1]])} />
        <Button icon={<ReloadOutlined />} onClick={() => docs.refetch()} loading={docs.isFetching}>Refresh</Button>
      </Space>
      {docs.isError && <Alert type="error" showIcon title="Could not load the list" description={errorText(docs.error)} />}
      <Table<ApprovedDocumentDto>
        rowKey="taxDeclarationId" size="small" loading={docs.isLoading} dataSource={docs.data ?? []} pagination={{ pageSize: 25 }} scroll={{ x: true }}
        locale={{ emptyText: <Empty description="No FAAS or TD approved in this period" /> }}
        columns={[
          { title: 'Approved', dataIndex: 'approvedAt', render: date },
          { title: 'TD No.', render: (_, d) => <span>{d.taxDeclarationNumber} <Tag>{kindLabel[d.kind]}</Tag></span> },
          { title: 'PIN', render: (_, d) => <Link to={`/properties/${d.propertyId}`}>{d.pin}</Link> },
          { title: 'Location', render: (_, d) => `${d.barangay}, ${d.municipality}` },
          { title: 'Approved by', dataIndex: 'approvedBy', render: (v: string | null) => v ?? '—' },
          { title: 'Now', dataIndex: 'currentStatus', render: (s: string) => <Tag color={s === 'Approved' ? 'green' : 'default'}>{s}</Tag> },
          {
            title: 'Printed copies', render: (_, d) => (
              <Space size={4} wrap>
                {d.taxDeclarationFormId ? <Link to={`/documents/${d.taxDeclarationFormId}`}>TD</Link> : <Tag>TD not printed</Tag>}
                {d.faasFormId && <Link to={`/documents/${d.faasFormId}`}>FAAS</Link>}
              </Space>
            ),
          },
        ]}
      />
    </Space>
  );
}

function RollsTab() {
  const me = useCurrentUser();
  const [municipalityId, setMunicipalityId] = useState<string>();
  const rolls = useRollSubmissions(municipalityId);
  const actions = useRollActions();
  const [toast, context] = message.useMessage();
  const [prepare, setPrepare] = useState<{ municipalityId?: string; month: Dayjs; remarks: string }>({ month: dayjs().subtract(1, 'month'), remarks: '' });
  const [returning, setReturning] = useState<RollSubmissionDto | null>(null);
  const [reason, setReason] = useState('');
  const municipal = me.data?.officeKind === 'Municipal';
  const provincial = !!me.data && (me.data.provinceWide || me.data.officeKind === 'Provincial');

  const submit = () => prepare.municipalityId && actions.submit.mutate(
    { municipalityId: prepare.municipalityId, year: prepare.month.year(), month: prepare.month.month() + 1, remarks: prepare.remarks || undefined },
    {
      onSuccess: (r) => toast.success(`Roll for ${monthLabel(r)} submitted: ${r.items.length} barangay roll(s).`),
      onError: (e) => toast.error(errorText(e)),
    });

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      {context}
      {municipal && (
        <Space wrap align="start" style={{ padding: 12, border: '1px solid #f0f0f0', borderRadius: 8 }}>
          <MunicipalityFilter value={prepare.municipalityId} placeholder="Municipality" onChange={(id) => setPrepare({ ...prepare, municipalityId: id })} />
          <DatePicker picker="month" aria-label="Month" allowClear={false} value={prepare.month}
            disabledDate={(d) => !d.isBefore(dayjs().startOf('month'))} onChange={(m) => m && setPrepare({ ...prepare, month: m })} />
          <Input placeholder="Remarks (optional)" value={prepare.remarks} onChange={(e) => setPrepare({ ...prepare, remarks: e.target.value })} style={{ width: 240 }} />
          <Popconfirm title={`Prepare and submit the roll for ${prepare.month.format('MMMM YYYY')}?`}
            description="PRIME prints the month's assessment roll of every barangay with entries and sends it to the province."
            onConfirm={submit} okText="Submit" disabled={!prepare.municipalityId}>
            <Button type="primary" icon={<SendOutlined />} disabled={!prepare.municipalityId} loading={actions.submit.isPending}>Prepare and submit</Button>
          </Popconfirm>
        </Space>
      )}
      <Space wrap>
        <MunicipalityFilter value={municipalityId} onChange={setMunicipalityId} />
        <Button icon={<ReloadOutlined />} onClick={() => rolls.refetch()} loading={rolls.isFetching}>Refresh</Button>
      </Space>
      {rolls.isError && <Alert type="error" showIcon title="Could not load the rolls" description={errorText(rolls.error)} />}
      <Table<RollSubmissionDto>
        rowKey="id" size="small" loading={rolls.isLoading} dataSource={rolls.data ?? []} pagination={{ pageSize: 25 }} scroll={{ x: true }}
        locale={{ emptyText: <Empty description="No monthly roll submitted yet" /> }}
        expandable={{ expandedRowRender: (r) => <RollItems items={r.items} />, rowExpandable: () => true }}
        columns={[
          { title: 'Month', render: (_, r) => monthLabel(r) },
          { title: 'Municipality', dataIndex: 'municipality' },
          { title: 'Status', dataIndex: 'status', render: (s: RollStatus) => <Tag color={rollColor[s]}>{s}</Tag> },
          { title: 'Submitted', render: (_, r) => `${date(r.submittedAt)} by ${r.submittedBy ?? '—'}` },
          { title: 'Barangay rolls', render: (_, r) => (r.items.length === 0 ? 'Nil return' : r.items.length) },
          {
            title: 'Province', render: (_, r) => (r.reviewedAt
              ? <span>{date(r.reviewedAt)} by {r.reviewedBy ?? '—'}{r.reviewRemarks ? ` — ${r.reviewRemarks}` : ''}</span>
              : '—'),
          },
          {
            title: '', render: (_, r) => provincial && r.status === 'Submitted' && (
              <Space size={4}>
                <Button size="small" type="primary" loading={actions.acknowledge.isPending}
                  onClick={() => actions.acknowledge.mutate({ id: r.id }, {
                    onSuccess: () => toast.success('Roll acknowledged.'), onError: (e) => toast.error(errorText(e)),
                  })}>Acknowledge</Button>
                <Button size="small" onClick={() => { setReturning(r); setReason(''); }}>Return</Button>
              </Space>
            ),
          },
        ]}
      />
      <Modal title={returning ? `Return the roll for ${monthLabel(returning)}` : ''} open={!!returning} onCancel={() => setReturning(null)}
        okText="Return" okButtonProps={{ disabled: !reason.trim(), loading: actions.returnRoll.isPending }} destroyOnHidden
        onOk={() => returning && actions.returnRoll.mutate({ id: returning.id, remarks: reason.trim() }, {
          onSuccess: () => { toast.success('Roll returned to the municipality.'); setReturning(null); },
          onError: (e) => toast.error(errorText(e)),
        })}>
        <Typography.Paragraph type="secondary">The municipality corrects its records and submits a new roll for the month. This one is kept.</Typography.Paragraph>
        <Input.TextArea aria-label="Remarks" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="What needs correcting" />
      </Modal>
    </Space>
  );
}

function RollItems({ items }: { items: RollSubmissionItemDto[] }) {
  if (items.length === 0) {
    return <Typography.Text type="secondary">No FAAS entered in any barangay that month.</Typography.Text>;
  }
  return (
    <Table<RollSubmissionItemDto> rowKey="id" size="small" pagination={false} dataSource={items}
      columns={[
        { title: 'Barangay', dataIndex: 'barangay' },
        { title: 'Roll', dataIndex: 'kind', render: (k: RollSubmissionItemDto['kind']) => (k === 'AssessmentRollTaxable' ? 'Taxable' : 'Exempt') },
        { title: 'Entries', dataIndex: 'entryCount' },
        { title: '', render: (_, i) => <Link to={`/documents/${i.issuedFormId}`}>Open printed roll</Link> },
      ]} />
  );
}
