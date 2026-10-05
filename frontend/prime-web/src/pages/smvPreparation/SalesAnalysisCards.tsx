import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Button, Card, DatePicker, Form, Input, InputNumber, Modal, Popconfirm, Select, Space, Table, Typography } from 'antd';
import { useAddTimeFactor, useCreateSalesAnalysis, useRemoveTimeFactor, useSalesAnalyses, useTimeFactors } from '../../api/salesAnalyses';
import { useActualUses, useAllMunicipalities, useClassifications } from '../../api/referenceData';
import { ApiRequestError } from '../../lib/apiClient';
import { areaUnitLabel, type AreaMeasure, type SalesAnalysisSummaryDto, type TimeAdjustmentFactorDto } from '../../lib/salesAnalysisTypes';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);

/** Factors adjusting sale prices to the base valuation date, entered by the assessor with their source (Q6). */
export function TimeFactorsCard({ preparationId, editable }: { preparationId: string; editable: boolean }) {
  const { data = [], isLoading } = useTimeFactors(preparationId);
  const remove = useRemoveTimeFactor(preparationId);
  const [adding, setAdding] = useState(false);
  return (
    <Card size="small" title="Time-adjustment factors (to the base valuation date)"
      extra={editable && <Button size="small" onClick={() => setAdding(true)}>Add a factor</Button>}>
      <Typography.Paragraph type="secondary">
        The LAM names no method: enter a factor per month or quarter with its source. A sale whose date no factor covers is not analysed;
        with no factor at all, prices are used as recorded.
      </Typography.Paragraph>
      {remove.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not remove" description={errorText(remove.error)} />}
      <Table<TimeAdjustmentFactorDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false}
        locale={{ emptyText: 'No factor entered' }}
        columns={[
          { title: 'From', dataIndex: 'periodFrom' },
          { title: 'To', dataIndex: 'periodTo' },
          { title: 'Factor', dataIndex: 'factor', align: 'right' },
          { title: 'Source', dataIndex: 'source' },
          {
            title: '', render: (_, r) => editable && (
              <Popconfirm title="Remove this factor? The analyses are recomputed." onConfirm={() => remove.mutate(r.id)}>
                <Button size="small" danger>Remove</Button>
              </Popconfirm>
            ),
          },
        ]} />
      {adding && <AddFactorModal preparationId={preparationId} onClose={() => setAdding(false)} />}
    </Card>
  );
}

