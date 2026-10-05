import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Alert, Button, Card, Checkbox, Descriptions, Input, InputNumber, Radio, Select, Space, Spin, Table, Tag, Typography } from 'antd';
import { useImpactStudy, useImpactUnits, useUpdateImpactStudy, type OptionInput, type SaveStudyRequest, type StudyDto, type TaxImpactUnitDto } from '../../api/impactStudies';
import { useActualUses, useClassifications } from '../../api/referenceData';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import { PrintFormButton } from '../../components/PrintFormButton';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const money = (v: number | null | undefined) => (v == null ? '—' : formatMoney(v));
const MAX_OPTIONS = 3;

const toRequest = (s: StudyDto): SaveStudyRequest => ({
  title: s.title, smvSimulationRunId: s.smvSimulationRunId, year: s.year, referenceDate: s.referenceDate, actualCollection: s.actualCollection,
  discounts: s.discounts, collectionSource: s.collectionSource, includeAllTaxableUnits: s.includeAllTaxableUnits, notes: s.notes,
  rates: s.rates.map((r) => ({ label: r.label, ratePercent: r.ratePercent, source: r.source })),
  options: s.options.map((o) => ({
    name: o.name, ratePercent: o.ratePercent, description: o.description,
    levels: o.levels.map((l) => ({ classificationId: l.classificationId, actualUseId: l.actualUseId, lowerValue: l.lowerValue, upperValue: l.upperValue, percent: l.percent })),
  })),
});

/** One study: the Treasurer's figures and the options (editable), then compliance, the scenarios and the units. */
export function ImpactStudyPage() {
  const { id = '' } = useParams();
  const { data: s, isLoading, error } = useImpactStudy(id);
  if (isLoading) return <Spin />;
  if (!s) return <Alert type="error" showIcon title="Could not load the study" description={error ? errorText(error) : undefined} />;
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Typography.Title level={3} style={{ margin: 0 }}>{s.title}</Typography.Title>
        <Space wrap>
          <PrintFormButton formCode="REVENUE_TAX_IMPACT_REPORT" subjectId={s.id} issuable={false} label="Report (RTIR)" />
          <Link to="/smv-impact">All studies</Link>
        </Space>
      </Space>
      <Descriptions size="small" bordered column={{ xs: 1, sm: 1, md: 2, lg: 3, xl: 3, xxl: 3 }} items={[
        { key: 'smv', label: 'SMV', children: `${s.smvReference} (${s.smvRevisionYear}), values as of ${s.simulationAsOf}` },
        { key: 'scope', label: 'Cities/municipalities', children: s.municipalities.join(', ') },
        { key: 'units', label: 'Units', children: s.includeAllTaxableUnits ? 'Every taxable unit' : 'Taxable land parcels' },
      ]} />
      {s.warnings.map((w) => <Alert key={w} type="warning" showIcon title={w} />)}
      <Results s={s} />
      <Editor key={JSON.stringify(toRequest(s))} s={s} />
      <Units s={s} />
    </Space>
  );
}

