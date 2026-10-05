import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  Alert, Button, Card, Checkbox, Descriptions, Form, Input, InputNumber, Modal, Select, Space, Spin, Table, Tag, Typography,
} from 'antd';
import {
  useAdoptSalesAnalysisGroup, useRefreshSalesAnalysis, useSalesAnalysis, useSetSalesAnalysisGroups, useUpdateAnalysisSale, useUpdateSalesAnalysis,
} from '../../api/salesAnalyses';
import { useSubClassifications } from '../../api/referenceData';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import { PrintFormButton } from '../../components/PrintFormButton';
import {
  areaUnitLabel, type SalesAnalysisDto, type SalesAnalysisGroupDto, type SalesAnalysisRangeDto, type SalesAnalysisSaleDto, type SalesAnalysisValueDto,
} from '../../lib/salesAnalysisTypes';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const money = (v: number | null | undefined) => (v == null ? '—' : formatMoney(v));

/**
 * One sales analysis (SMV Forms 2–4, 6–8; docs/analysis/smv-preparation-general-revision.md §4.2): the sales as recorded and
 * adjusted, the rounded values with their intervals (Table 1), the ranges and their frequencies (Table 2), and the sub-classes
 * the assessor forms from them, with the values PRIME proposes and those the assessor adopts (Table 3).
 */
export function SalesAnalysisPage() {
  const { id = '' } = useParams();
  const { data: a, isLoading, error } = useSalesAnalysis(id);
  if (isLoading) return <Spin />;
  if (!a) return <Alert type="error" showIcon title="Could not load the analysis" description={error ? errorText(error) : undefined} />;
  const unit = areaUnitLabel[a.areaUnit];
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Typography.Title level={3} style={{ margin: 0 }}>Sales analysis — {a.classificationName}{a.actualUseName ? ` (${a.actualUseName})` : ''}</Typography.Title>
        <Space wrap>
          {(a.areaUnit === 'Hectare' ? [[6, 'Statement'], [7, 'Tabulation'], [8, 'Computation']] : [[2, 'Statement'], [3, 'Tabulation'], [4, 'Computation']]).map(([n, label]) => (
            <PrintFormButton key={n} formCode={`SMV_FORM_${n}`} subjectId={a.id} issuable={!['Preparing', 'PublishedForComment', 'Remanded', 'Cancelled'].includes(a.preparationStatus)}
              label={`Form ${n} · ${label}`} />
          ))}
          <Link to={`/smv-preparation/${a.smvPreparationId}`}>Back to the preparation</Link>
        </Space>
      </Space>
      {!a.editable && <Typography.Text type="secondary">Read only: the analysis is done by the provincial office before the SMV is submitted, or after a remand.</Typography.Text>}
      {a.warnings.map((w) => <Alert key={w} type="warning" showIcon title={w} />)}
      <ParametersCard a={a} />
      <SalesCard a={a} />
      <Space align="start" wrap style={{ width: '100%' }} styles={{ item: { flex: '1 1 340px', minWidth: 0 } }}>
        <Card size="small" title={`Table 1 — rounded unit values (${unit}), lowest to highest`}>
          <Table<SalesAnalysisValueDto> rowKey="saleId" size="small" dataSource={a.values} pagination={{ pageSize: 25 }}
            locale={{ emptyText: 'No sale analysed' }}
            columns={[
              { title: 'No.', dataIndex: 'number', align: 'right' },
              { title: 'Adjusted', dataIndex: 'adjustedUnitPrice', align: 'right', render: money },
              { title: 'Rounded', dataIndex: 'roundedUnitValue', align: 'right', render: money },
              { title: 'Interval %', dataIndex: 'intervalPercent', align: 'right', render: (v: number | null) => v ?? '—' },
            ]} />
          <Typography.Text type="secondary">Average interval: {a.averageIntervalPercent ?? '—'}%</Typography.Text>
        </Card>
        <Card size="small" title={`Table 2 — ranges at ±${a.effectiveWidthPercent ?? '?'}%`}>
          <Table<SalesAnalysisRangeDto> rowKey="number" size="small" dataSource={a.ranges} pagination={false}
            locale={{ emptyText: 'No range' }}
            columns={[
              { title: 'Range', dataIndex: 'number', align: 'right' },
              { title: 'Low', dataIndex: 'low', align: 'right', render: money },
              { title: 'Mid', dataIndex: 'mid', align: 'right', render: money },
              { title: 'High', dataIndex: 'high', align: 'right', render: money },
              { title: 'Sales', dataIndex: 'frequency', align: 'right' },
              { title: 'Sub-class', render: (_, r) => { const g = a.groups.find((x) => x.id === r.groupId); return g ? <Tag>{g.subClassificationName ?? `Group ${g.sequence}`}</Tag> : '—'; } },
            ]} />
        </Card>
      </Space>
      <GroupsCard key={JSON.stringify(a.groups)} a={a} />
    </Space>
  );
}

