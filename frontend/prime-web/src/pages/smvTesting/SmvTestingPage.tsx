import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Alert, Button, Card, DatePicker, Descriptions, Drawer, Form, Input, Modal, Progress, Select, Space, Statistic, Switch, Table, Tabs, Tag, Typography,
} from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  useCreateValuationTest, useSmvSimulation, useSmvSimulationResults, useSmvSimulations, useStartSmvSimulation, useValuationTest, useValuationTests,
  useValuationTestSales, type SimulationResultSearch,
} from '../../api/smvTesting';
import { useSmvs } from '../../api/valuation';
import { useAllMunicipalities } from '../../api/referenceData';
import { useCurrentUser } from '../../api/offices';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import {
  groupLevelLabel, jobStatusColor, type JobExecutionStatus, type SmvSimulationResultDto, type SmvSimulationRunDto, type ValuationTestDto,
  type ValuationTestGroupDto, type ValuationTestSaleDto,
} from '../../lib/smvTestingTypes';
import { PrintFormButton } from '../../components/PrintFormButton';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const money = (v: number | null | undefined) => (v == null ? '—' : formatMoney(v));

/**
 * Values a proposed SMV would give, before it is adopted (docs/analysis/smv-preparation-general-revision.md §4.3): simulations
 * of the units in a scope, and valuation testing against accepted land sales. Nothing here is a valuation or an assessment.
 */
export function SmvTestingPage() {
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Title level={3} style={{ margin: 0 }}>SMV Simulation &amp; Testing</Typography.Title>
      <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
        Prices units and sales under an SMV&apos;s rows whatever their status, so a proposed SMV can be checked before it is certified and approved.
        Results are kept apart: they are never stored as valuations, assessed or posted.
      </Typography.Paragraph>
      <Tabs items={[
        { key: 'simulations', label: 'Simulations', children: <SimulationsTab /> },
        { key: 'tests', label: 'Valuation testing', children: <ValuationTestsTab /> },
      ]} />
    </Space>
  );
}

/** SMVs that can be simulated or tested: any not rejected or cancelled, newest first. */
function useSmvOptions() {
  const { data } = useSmvs();
  return (data?.items ?? [])
    .filter((s) => !['Rejected', 'Cancelled', 'Voided'].includes(s.status))
    .sort((a, b) => b.effectivityDate.localeCompare(a.effectivityDate))
    .map((s) => ({
      value: s.id,
      label: `${s.reference || '(no reference)'} — ${s.revisionYear}, effective ${s.effectivityDate} (${s.status})`,
      effectivityDate: s.effectivityDate,
    }));
}

/** Cities/municipalities the user may work in. */
function useScopeOptions() {
  const { data: municipalities = [] } = useAllMunicipalities();
  const me = useCurrentUser();
  const allowed = me.data?.municipalityIds;
  return municipalities.filter((m) => !allowed || allowed.includes(m.id)).map((m) => ({ value: m.id, label: m.name }));
}

function SimulationsTab() {
  const { data = [], isLoading } = useSmvSimulations();
  const [starting, setStarting] = useState(false);
  const [openId, setOpenId] = useState<string>();
  return (
    <Card size="small" title="Simulations" extra={<Button type="primary" icon={<PlusOutlined />} onClick={() => setStarting(true)}>New simulation</Button>}>
      <Table<SmvSimulationRunDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={{ pageSize: 10 }}
        locale={{ emptyText: 'No simulation yet' }} scroll={{ x: 'max-content' }}
        onRow={(r) => ({ onClick: () => setOpenId(r.id), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'SMV', render: (_, r) => `${r.smvReference} (${r.smvRevisionYear})` },
          { title: 'As of', dataIndex: 'asOf' },
          { title: 'Scope', render: (_, r) => r.municipalities.map((m) => m.name).join(', ') },
          { title: 'Units', render: (_, r) => `${r.processedCount} / ${r.totalCount}`, align: 'right' },
          { title: 'Failed', dataIndex: 'failedCount', align: 'right' },
          { title: 'Status', dataIndex: 'status', render: (s: JobExecutionStatus) => <Tag color={jobStatusColor[s]}>{s}</Tag> },
          { title: 'Started', dataIndex: 'startedAt', render: (v: string | null) => (v ? dayjs(v).format('YYYY-MM-DD HH:mm') : '—') },
        ]} />
      {starting && <StartSimulationModal onClose={(id) => { setStarting(false); if (id) setOpenId(id); }} />}
      {openId && <SimulationDrawer id={openId} onClose={() => setOpenId(undefined)} />}
    </Card>
  );
}