function Results({ s }: { s: StudyDto }) {
  const c = s.compliance;
  return (
    <>
      <Card size="small" title={`Revenue compliance, ${s.year} (tax gap approach)`}>
        {c ? (
          <Descriptions size="small" column={{ xs: 1, sm: 2, md: 2, lg: 4, xl: 4, xxl: 4 }} items={[
            { key: 'av', label: `Taxable AV as of ${s.referenceDate}`, children: money(c.taxableAssessedValue) },
            { key: 'pot', label: `Tax potential (× ${c.ratePercent}%)`, children: money(c.taxPotential) },
            { key: 'col', label: 'Collection + discounts', children: `${money(c.actualCollection)} + ${money(c.discounts)} = ${money(c.totalCollection)}` },
            { key: 'gap', label: 'Tax gap', children: <b>{money(c.taxGap)}</b> },
            { key: 'cr', label: 'Compliance rate', children: c.complianceRatePercent == null ? '—' : `${c.complianceRatePercent}%` },
            { key: 'ce', label: 'Collection efficiency', children: c.collectionEfficiencyPercent == null ? '—' : `${c.collectionEfficiencyPercent}%` },
          ]} />
        ) : <Typography.Text type="secondary">Enter the year's collection and discounts (with the Treasurer's source) to compute it.</Typography.Text>}
      </Card>
      <Card size="small" title="Tax impact">
        <Table rowKey="key" size="small" pagination={false} dataSource={s.scenarios} scroll={{ x: 'max-content' }}
          columns={[
            { title: 'Scenario', render: (_, x) => <>{x.name}{x.rowsAtExistingLevel > 0 && <Tag style={{ marginLeft: 6 }}>{x.rowsAtExistingLevel} row(s) keep their level</Tag>}</> },
            { title: 'Rate', dataIndex: 'ratePercent', align: 'right', render: (v: number) => `${v}%` },
            { title: 'Units', align: 'right', render: (_, x) => x.summary.units },
            { title: 'Current tax', align: 'right', render: (_, x) => money(x.summary.currentTax) },
            { title: 'Tax', align: 'right', render: (_, x) => <b>{money(x.summary.scenarioTax)}</b> },
            { title: 'Lower / higher / same', align: 'right', render: (_, x) => `${x.summary.lower} / ${x.summary.higher} / ${x.summary.unchanged}` },
            { title: 'Increases (smallest – median – largest)', align: 'right', render: (_, x) => (x.summary.smallestIncrease == null ? '—'
              : `${money(x.summary.smallestIncrease)} – ${money(x.summary.medianIncrease)} – ${money(x.summary.largestIncrease)}`) },
            { title: 'Largest increase', align: 'right', render: (_, x) => (x.summary.largestIncreasePercent == null ? '—' : `${x.summary.largestIncreasePercent}%`) },
            { title: 'Reclassified', align: 'right', render: (_, x) => `${x.summary.reclassified} (${money(x.summary.reclassifiedTaxChange)})` },
          ]} />
      </Card>
    </>
  );
}