function ParametersCard({ a }: { a: SalesAnalysisDto }) {
  const update = useUpdateSalesAnalysis(a.id);
  const refresh = useRefreshSalesAnalysis(a.id);
  return (
    <Card size="small" title="Parameters" extra={a.editable && <Button size="small" loading={refresh.isPending} onClick={() => refresh.mutate(undefined)}>Refresh sales</Button>}>
      <Descriptions size="small" column={{ xs: 1, sm: 1, md: 2, lg: 3, xl: 3, xxl: 3 }} items={[
        { key: 'scope', label: 'Market areas', children: a.municipalities.join(', ') },
        { key: 'period', label: 'Sales period', children: `${a.salesFrom ?? 'first'} to ${a.salesTo ?? 'latest'}` },
        { key: 'bvd', label: 'Base valuation date', children: a.baseValuationDate ?? 'not set' },
      ]} />
      {(update.isError || refresh.isError) && <Alert type="error" showIcon style={{ marginBottom: 8 }} title="Could not save"
        description={errorText(update.error ?? refresh.error)} />}
      <Form layout="inline" disabled={!a.editable} key={`${a.roundingIncrement}-${a.rangeWidthPercent}-${a.notes}`}
        initialValues={{ roundingIncrement: a.roundingIncrement, rangeWidthPercent: a.rangeWidthPercent, notes: a.notes }}
        onFinish={(v) => update.mutate({ roundingIncrement: v.roundingIncrement ?? 0, rangeWidthPercent: v.rangeWidthPercent ?? null, notes: v.notes ?? null })}>
        <Form.Item name="roundingIncrement" label="Round to"><InputNumber<number> min={0} /></Form.Item>
        <Form.Item name="rangeWidthPercent" label="Range width ±%">
          <InputNumber<number> min={0.01} max={100} placeholder={a.averageIntervalPercent != null ? `${a.averageIntervalPercent} (average)` : ''} style={{ width: 150 }} />
        </Form.Item>
        <Form.Item name="notes" label="Notes" style={{ maxWidth: '100%' }}><Input style={{ width: 320, maxWidth: '100%' }} maxLength={2000} /></Form.Item>
        <Button htmlType="submit" loading={update.isPending}>Save</Button>
      </Form>
    </Card>
  );
}

function SalesCard({ a }: { a: SalesAnalysisDto }) {
  const [editing, setEditing] = useState<SalesAnalysisSaleDto>();
  return (
    <Card size="small" title={`Sales (Form ${a.areaUnit === 'Hectare' ? '6 / 7' : '2 / 3'}) — ${a.sales.length}`}>
      <Table<SalesAnalysisSaleDto> rowKey="id" size="small" dataSource={a.sales} pagination={{ pageSize: 20 }} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No accepted sale of this class in the market areas' }}
        columns={[
          { title: 'Date', dataIndex: 'transactionDate' },
          { title: 'Location', render: (_, s) => [s.location, s.barangayName].filter(Boolean).join(', ') || '—' },
          { title: 'TD / PIN', render: (_, s) => [s.taxDeclarationNumber, s.pin].filter(Boolean).join(' / ') || '—' },
          { title: 'Recorded sub-class', dataIndex: 'subClassificationName', render: (v: string | null) => v ?? '—' },
          { title: 'Area', dataIndex: 'area', align: 'right', render: (v: number | null) => v ?? '—' },
          { title: 'Land price', dataIndex: 'price', align: 'right', render: money },
          { title: 'Unit price', dataIndex: 'unitPrice', align: 'right', render: money },
          { title: 'Time factor', dataIndex: 'timeFactor', align: 'right', render: (v: number | null) => v ?? '—' },
          { title: 'Other adj. %', dataIndex: 'otherAdjustmentPercent', align: 'right' },
          { title: 'Adjusted', dataIndex: 'adjustedUnitPrice', align: 'right', render: money },
          { title: 'Rounded', dataIndex: 'roundedUnitValue', align: 'right', render: money },
          { title: 'Not analysed because', render: (_, s) => s.exclusionReason && <Typography.Text type={s.leftOut ? 'warning' : 'danger'}>{s.leftOut ? 'Left out: ' : ''}{s.exclusionReason}</Typography.Text> },
          { title: '', render: (_, s) => a.editable && <Button size="small" onClick={() => setEditing(s)}>Edit</Button> },
        ]} />
      {editing && <SaleModal analysisId={a.id} sale={editing} onClose={() => setEditing(undefined)} />}
    </Card>
  );
}

