import { useState } from 'react';
import { Alert, Button, Card, Col, DatePicker, Form, Input, InputNumber, Modal, Row, Select, Space, Table, Typography } from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import {
  useApproveBuildingCost, useApproveDepreciationSchedule, useApproveExtraItemCost, useBuildingCosts, useCreateBuildingCost,
  useCreateDepreciationSchedule, useCreateExtraItemCost, useDepreciationSchedules, useExtraItemCosts, useSmvs,
} from '../../api/valuation';
import { useBuildingComponentTypes, useBuildingTypes, useClassifications, useStructuralTypes } from '../../api/referenceData';
import { formatMoney } from '../../lib/format';
import {
  depreciationReadings, type BuildingCostDto, type DepreciationReading, type DepreciationScheduleDto, type ExtraItemCostDto,
} from '../../lib/types';
import { day, errorText, lookup, pct, period, statusTag, useToast } from './ruleHelpers';
import { ApproveButton } from './ApproveButton';

/** A depreciation table in words: "1–5 y 2%; 6+ y 3%". */
const bands = (t: DepreciationScheduleDto) =>
  t.rows.map((r) => `${r.toAge === null ? `${r.fromAge}+` : `${r.fromAge}–${r.toAge}`} y ${pct(r.percent)}`).join('; ');

/**
 * The SMV's building tables (docs/analysis/valuation-foundation.md §4.5): base unit construction
 * costs by structural type, extra-item costs, and depreciation tables. Each is created as a Draft
 * and approved by a second user; every cost and percent comes from the certified SMV.
 */
export function BuildingCostsTab() {
  const [creating, setCreating] = useState<'cost' | 'item' | 'depreciation' | null>(null);
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
        A building is valued on these when the SMV in force has construction costs: floor area × the cost for its structural type, plus its extra
        items, × completion, less depreciation for its age. Under an SMV without them, the older rate by classification and use applies.
      </Typography.Paragraph>
      <CostsCard onNew={() => setCreating('cost')} />
      <ItemsCard onNew={() => setCreating('item')} />
      <DepreciationCard onNew={() => setCreating('depreciation')} />
      {creating === 'cost' && <CreateCostModal onClose={() => setCreating(null)} />}
      {creating === 'item' && <CreateItemModal onClose={() => setCreating(null)} />}
      {creating === 'depreciation' && <CreateDepreciationModal onClose={() => setCreating(null)} />}
    </Space>
  );
}