/** The Treasurer's figures and the options, saved as a whole. */
function Editor({ s }: { s: StudyDto }) {
  const save = useUpdateImpactStudy(s.id);
  const { data: classes = [] } = useClassifications();
  const { data: uses = [] } = useActualUses();
  const [r, setR] = useState<SaveStudyRequest>(() => toRequest(s));
  const setOption = (i: number, patch: Partial<OptionInput>) => setR({ ...r, options: r.options.map((o, j) => (j === i ? { ...o, ...patch } : o)) });
  const dirty = JSON.stringify(r) !== JSON.stringify(toRequest(s));
  return (
    <Card size="small" title="The Treasurer's figures and the options" extra={s.editable && (
      <Button type="primary" size="small" disabled={!dirty} loading={save.isPending} onClick={() => save.mutate(r)}>Save study</Button>
    )}>
      {save.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not save" description={errorText(save.error)} />}
      <Typography.Text strong>Existing tax rates</Typography.Text>
      <Table rowKey={(x) => String(r.rates.indexOf(x))} size="small" pagination={false} dataSource={r.rates} style={{ margin: '4px 0 8px' }} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No rate entered' }}
        columns={[
          { title: 'Rate', render: (_, x, i) => <Input aria-label="Rate label" disabled={!s.editable} value={x.label} maxLength={100} style={{ width: 200 }}
            onChange={(e) => setR({ ...r, rates: r.rates.map((y, j) => (j === i ? { ...y, label: e.target.value } : y)) })} /> },
          { title: 'Percent', render: (_, x, i) => <InputNumber<number> aria-label="Rate percent" disabled={!s.editable} min={0} max={100} step={0.1} value={x.ratePercent}
            onChange={(v) => setR({ ...r, rates: r.rates.map((y, j) => (j === i ? { ...y, ratePercent: v ?? 0 } : y)) })} /> },
          { title: 'Source', render: (_, x, i) => <Input aria-label="Rate source" disabled={!s.editable} value={x.source} maxLength={300} style={{ width: 280 }}
            onChange={(e) => setR({ ...r, rates: r.rates.map((y, j) => (j === i ? { ...y, source: e.target.value } : y)) })} /> },
          { title: '', render: (_, _x, i) => s.editable && <Button size="small" danger onClick={() => setR({ ...r, rates: r.rates.filter((_y, j) => j !== i) })}>Remove</Button> },
        ]} />
      {s.editable && <Button size="small" onClick={() => setR({ ...r, rates: [...r.rates, { label: '', ratePercent: 0, source: '' }] })}>Add a rate</Button>}

      <div style={{ marginTop: 12 }}><Typography.Text strong>The year's collection</Typography.Text></div>
      <Space wrap style={{ marginTop: 4 }}>
        <InputNumber<number> aria-label="Actual collection" disabled={!s.editable} min={0} placeholder="Actual collection" style={{ width: 180 }}
          value={r.actualCollection ?? undefined} onChange={(v) => setR({ ...r, actualCollection: v ?? null })} />
        <InputNumber<number> aria-label="Discounts" disabled={!s.editable} min={0} placeholder="Discounts given" style={{ width: 160 }}
          value={r.discounts ?? undefined} onChange={(v) => setR({ ...r, discounts: v ?? null })} />
        <Input aria-label="Collection source" disabled={!s.editable} placeholder="Source (the Treasurer's report)" style={{ width: 320 }} maxLength={500}
          value={r.collectionSource ?? ''} onChange={(e) => setR({ ...r, collectionSource: e.target.value || null })} />
        <Checkbox disabled={!s.editable} checked={r.includeAllTaxableUnits} onChange={(e) => setR({ ...r, includeAllTaxableUnits: e.target.checked })}>
          Every taxable unit
        </Checkbox>
      </Space>

      <div style={{ marginTop: 12 }}><Typography.Text strong>Tax options (up to {MAX_OPTIONS})</Typography.Text></div>
      {r.options.map((o, i) => (
        <Card key={i} size="small" style={{ marginTop: 8 }} title={
          <Space wrap>
            <Input aria-label="Option name" disabled={!s.editable} value={o.name} maxLength={100} style={{ width: 220 }} onChange={(e) => setOption(i, { name: e.target.value })} />
            <InputNumber<number> aria-label="Option rate" disabled={!s.editable} min={0} max={100} step={0.1} value={o.ratePercent} suffix="%"
              onChange={(v) => setOption(i, { ratePercent: v ?? 0 })} />
          </Space>
        } extra={s.editable && <Button size="small" danger onClick={() => setR({ ...r, options: r.options.filter((_y, j) => j !== i) })}>Remove option</Button>}>
          <Input aria-label="Option description" disabled={!s.editable} placeholder="Description" value={o.description ?? ''} maxLength={1000}
            onChange={(e) => setOption(i, { description: e.target.value || null })} />
          <Table rowKey={(l) => String(o.levels.indexOf(l))} size="small" pagination={false} dataSource={o.levels} style={{ marginTop: 8 }} scroll={{ x: 'max-content' }}
            locale={{ emptyText: 'No level: every row keeps its level' }}
            columns={[
              { title: 'Class', render: (_, l, k) => <Select aria-label="Level class" disabled={!s.editable} showSearch optionFilterProp="label" style={{ width: 180 }}
                value={l.classificationId || undefined} options={classes.map((c) => ({ value: c.id, label: c.name }))}
                onChange={(v) => setOption(i, { levels: o.levels.map((y, j) => (j === k ? { ...y, classificationId: v } : y)) })} /> },
              { title: 'Use (optional)', render: (_, l, k) => <Select aria-label="Level use" disabled={!s.editable} allowClear showSearch optionFilterProp="label" style={{ width: 180 }}
                value={l.actualUseId ?? undefined} options={uses.map((u) => ({ value: u.id, label: u.name }))}
                onChange={(v) => setOption(i, { levels: o.levels.map((y, j) => (j === k ? { ...y, actualUseId: v ?? null } : y)) })} /> },
              { title: 'Over', render: (_, l, k) => <InputNumber<number> aria-label="Level lower value" disabled={!s.editable} min={0} value={l.lowerValue}
                onChange={(v) => setOption(i, { levels: o.levels.map((y, j) => (j === k ? { ...y, lowerValue: v ?? 0 } : y)) })} /> },
              { title: 'Not over', render: (_, l, k) => <InputNumber<number> aria-label="Level upper value" disabled={!s.editable} min={0} value={l.upperValue ?? undefined}
                onChange={(v) => setOption(i, { levels: o.levels.map((y, j) => (j === k ? { ...y, upperValue: v ?? null } : y)) })} /> },
              { title: 'Level %', render: (_, l, k) => <InputNumber<number> aria-label="Level percent" disabled={!s.editable} min={0} max={100} value={l.percent}
                onChange={(v) => setOption(i, { levels: o.levels.map((y, j) => (j === k ? { ...y, percent: v ?? 0 } : y)) })} /> },
              { title: '', render: (_, _l, k) => s.editable && <Button size="small" danger onClick={() => setOption(i, { levels: o.levels.filter((_y, j) => j !== k) })}>Remove</Button> },
            ]} />
          {s.editable && <Button size="small" style={{ marginTop: 6 }}
            onClick={() => setOption(i, { levels: [...o.levels, { classificationId: '', actualUseId: null, lowerValue: 0, upperValue: null, percent: 0 }] })}>Add a level</Button>}
        </Card>
      ))}
      {s.editable && r.options.length < MAX_OPTIONS && (
        <Button size="small" style={{ marginTop: 8 }} onClick={() => setR({ ...r, options: [...r.options, { name: `Option ${r.options.length + 1}`, ratePercent: 0, description: null, levels: [] }] })}>
          Add an option
        </Button>
      )}
    </Card>
  );
}

function Units({ s }: { s: StudyDto }) {
  const [q, setQ] = useState<{ page: number; pageSize: number; change?: string; pin?: string }>({ page: 1, pageSize: 25 });
  const units = useImpactUnits(s.id, q, JSON.stringify(toRequest(s)));
  return (
    <Card size="small" title="Units">
      <Space wrap style={{ marginBottom: 8 }}>
        <Radio.Group size="small" value={q.change ?? ''} onChange={(e) => setQ({ ...q, page: 1, change: e.target.value || undefined })} optionType="button"
          options={[{ value: '', label: 'All' }, { value: 'higher', label: 'Higher' }, { value: 'lower', label: 'Lower' }, { value: 'unchanged', label: 'Unchanged' },
            { value: 'reclassified', label: 'Reclassified' }]} />
        <Input.Search size="small" allowClear placeholder="PIN starts with" style={{ width: 200 }} onSearch={(pin) => setQ({ ...q, page: 1, pin: pin || undefined })} />
      </Space>
      <Table<TaxImpactUnitDto> rowKey="rpuId" size="small" loading={units.isLoading} dataSource={units.data?.items ?? []} scroll={{ x: 'max-content' }}
        pagination={{ current: q.page, pageSize: q.pageSize, total: units.data?.totalCount ?? 0, onChange: (page, pageSize) => setQ({ ...q, page, pageSize }) }}
        columns={[
          { title: 'PIN', render: (_, u) => <Link to={`/properties/${u.propertyId}`}>{u.pin}</Link> },
          { title: 'Unit', render: (_, u) => `${u.rpuNumber} (${u.rpuType})` },
          { title: 'Classification', render: (_, u) => (u.reclassified ? <Tag color="orange">{u.currentClassification} → {u.newClassification}</Tag> : u.newClassification ?? u.currentClassification) },
          { title: 'Taxable AV now → new', align: 'right', render: (_, u) => `${money(u.currentTaxableAssessedValue)} → ${money(u.newTaxableAssessedValue)}` },
          { title: 'Tax now', dataIndex: 'currentTax', align: 'right', render: money },
          { title: 'Tax at new values', dataIndex: 'newTax', align: 'right', render: money },
          ...s.options.map((o, i) => ({ title: o.name, key: `o${i}`, align: 'right' as const, render: (_: unknown, u: TaxImpactUnitDto) => money(u.optionTaxes[i]) })),
        ]} />
    </Card>
  );
}