function StartSimulationModal({ onClose }: { onClose: (id?: string) => void }) {
  const start = useStartSmvSimulation();
  const smvs = useSmvOptions();
  const scope = useScopeOptions();
  const [form] = Form.useForm();
  return (
    <Modal open title="New simulation" footer={null} width={720} destroyOnHidden onCancel={() => onClose()}>
      <Typography.Paragraph type="secondary">
        Every active unit with a current Tax Declaration in the scope is valued as of the date under the SMV&apos;s rows, as for a general revision
        (a building takes a new depreciation), and assessed at the levels in force then. It runs in the background.
      </Typography.Paragraph>
      {start.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not start" description={errorText(start.error)} />}
      <Form form={form} layout="vertical"
        onFinish={(v) => start.mutate({
          smvId: v.smvId, asOf: v.asOf.format('YYYY-MM-DD'), municipalityIds: v.municipalityIds, description: v.description ?? null,
        }, { onSuccess: (r) => onClose(r.id) })}>
        <Form.Item name="smvId" label="SMV" rules={[{ required: true }]}>
          <Select showSearch optionFilterProp="label" options={smvs} notFoundContent="No SMV"
            onChange={(id) => { const s = smvs.find((o) => o.value === id); if (s) form.setFieldValue('asOf', dayjs(s.effectivityDate)); }} />
        </Form.Item>
        <Form.Item name="asOf" label="Value as of (the proposed effectivity)" rules={[{ required: true }]}><DatePicker /></Form.Item>
        <Form.Item name="municipalityIds" label="Scope (cities/municipalities)" rules={[{ required: true, type: 'array', min: 1 }]}>
          <Select mode="multiple" showSearch optionFilterProp="label" options={scope} />
        </Form.Item>
        <Form.Item name="description" label="Description"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={start.isPending}>Start</Button>
      </Form>
    </Modal>
  );
}

function SimulationDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const { data: run } = useSmvSimulation(id);
  const running = run?.status === 'Queued' || run?.status === 'Running';
  const [q, setQ] = useState<SimulationResultSearch>({ page: 1, pageSize: 25 });
  const results = useSmvSimulationResults(id, q, running, `${run?.status}-${run?.processedCount}`);
  const s = run?.summary;
  return (
    <Drawer open size="large" title="Simulation" onClose={onClose} destroyOnHidden>
      {run && (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Descriptions size="small" column={1} bordered items={[
            { key: 'smv', label: 'SMV', children: `${run.smvReference} (${run.smvRevisionYear}; ${run.smvStatus} in PRIME)` },
            { key: 'asOf', label: 'Valued as of', children: run.asOf },
            { key: 'scope', label: 'Scope', children: run.municipalities.map((m) => m.name).join(', ') },
            { key: 'status', label: 'Status', children: <Tag color={jobStatusColor[run.status]}>{run.status}</Tag> },
            ...(run.description ? [{ key: 'desc', label: 'Description', children: run.description }] : []),
            ...(run.remarks ? [{ key: 'remarks', label: 'Remarks', children: run.remarks }] : []),
          ]} />
          {running && <Progress percent={run.totalCount ? Math.round((run.processedCount / run.totalCount) * 100) : 0} />}
          {s && (
            <Card size="small" title={`Units with both a current posted assessment and a simulated one: ${s.compared}`}>
              <Space wrap size="large">
                <Statistic title="Market value, current" value={money(s.currentMarketValue)} />
                <Statistic title="Market value, simulated" value={money(s.simulatedMarketValue)} />
                <Statistic title="Assessed value, current" value={money(s.currentAssessedValue)} />
                <Statistic title="Assessed value, simulated" value={money(s.simulatedAssessedValue)} />
              </Space>
              <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                {s.higher} higher, {s.lower} lower, {s.unchanged} unchanged; {s.classificationChanged} with another principal classification.
                {' '}Simulated: {s.simulated}; could not be valued or assessed: {s.failed}; no posted assessment yet: {s.withoutCurrent}.
                {' '}Simulated taxable assessed value of every simulated unit: {money(s.simulatedTaxableAssessedValue)}.
              </Typography.Paragraph>
            </Card>
          )}
          <Space wrap>
            <Input.Search allowClear placeholder="PIN starts with" style={{ width: 220 }} onSearch={(pin) => setQ({ ...q, page: 1, pin: pin || undefined })} />
            <Space><Switch size="small" checked={q.failed === true} onChange={(v) => setQ({ ...q, page: 1, failed: v ? true : undefined })} /> Failed only</Space>
            <Space><Switch size="small" checked={q.classificationChanged === true}
              onChange={(v) => setQ({ ...q, page: 1, classificationChanged: v ? true : undefined })} /> Classification changed</Space>
          </Space>
          <Table<SmvSimulationResultDto> rowKey="id" size="small" loading={results.isLoading} dataSource={results.data?.items ?? []}
            scroll={{ x: 'max-content' }} locale={{ emptyText: running ? 'Running…' : 'No result' }}
            pagination={{ current: q.page, pageSize: q.pageSize, total: results.data?.totalCount ?? 0, onChange: (page, pageSize) => setQ({ ...q, page, pageSize }) }}
            columns={[
              { title: 'PIN', render: (_, r) => <Link to={`/properties/${r.propertyId}`}>{r.pin}</Link> },
              { title: 'Unit', render: (_, r) => `${r.rpuNumber} (${r.rpuType})` },
              { title: 'Barangay', dataIndex: 'barangayName' },
              { title: 'Classification', render: (_, r) => (r.classificationChanged
                ? <Tag color="orange">{r.currentClassification} → {r.simulatedClassification}</Tag> : r.simulatedClassification ?? r.currentClassification ?? '—') },
              { title: 'MV current', dataIndex: 'currentMarketValue', align: 'right', render: money },
              { title: 'MV simulated', dataIndex: 'simulatedMarketValue', align: 'right', render: money },
              { title: 'Change', align: 'right', render: (_, r) => (r.marketValueChange == null ? '—'
                : `${money(r.marketValueChange)}${r.marketValueChangePercent != null ? ` (${r.marketValueChangePercent}%)` : ''}`) },
              { title: 'AV current', dataIndex: 'currentAssessedValue', align: 'right', render: money },
              { title: 'AV simulated', dataIndex: 'simulatedAssessedValue', align: 'right', render: money },
              { title: 'Why not simulated', dataIndex: 'failureReason', render: (v: string | null) => (v ? <Typography.Text type="danger">{v}</Typography.Text> : '') },
            ]} />
        </Space>
      )}
    </Drawer>
  );
}