function AddFactorModal({ preparationId, onClose }: { preparationId: string; onClose: () => void }) {
  const add = useAddTimeFactor(preparationId);
  return (
    <Modal open title="Add a time-adjustment factor" footer={null} destroyOnHidden onCancel={onClose}>
      {add.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not add" description={errorText(add.error)} />}
      <Form layout="vertical" onFinish={(v) => add.mutate({
        periodFrom: v.period[0].format('YYYY-MM-DD'), periodTo: v.period[1].format('YYYY-MM-DD'), factor: v.factor, source: v.source,
      }, { onSuccess: onClose })}>
        <Form.Item name="period" label="Sales dated" rules={[{ required: true }]}><DatePicker.RangePicker /></Form.Item>
        <Form.Item name="factor" label="Factor (1.05 raises prices by 5%)" rules={[{ required: true }]}>
          <InputNumber<number> min={0.000001} max={10} step={0.01} precision={6} />
        </Form.Item>
        <Form.Item name="source" label="Source" rules={[{ required: true }, { max: 300 }]}><Input /></Form.Item>
        <Button type="primary" htmlType="submit" loading={add.isPending}>Add</Button>
      </Form>
    </Modal>
  );
}

/** One analysis per class (and crop) behind the proposed unit values (SMV Forms 2–4, 6–8). */
export function SalesAnalysesCard({ preparationId, editable, coverage }: { preparationId: string; editable: boolean; coverage: string[] }) {
  const navigate = useNavigate();
  const { data = [], isLoading } = useSalesAnalyses(preparationId);
  const [creating, setCreating] = useState(false);
  return (
    <Card size="small" title="Sales analyses" extra={editable && <Button size="small" type="primary" onClick={() => setCreating(true)}>New analysis</Button>}>
      <Table<SalesAnalysisSummaryDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No analysis yet' }}
        onRow={(r) => ({ onClick: () => navigate(`/smv-preparation/analyses/${r.id}`), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Class', dataIndex: 'classificationName' },
          { title: 'Use / crop', dataIndex: 'actualUseName', render: (v: string | null) => v ?? 'Every use' },
          { title: 'Unit', dataIndex: 'areaUnit', render: (u: AreaMeasure) => areaUnitLabel[u] },
          { title: 'Sales / analysed', render: (_, r) => `${r.saleCount} / ${r.analysedCount}`, align: 'right' },
          { title: 'Sub-classes / adopted', render: (_, r) => `${r.groupCount} / ${r.adoptedCount}`, align: 'right' },
        ]} />
      {creating && <CreateAnalysisModal preparationId={preparationId} coverage={coverage}
        onClose={(id) => { setCreating(false); if (id) navigate(`/smv-preparation/analyses/${id}`); }} />}
    </Card>
  );
}

function CreateAnalysisModal({ preparationId, coverage, onClose }: { preparationId: string; coverage: string[]; onClose: (id?: string) => void }) {
  const create = useCreateSalesAnalysis(preparationId);
  const { data: classes = [] } = useClassifications();
  const { data: uses = [] } = useActualUses();
  const { data: municipalities = [] } = useAllMunicipalities();
  const options = municipalities.filter((m) => coverage.length === 0 || coverage.includes(m.name)).map((m) => ({ value: m.id, label: m.name }));
  return (
    <Modal open title="New sales analysis" footer={null} width={680} destroyOnHidden onCancel={() => onClose()}>
      <Typography.Paragraph type="secondary">
        Takes the accepted land sales of the class (and use or crop) in the chosen cities/municipalities. Agricultural land is usually analysed per crop
        and per hectare.
      </Typography.Paragraph>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not create" description={errorText(create.error)} />}
      <Form layout="vertical" initialValues={{ areaUnit: 'SquareMetre', roundingIncrement: 100 }}
        onFinish={(v) => create.mutate({
          classificationId: v.classificationId, actualUseId: v.actualUseId ?? null, municipalityIds: v.municipalityIds,
          salesFrom: v.sales?.[0]?.format('YYYY-MM-DD') ?? null, salesTo: v.sales?.[1]?.format('YYYY-MM-DD') ?? null, areaUnit: v.areaUnit,
          roundingIncrement: v.roundingIncrement ?? null, rangeWidthPercent: v.rangeWidthPercent ?? null, notes: v.notes ?? null,
        }, { onSuccess: (r) => onClose(r.id) })}>
        <Space wrap>
          <Form.Item name="classificationId" label="Class" rules={[{ required: true }]}>
            <Select showSearch optionFilterProp="label" style={{ width: 240 }} options={classes.map((c) => ({ value: c.id, label: c.name }))} />
          </Form.Item>
          <Form.Item name="actualUseId" label="Use or crop (optional)">
            <Select allowClear showSearch optionFilterProp="label" style={{ width: 240 }} options={uses.map((u) => ({ value: u.id, label: u.name }))} />
          </Form.Item>
        </Space>
        <Form.Item name="municipalityIds" label="Market areas (cities/municipalities)" rules={[{ required: true, type: 'array', min: 1 }]}>
          <Select mode="multiple" showSearch optionFilterProp="label" options={options} />
        </Form.Item>
        <Space wrap>
          <Form.Item name="sales" label="Sales period (optional)"><DatePicker.RangePicker allowEmpty={[true, true]} /></Form.Item>
          <Form.Item name="areaUnit" label="Unit values per" rules={[{ required: true }]}>
            <Select style={{ width: 160 }} options={(Object.keys(areaUnitLabel) as AreaMeasure[]).map((u) => ({ value: u, label: areaUnitLabel[u] }))} />
          </Form.Item>
          <Form.Item name="roundingIncrement" label="Round to"><InputNumber<number> min={0} /></Form.Item>
          <Form.Item name="rangeWidthPercent" label="Range width ±% (empty: average interval)"><InputNumber<number> min={0.01} max={100} /></Form.Item>
        </Space>
        <Form.Item name="notes" label="Notes"><Input.TextArea rows={2} maxLength={2000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={create.isPending}>Create</Button>
      </Form>
    </Modal>
  );
}