function CostsCard({ onNew }: { onNew: () => void }) {
  const { data = [], isLoading } = useBuildingCosts();
  const approve = useApproveBuildingCost();
  const { context, fail } = useToast();
  return (
    <Card title="Construction cost per sqm (BUCC)" extra={<Button icon={<PlusOutlined />} onClick={onNew}>New cost</Button>}>
      {context}
      <Table<BuildingCostDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
        columns={[
          { title: 'SMV', dataIndex: 'smvReference' },
          { title: 'Structural type', dataIndex: 'structuralTypeName' },
          { title: 'Building kind', dataIndex: 'buildingTypeName', render: (v: string | null) => v ?? 'all' },
          { title: 'Classification', dataIndex: 'classificationName', render: (v: string | null) => v ?? 'all' },
          { title: 'Cost per sqm', dataIndex: 'costPerSquareMetre', align: 'right', render: formatMoney },
          { title: 'Legal basis', dataIndex: 'legalBasis' },
          { title: 'Period', render: (_, r) => period(r.effectiveDate, r.endDate) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          { title: '', render: (_, r) => <ApproveButton status={r.status} pending={approve.isPending} onApprove={() => approve.mutate(r.id, { onError: fail })} /> },
        ]} />
    </Card>
  );
}

function ItemsCard({ onNew }: { onNew: () => void }) {
  const { data = [], isLoading } = useExtraItemCosts();
  const approve = useApproveExtraItemCost();
  const { context, fail } = useToast();
  return (
    <Card title="Extra items" extra={<Button icon={<PlusOutlined />} onClick={onNew}>New item cost</Button>}>
      {context}
      <Table<ExtraItemCostDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
        columns={[
          { title: 'SMV', dataIndex: 'smvReference' },
          { title: 'Item', dataIndex: 'componentTypeName' },
          { title: 'Cost', align: 'right', render: (_, r) => `${formatMoney(r.unitCost)} per ${r.unit}` },
          { title: 'Legal basis', dataIndex: 'legalBasis' },
          { title: 'Period', render: (_, r) => period(r.effectiveDate, r.endDate) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          { title: '', render: (_, r) => <ApproveButton status={r.status} pending={approve.isPending} onApprove={() => approve.mutate(r.id, { onError: fail })} /> },
        ]} />
    </Card>
  );
}

function DepreciationCard({ onNew }: { onNew: () => void }) {
  const { data = [], isLoading } = useDepreciationSchedules();
  const approve = useApproveDepreciationSchedule();
  const { context, fail } = useToast();
  return (
    <Card title="Depreciation tables" extra={<Button icon={<PlusOutlined />} onClick={onNew}>New table</Button>}>
      {context}
      <Table<DepreciationScheduleDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
        columns={[
          { title: 'SMV', dataIndex: 'smvReference' },
          { title: 'Structural type', dataIndex: 'structuralTypeName' },
          { title: 'Reads as', dataIndex: 'reading', render: (v: DepreciationReading) => depreciationReadings.find((r) => r.value === v)?.label ?? v },
          { title: 'Age bands', render: (_, t) => bands(t) },
          { title: 'Remaining at least', dataIndex: 'minimumRemainingPercent', render: pct },
          { title: 'Legal basis', dataIndex: 'legalBasis' },
          { title: 'Period', render: (_, r) => period(r.effectiveDate, r.endDate) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          { title: '', render: (_, r) => <ApproveButton status={r.status} pending={approve.isPending} onApprove={() => approve.mutate(r.id, { onError: fail })} /> },
        ]} />
    </Card>
  );
}

/** The SMV picker, searchable (the list can be long). */
function SmvSelect(props: { value?: string; onChange?: (v: string) => void }) {
  const { data: smvs } = useSmvs();
  return (
    <Select {...props} showSearch optionFilterProp="label"
      options={(smvs?.items ?? []).map((s) => ({ value: s.id, label: `${s.reference} (${s.revisionYear})` }))} />
  );
}

/** Legal basis and effective date, common to the three tables. */
function BasisFields() {
  return (
    <Row gutter={12}>
      <Col xs={24} md={16}><Form.Item name="legalBasis" label="Legal basis" rules={[{ required: true }, { max: 500 }]}><Input /></Form.Item></Col>
      <Col xs={24} md={8}><Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
    </Row>
  );
}

function CreateCostModal({ onClose }: { onClose: () => void }) {
  const create = useCreateBuildingCost();
  const [form] = Form.useForm();
  const { data: structures = [] } = useStructuralTypes();
  const { data: kinds = [] } = useBuildingTypes();
  const { data: classifications = [] } = useClassifications();
  return (
    <Modal open title="New construction cost" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" onFinish={(v) => create.mutate({
        smvId: v.smvId, structuralTypeId: v.structuralTypeId, buildingTypeId: v.buildingTypeId ?? null, classificationId: v.classificationId ?? null,
        costPerSquareMetre: v.costPerSquareMetre, legalBasis: v.legalBasis, effectiveDate: day(v.effectiveDate)!, remarks: null,
      }, { onSuccess: onClose })}>
        <Form.Item name="smvId" label="SMV" rules={[{ required: true }]}><SmvSelect /></Form.Item>
        <Form.Item name="structuralTypeId" label="Structural type" rules={[{ required: true }]}>
          <Select showSearch optionFilterProp="label" options={lookup(structures)} />
        </Form.Item>
        <Row gutter={12}>
          <Col xs={24} md={12}><Form.Item name="buildingTypeId" label="Building kind" extra="Blank: every kind">
            <Select allowClear showSearch optionFilterProp="label" options={lookup(kinds)} />
          </Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="classificationId" label="Classification" extra="Blank: every classification">
            <Select allowClear showSearch optionFilterProp="label" options={lookup(classifications)} />
          </Form.Item></Col>
        </Row>
        <Form.Item name="costPerSquareMetre" label="Cost per sqm" rules={[{ required: true }]}>
          <InputNumber min={0.01} precision={2} style={{ width: '100%' }} />
        </Form.Item>
        <BasisFields />
      </Form>
    </Modal>
  );
}