function ValuationTestsTab() {
  const { data = [], isLoading } = useValuationTests();
  const [creating, setCreating] = useState(false);
  const [openId, setOpenId] = useState<string>();
  return (
    <Card size="small" title="Valuation tests" extra={<Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>New test</Button>}>
      <Typography.Paragraph type="secondary">
        Each accepted land sale is valued at its area times the SMV&apos;s unit value for its class, sub-class and use (no lot adjustments), against its land
        price as recorded; sales are not yet adjusted to a base valuation date (that comes with SMV preparation). Benchmarks are the province&apos;s
        configuration and none is set by PRIME.
      </Typography.Paragraph>
      <Table<ValuationTestDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={{ pageSize: 10 }}
        locale={{ emptyText: 'No valuation test yet' }} scroll={{ x: 'max-content' }}
        onRow={(r) => ({ onClick: () => setOpenId(r.id), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'SMV', render: (_, r) => `${r.smvReference} (${r.smvRevisionYear})` },
          { title: 'As of', dataIndex: 'asOf' },
          { title: 'Scope', render: (_, r) => r.municipalities.map((m) => m.name).join(', ') },
          { title: 'Sales period', render: (_, r) => `${r.salesFrom ?? 'first'} to ${r.salesTo ?? 'latest'}` },
          { title: 'Sales / tested', render: (_, r) => `${r.salesCount} / ${r.testedCount}`, align: 'right' },
          { title: 'Made', dataIndex: 'createdAt', render: (v: string) => dayjs(v).format('YYYY-MM-DD HH:mm') },
          { title: '', render: (_, r) => <span onClick={(e) => e.stopPropagation()}><PrintFormButton formCode="VALUATION_TEST_REPORT" subjectId={r.id} issuable /></span> },
        ]} />
      {creating && <CreateTestModal onClose={(id) => { setCreating(false); if (id) setOpenId(id); }} />}
      {openId && <TestDrawer id={openId} onClose={() => setOpenId(undefined)} />}
    </Card>
  );
}

function CreateTestModal({ onClose }: { onClose: (id?: string) => void }) {
  const create = useCreateValuationTest();
  const smvs = useSmvOptions();
  const scope = useScopeOptions();
  const [form] = Form.useForm();
  return (
    <Modal open title="New valuation test" footer={null} width={720} destroyOnHidden onCancel={() => onClose()}>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not test" description={errorText(create.error)} />}
      <Form form={form} layout="vertical"
        onFinish={(v) => create.mutate({
          smvId: v.smvId, asOf: v.asOf.format('YYYY-MM-DD'), municipalityIds: v.municipalityIds,
          salesFrom: v.sales?.[0]?.format('YYYY-MM-DD') ?? null, salesTo: v.sales?.[1]?.format('YYYY-MM-DD') ?? null, description: v.description ?? null,
        }, { onSuccess: (r) => onClose(r.id) })}>
        <Form.Item name="smvId" label="SMV" rules={[{ required: true }]}>
          <Select showSearch optionFilterProp="label" options={smvs} notFoundContent="No SMV"
            onChange={(id) => { const s = smvs.find((o) => o.value === id); if (s) form.setFieldValue('asOf', dayjs(s.effectivityDate)); }} />
        </Form.Item>
        <Form.Item name="asOf" label="Unit values as of" rules={[{ required: true }]}><DatePicker /></Form.Item>
        <Form.Item name="municipalityIds" label="Sales of (cities/municipalities)" rules={[{ required: true, type: 'array', min: 1 }]}>
          <Select mode="multiple" showSearch optionFilterProp="label" options={scope} />
        </Form.Item>
        <Form.Item name="sales" label="Sales period (optional)"><DatePicker.RangePicker allowEmpty={[true, true]} /></Form.Item>
        <Form.Item name="description" label="Description"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={create.isPending}>Test</Button>
      </Form>
    </Modal>
  );
}