function SaleModal({ analysisId, sale, onClose }: { analysisId: string; sale: SalesAnalysisSaleDto; onClose: () => void }) {
  const update = useUpdateAnalysisSale(analysisId);
  const [leftOut, setLeftOut] = useState(sale.leftOut);
  return (
    <Modal open title={`Sale of ${sale.transactionDate}`} footer={null} destroyOnHidden onCancel={onClose}>
      {update.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not save" description={errorText(update.error)} />}
      <Form layout="vertical" initialValues={{ leftOut: sale.leftOut, exclusionReason: sale.leftOut ? sale.exclusionReason : null, otherAdjustmentPercent: sale.otherAdjustmentPercent, note: sale.note }}
        onFinish={(v) => update.mutate({ saleId: sale.id, leftOut: !!v.leftOut, exclusionReason: v.exclusionReason ?? null,
          otherAdjustmentPercent: v.otherAdjustmentPercent ?? 0, note: v.note ?? null }, { onSuccess: onClose })}>
        <Form.Item name="otherAdjustmentPercent" label="Other adjustment % (agricultural road and distance deductions: negative)">
          <InputNumber<number> min={-100} max={100} step={1} />
        </Form.Item>
        <Form.Item name="leftOut" valuePropName="checked"><Checkbox onChange={(e) => setLeftOut(e.target.checked)}>Leave this sale out of the analysis</Checkbox></Form.Item>
        {leftOut && <Form.Item name="exclusionReason" label="Why" rules={[{ required: true }, { max: 500 }]}><Input /></Form.Item>}
        <Form.Item name="note" label="Note"><Input maxLength={500} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={update.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}

interface GroupRow { key: string; fromValue: number | null; toValue: number | null; subClassificationId: string | null; adoptedValue: number | null; basis: string | null }

/** Table 3: the assessor's sub-classes over the ranges, highest first; adopting writes a draft row of the proposed SMV. */
function GroupsCard({ a }: { a: SalesAnalysisDto }) {
  const { data: subClasses = [] } = useSubClassifications();
  const save = useSetSalesAnalysisGroups(a.id);
  const adopt = useAdoptSalesAnalysisGroup(a.id);
  const adopted = a.groups.filter((g) => g.adoptedAt);
  const toRows = () => a.groups.filter((g) => !g.adoptedAt).map((g) => ({
    key: g.id, fromValue: g.fromValue, toValue: g.toValue, subClassificationId: g.subClassificationId, adoptedValue: g.adoptedValue, basis: g.basis,
  }));
  // The editor starts from the saved groups; the page remounts it whenever they change.
  const [rows, setRows] = useState<GroupRow[]>(toRows);
  const set = (key: string, patch: Partial<GroupRow>) => setRows(rows.map((r) => (r.key === key ? { ...r, ...patch } : r)));
  const unit = areaUnitLabel[a.areaUnit];
  const subClassOptions = subClasses.map((s) => ({ value: s.id, label: s.name }));
  const dirty = JSON.stringify(rows) !== JSON.stringify(toRows());
  return (
    <Card size="small" title="Table 3 — sub-classes and adopted unit values"
      extra={a.editable && (
        <Space>
          <Button size="small" onClick={() => setRows([...rows, { key: crypto.randomUUID(), fromValue: null, toValue: null, subClassificationId: null, adoptedValue: null, basis: null }])}>Add a sub-class</Button>
          <Button size="small" type="primary" disabled={!dirty || rows.some((r) => r.fromValue == null || r.toValue == null)} loading={save.isPending}
            onClick={() => save.mutate(rows.map((r) => ({ fromValue: r.fromValue!, toValue: r.toValue!, subClassificationId: r.subClassificationId, adoptedValue: r.adoptedValue, basis: r.basis })))}>
            Save sub-classes
          </Button>
        </Space>
      )}>
      <Typography.Paragraph type="secondary">
        Merge ranges with few sales: a sub-class runs from the low of one range to the high of another. PRIME proposes the mean of the merged midpoints
        weighted by their sales; the assessor adopts the value, which becomes a draft row of the proposed SMV ({unit}).
      </Typography.Paragraph>
      {(save.isError || adopt.isError) && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not save" description={errorText(save.error ?? adopt.error)} />}
      <Table<SalesAnalysisGroupDto> rowKey="id" size="small" dataSource={a.groups} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No sub-class formed yet' }}
        columns={[
          { title: '#', dataIndex: 'sequence', align: 'right' },
          { title: 'Sub-class', dataIndex: 'subClassificationName', render: (v: string | null) => v ?? '—' },
          { title: 'Rounded values', render: (_, g) => `${money(g.fromValue)} – ${money(g.toValue)}` },
          { title: 'Sales', dataIndex: 'frequency', align: 'right' },
          { title: 'Proposed', dataIndex: 'proposedValue', align: 'right', render: money },
          { title: 'Adopted', dataIndex: 'adoptedValue', align: 'right', render: money },
          { title: 'Basis', dataIndex: 'basis' },
          {
            title: '', render: (_, g) => g.adoptedAt ? <Tag color="green">Adopted into the SMV</Tag> : a.editable && (
              <Button size="small" disabled={dirty || !g.subClassificationId || !g.adoptedValue} loading={adopt.isPending} onClick={() => adopt.mutate(g.id)}>Adopt</Button>
            ),
          },
        ]} />
      {a.editable && rows.length > 0 && (
        <Table<GroupRow> rowKey="key" size="small" dataSource={rows} pagination={false} scroll={{ x: 'max-content' }} style={{ marginTop: 12 }}
          title={() => 'Edit the sub-classes not yet adopted'}
          columns={[
            { title: 'From range', render: (_, r) => <Select aria-label="From range" style={{ width: 210 }} value={r.fromValue ?? undefined}
              onChange={(v) => set(r.key, { fromValue: v })} options={a.ranges.map((x) => ({ value: x.low, label: `Range ${x.number} (from ${money(x.low)})` }))} /> },
            { title: 'To range', render: (_, r) => <Select aria-label="To range" style={{ width: 210 }} value={r.toValue ?? undefined}
              onChange={(v) => set(r.key, { toValue: v })} options={a.ranges.map((x) => ({ value: x.high, label: `Range ${x.number} (to ${money(x.high)})` }))} /> },
            { title: 'Sub-class', render: (_, r) => <Select aria-label="Sub-class" allowClear showSearch optionFilterProp="label" style={{ width: 180 }}
              value={r.subClassificationId ?? undefined} onChange={(v) => set(r.key, { subClassificationId: v ?? null })} options={subClassOptions} /> },
            { title: 'Value to adopt', render: (_, r) => <InputNumber<number> aria-label="Value to adopt" min={0.01} value={r.adoptedValue ?? undefined}
              onChange={(v) => set(r.key, { adoptedValue: v ?? null })} /> },
            { title: 'Basis', render: (_, r) => <Input aria-label="Basis" style={{ width: 220 }} value={r.basis ?? ''} maxLength={1000}
              onChange={(e) => set(r.key, { basis: e.target.value || null })} /> },
            { title: '', render: (_, r) => <Button size="small" danger onClick={() => setRows(rows.filter((x) => x.key !== r.key))}>Remove</Button> },
          ]} />
      )}
      {adopted.length > 0 && <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
        Adopted values are rows of the proposed SMV; a correction is a new row entered on the Valuation Rules page.
      </Typography.Paragraph>}
    </Card>
  );
}