function CreateItemModal({ onClose }: { onClose: () => void }) {
  const create = useCreateExtraItemCost();
  const [form] = Form.useForm();
  const { data: components = [] } = useBuildingComponentTypes();
  return (
    <Modal open title="New extra-item cost" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" onFinish={(v) => create.mutate({
        smvId: v.smvId, componentTypeId: v.componentTypeId, unit: v.unit, unitCost: v.unitCost, legalBasis: v.legalBasis,
        effectiveDate: day(v.effectiveDate)!, remarks: null,
      }, { onSuccess: onClose })}>
        <Form.Item name="smvId" label="SMV" rules={[{ required: true }]}><SmvSelect /></Form.Item>
        <Form.Item name="componentTypeId" label="Item" rules={[{ required: true }]}>
          <Select showSearch optionFilterProp="label" options={lookup(components)} />
        </Form.Item>
        <Row gutter={12}>
          <Col xs={12}><Form.Item name="unitCost" label="Cost per unit" rules={[{ required: true }]}><InputNumber min={0.01} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={12}><Form.Item name="unit" label="Unit" rules={[{ required: true }, { max: 30 }]}><Input placeholder="e.g. sqm, linear m, each" /></Form.Item></Col>
        </Row>
        <BasisFields />
      </Form>
    </Modal>
  );
}

interface BandForm { fromAge?: number; toAge?: number; percent?: number }

function CreateDepreciationModal({ onClose }: { onClose: () => void }) {
  const create = useCreateDepreciationSchedule();
  const [form] = Form.useForm();
  const { data: structures = [] } = useStructuralTypes();
  const reading = Form.useWatch('reading', form) as DepreciationReading | undefined;
  return (
    <Modal open title="New depreciation table" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()}
      width={640} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" initialValues={{ reading: 'Cumulative', rows: [{}] }} onFinish={(v) => create.mutate({
        smvId: v.smvId, structuralTypeId: v.structuralTypeId, reading: v.reading, minimumRemainingPercent: v.minimumRemainingPercent,
        rows: (v.rows ?? []).map((r: BandForm) => ({ fromAge: r.fromAge ?? 0, toAge: r.toAge ?? null, percent: r.percent ?? 0 })),
        legalBasis: v.legalBasis, effectiveDate: day(v.effectiveDate)!, remarks: null,
      }, { onSuccess: onClose })}>
        <Form.Item name="smvId" label="SMV" rules={[{ required: true }]}><SmvSelect /></Form.Item>
        <Form.Item name="structuralTypeId" label="Structural type" rules={[{ required: true }]}>
          <Select showSearch optionFilterProp="label" options={lookup(structures)} />
        </Form.Item>
        <Row gutter={12}>
          <Col xs={24} md={14}><Form.Item name="reading" label="The table's percent is"><Select options={depreciationReadings} /></Form.Item></Col>
          <Col xs={24} md={10}><Form.Item name="minimumRemainingPercent" label="Remaining value at least (%)" rules={[{ required: true }]}>
            <InputNumber min={0} max={100} precision={4} style={{ width: '100%' }} />
          </Form.Item></Col>
        </Row>
        <Typography.Text strong>
          {reading === 'YearlyWithinBand' ? 'Percent per year of age within each band' : 'Total depreciation for an age within each band'} (years; leave the last band open)
        </Typography.Text>
        <Form.List name="rows">
          {(fields, { add, remove }) => (
            <>
              {fields.map((field) => (
                <Space key={field.key} align="baseline" wrap style={{ display: 'flex' }}>
                  <Form.Item name={[field.name, 'fromAge']} rules={[{ required: true, message: 'From' }]}>
                    <InputNumber aria-label="From age" placeholder="from (y)" min={0} precision={0} style={{ width: 110 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'toAge']}>
                    <InputNumber aria-label="To age" placeholder="to (y)" min={0} precision={0} style={{ width: 110 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'percent']} rules={[{ required: true, message: '%' }]}>
                    <InputNumber aria-label="Band percent" placeholder="%" min={0} max={100} precision={4} style={{ width: 110 }} />
                  </Form.Item>
                  <MinusCircleOutlined aria-label="Remove band" onClick={() => remove(field.name)} />
                </Space>
              ))}
              <Button type="dashed" icon={<PlusOutlined />} onClick={() => add()} style={{ margin: '4px 0 12px' }}>Add band</Button>
            </>
          )}
        </Form.List>
        <BasisFields />
      </Form>
    </Modal>
  );
}