function Mark({ ok }: { ok: boolean | null }) {
  return ok == null ? null : <Tag color={ok ? 'green' : 'red'}>{ok ? 'within' : 'outside'}</Tag>;
}

function TestDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const { data: test } = useValuationTest(id);
  const { data: sales = [], isLoading } = useValuationTestSales(id);
  const b = test?.benchmarks;
  const configured = !!b && (b.medianRatioLow != null || b.medianRatioHigh != null || b.maximumCoefficientOfDispersion != null);
  return (
    <Drawer open size="large" title="Valuation test" onClose={onClose} destroyOnHidden
      extra={test && <PrintFormButton formCode="VALUATION_TEST_REPORT" subjectId={test.id} issuable label="Print report" />}>
      {test && (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Descriptions size="small" column={1} bordered items={[
            { key: 'smv', label: 'SMV', children: `${test.smvReference} (${test.smvRevisionYear})` },
            { key: 'asOf', label: 'Unit values as of', children: test.asOf },
            { key: 'scope', label: 'Sales of', children: test.municipalities.map((m) => m.name).join(', ') },
            { key: 'period', label: 'Sales period', children: `${test.salesFrom ?? 'first recorded'} to ${test.salesTo ?? 'latest recorded'}` },
            { key: 'counts', label: 'Accepted land sales / tested', children: `${test.salesCount} / ${test.testedCount}` },
            {
              key: 'bench', label: 'Benchmarks', children: configured
                ? `median ratio ${b!.medianRatioLow ?? '…'} to ${b!.medianRatioHigh ?? '…'}; CoD at most ${b!.maximumCoefficientOfDispersion ?? '…'}%`
                : 'none configured',
            },
          ]} />
          <Table<ValuationTestGroupDto> rowKey={(g) => `${g.level}-${g.name}`} size="small" pagination={false} dataSource={test.groups ?? []}
            locale={{ emptyText: 'No sale could be tested' }}
            columns={[
              { title: 'Group', dataIndex: 'level', render: (l: ValuationTestGroupDto['level']) => groupLevelLabel[l] },
              { title: 'Name', dataIndex: 'name' },
              { title: 'Sales', dataIndex: 'count', align: 'right' },
              { title: 'Median ratio', align: 'right', render: (_, g) => <>{g.medianRatio ?? '—'} <Mark ok={g.medianWithinBenchmark} /></> },
              { title: 'CoD (%)', align: 'right', render: (_, g) => <>{g.coefficientOfDispersion ?? '—'} <Mark ok={g.dispersionWithinBenchmark} /></> },
            ]} />
          <Table<ValuationTestSaleDto> rowKey="id" size="small" loading={isLoading} dataSource={sales} pagination={{ pageSize: 20 }}
            scroll={{ x: 'max-content' }}
            columns={[
              { title: 'Date', dataIndex: 'transactionDate' },
              { title: 'Location', render: (_, s) => [s.barangayName, s.municipalityName].filter(Boolean).join(', ') },
              { title: 'Class / sub-class', render: (_, s) => [s.classificationName, s.subClassificationName].filter(Boolean).join(' — ') || '—' },
              { title: 'Area', align: 'right', render: (_, s) => (s.landArea == null ? '—' : `${s.landArea} ${s.landAreaUnit === 'Hectare' ? 'ha' : 'sqm'}`) },
              { title: 'Land price', dataIndex: 'price', align: 'right', render: money },
              { title: 'Unit value', align: 'right', render: (_, s) => (s.unitValue == null ? '—' : `${money(s.unitValue)} ${s.rateUnit ?? ''}`) },
              { title: 'Value', dataIndex: 'value', align: 'right', render: money },
              { title: 'Ratio', dataIndex: 'ratio', align: 'right', render: (v: number | null) => v ?? '—' },
              { title: 'Left out because', dataIndex: 'exclusionReason' },
            ]} />
        </Space>
      )}
    </Drawer>
  );
}
