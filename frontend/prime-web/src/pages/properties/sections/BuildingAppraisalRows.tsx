import { useState } from 'react';
import { Alert, Button, Checkbox, Form, Input, InputNumber, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useAddBuildingComponent, useAddBuildingUsePortion } from '../../../api/buildings';
import { useAddBuildingFloor, useAddBuildingMaterial } from '../../../api/descriptions';
import { useStructuralMaterials, useStructuralParts } from '../../../api/referenceData';
import { BuildingDescriptionModal } from '../modals/DescriptionModals';
import { useActualUses, useBuildingComponentTypes, useClassifications } from '../../../api/referenceData';
import { EditOutlined } from '@ant-design/icons';
import { ApiRequestError } from '../../../lib/apiClient';
import { formatMoney } from '../../../lib/format';
import type { BuildingDto } from '../../../lib/types';

const plain = new Intl.NumberFormat('en-PH', { maximumFractionDigits: 4 });
const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);

/**
 * A building's appraisal rows (MRPAAO Att. 2; docs/analysis/mrpaao-forms-model.md
 * §8.3): use portions, each valued at the SMV rate for its classification and
 * use (they must total the building's floor area), and components — an
 * "additional item" adds its cost to the construction cost of its portion, or
 * of all portions by floor area.
 */
export function BuildingAppraisalRows({ building, rpuId, propertyId }: { building: BuildingDto; rpuId: string; propertyId: string }) {
  const [dialog, setDialog] = useState<'portion' | 'component' | 'floor' | 'material' | 'description' | null>(null);
  const floors = building.floors ?? [];
  const materials = building.materials ?? [];
  const floorsTotal = floors.reduce((sum, f) => sum + f.area, 0);
  const covered = building.usePortions.reduce((sum, p) => sum + p.floorArea, 0);
  const portionLabel = (id: string | null) => (id === null ? 'All portions' : `Portion ${building.usePortions.find((p) => p.id === id)?.sequence ?? '?'}`);
  const header = (title: string, kind: 'portion' | 'component' | 'floor' | 'material', label: string) => (
    <Space style={{ margin: '12px 0 6px', justifyContent: 'space-between', width: '100%' }}>
      <Typography.Text strong>{title}</Typography.Text>
      <Button size="small" icon={<PlusOutlined />} onClick={() => setDialog(kind)}>{label}</Button>
    </Space>
  );

  const dash = (v: string | null) => v ?? '—';
  return (
    <div style={{ marginBottom: 8 }}>
      <Space style={{ margin: '4px 0 6px', justifyContent: 'space-between', width: '100%' }} wrap>
        <Typography.Text type="secondary">
          Permit {dash(building.buildingPermitNumber)}{building.buildingPermitDate ? ` (${building.buildingPermitDate})` : ''} · CCT {dash(building.condominiumCertificateNumber)}
          {' '}· Completion {dash(building.certificateOfCompletionDate)} · Occupancy {dash(building.certificateOfOccupancyDate)}
          {' '}· Constructed {dash(building.dateConstructed)} · Occupied {dash(building.dateOccupied)}
        </Typography.Text>
        <Button size="small" icon={<EditOutlined />} onClick={() => setDialog('description')}>Edit description</Button>
      </Space>

      {header('Floor areas', 'floor', 'Add floor')}
      {floors.length > 0 && floorsTotal !== building.totalFloorArea && (
        <Alert type="info" showIcon style={{ marginBottom: 6 }}
          title={`The floors recorded total ${plain.format(floorsTotal)} of ${plain.format(building.totalFloorArea)} sqm.`} />
      )}
      <Table size="small" rowKey="id" dataSource={floors} pagination={false} locale={{ emptyText: 'Not recorded' }}
        columns={[
          { title: 'Floor', dataIndex: 'floorNumber', width: 80 },
          { title: 'Area', dataIndex: 'area', align: 'right', render: (v: number) => `${plain.format(v)} sqm` },
        ]} />

      {header('Structural materials', 'material', 'Add material')}
      <Table size="small" rowKey="id" dataSource={materials} pagination={false} locale={{ emptyText: 'Checklist not filled' }}
        columns={[
          { title: 'Structure part', dataIndex: 'structuralPartName' },
          { title: 'Material', dataIndex: 'materialName' },
          { title: 'Floor', dataIndex: 'floorNumber', render: (v: number | null) => (v === null ? 'All floors' : v) },
        ]} />

      {header('Use portions', 'portion', 'Add use portion')}
      {building.usePortions.length > 0 && covered !== building.totalFloorArea && (
        <Alert type="warning" showIcon style={{ marginBottom: 6 }}
          title={`The portions cover ${plain.format(covered)} of ${plain.format(building.totalFloorArea)} sqm; they must cover it all before the building can be valued.`} />
      )}
      <Table size="small" rowKey="id" dataSource={building.usePortions} pagination={false}
        locale={{ emptyText: 'One use: valued under the Tax Declaration’s classification and use' }}
        columns={[
          { title: '#', dataIndex: 'sequence', width: 40 },
          { title: 'Classification', dataIndex: 'classificationName' },
          { title: 'Actual use', dataIndex: 'actualUseName' },
          { title: 'Floor area', dataIndex: 'floorArea', align: 'right', render: (v: number) => `${plain.format(v)} sqm` },
        ]} />

      {header('Components and additional items', 'component', 'Add component')}
      <Table size="small" rowKey="id" dataSource={building.components} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'None recorded' }}
        columns={[
          { title: 'Component', dataIndex: 'componentTypeName' },
          { title: 'Description', dataIndex: 'description', render: (v: string | null) => v ?? '—' },
          { title: 'Cost', dataIndex: 'cost', align: 'right', render: (v: number | null) => (v === null ? '—' : formatMoney(v)) },
          {
            title: 'Valued', render: (_, c) => (c.isAdditionalItem
              ? <Tag color="blue">Additional item · {portionLabel(c.buildingUsePortionId)}</Tag>
              : <Tag>Descriptive</Tag>),
          },
        ]} />

      {dialog === 'portion' && <PortionDialog building={building} rpuId={rpuId} propertyId={propertyId} onClose={() => setDialog(null)} />}
      {dialog === 'component' && <ComponentDialog building={building} rpuId={rpuId} propertyId={propertyId} onClose={() => setDialog(null)} />}
      {dialog === 'floor' && <FloorDialog building={building} rpuId={rpuId} propertyId={propertyId} onClose={() => setDialog(null)} />}
      {dialog === 'material' && <MaterialDialog building={building} rpuId={rpuId} propertyId={propertyId} onClose={() => setDialog(null)} />}
      {dialog === 'description' && <BuildingDescriptionModal building={building} rpuId={rpuId} onClose={() => setDialog(null)} />}
    </div>
  );
}

type DialogProps = { building: BuildingDto; rpuId: string; propertyId: string; onClose: () => void };

function PortionDialog({ building, rpuId, propertyId, onClose }: DialogProps) {
  const add = useAddBuildingUsePortion(building.id, rpuId, propertyId);
  const { data: classifications = [] } = useClassifications();
  const { data: uses = [] } = useActualUses();
  const remaining = building.totalFloorArea - building.usePortions.reduce((sum, p) => sum + p.floorArea, 0);
  return (
    <Modal open title="Add use portion" footer={null} onCancel={onClose} destroyOnHidden>
      {add.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not add" description={errorText(add.error)} />}
      <Form layout="vertical" initialValues={{ floorArea: remaining > 0 ? remaining : undefined }}
        onFinish={(v) => add.mutate({ classificationId: v.classificationId, actualUseId: v.actualUseId, floorArea: v.floorArea }, { onSuccess: onClose })}>
        <Form.Item name="classificationId" label="Classification" rules={[{ required: true }]}>
          <Select options={classifications.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Form.Item name="actualUseId" label="Actual use" rules={[{ required: true }]}>
          <Select options={uses.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Form.Item name="floorArea" label="Floor area (sqm)" rules={[{ required: true }]} extra={`${plain.format(remaining)} sqm not yet in a portion.`}>
          <InputNumber<number> min={0.0001} style={{ width: '100%' }} />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={add.isPending}>Add portion</Button>
      </Form>
    </Modal>
  );
}

function ComponentDialog({ building, rpuId, propertyId, onClose }: DialogProps) {
  const add = useAddBuildingComponent(building.id, rpuId, propertyId);
  const { data: types = [] } = useBuildingComponentTypes();
  const [form] = Form.useForm();
  const additional: boolean = Form.useWatch('isAdditionalItem', form) ?? false;
  return (
    <Modal open title="Add component" footer={null} onCancel={onClose} destroyOnHidden>
      {add.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not add" description={errorText(add.error)} />}
      <Form form={form} layout="vertical" onFinish={(v) => add.mutate({
        componentTypeId: v.componentTypeId, description: v.description ?? null, quantity: v.quantity ?? null, unitCost: v.unitCost ?? null,
        cost: v.cost ?? null, isAdditionalItem: v.isAdditionalItem ?? false, buildingUsePortionId: v.isAdditionalItem ? v.buildingUsePortionId ?? null : null,
      }, { onSuccess: onClose })}>
        <Form.Item name="componentTypeId" label="Component type" rules={[{ required: true }]}>
          <Select options={types.map((x) => ({ value: x.id, label: x.name }))} notFoundContent="No component types are configured" />
        </Form.Item>
        <Form.Item name="description" label="Description"><Input maxLength={500} /></Form.Item>
        <Form.Item name="isAdditionalItem" valuePropName="checked">
          <Checkbox>Additional item (fence, gate, garage, balcony …) — its cost is added to the construction cost</Checkbox>
        </Form.Item>
        <Space wrap>
          <Form.Item name="quantity" label="Quantity"><InputNumber<number> min={0} /></Form.Item>
          <Form.Item name="unitCost" label="Unit cost"><InputNumber<number> min={0} /></Form.Item>
          <Form.Item name="cost" label="Cost" extra="Or quantity × unit cost."><InputNumber<number> min={0} /></Form.Item>
        </Space>
        {additional && building.usePortions.length > 0 && (
          <Form.Item name="buildingUsePortionId" label="Belongs to">
            <Select allowClear placeholder="All portions (spread by floor area)"
              options={building.usePortions.map((p) => ({ value: p.id, label: `Portion ${p.sequence}: ${p.actualUseName}` }))} />
          </Form.Item>
        )}
        <Button type="primary" htmlType="submit" loading={add.isPending}>Add component</Button>
      </Form>
    </Modal>
  );
}

function FloorDialog({ building, rpuId, onClose }: DialogProps) {
  const add = useAddBuildingFloor(building.id, rpuId);
  const next = Math.max(0, ...(building.floors ?? []).map((f) => f.floorNumber)) + 1;
  return (
    <Modal open title="Add floor area" footer={null} onCancel={onClose} destroyOnHidden>
      {add.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not add" description={errorText(add.error)} />}
      <Form layout="vertical" initialValues={{ floorNumber: next }} onFinish={(v) => add.mutate({ floorNumber: v.floorNumber, area: v.area }, { onSuccess: onClose })}>
        <Space wrap>
          <Form.Item name="floorNumber" label="Floor" rules={[{ required: true }]}><InputNumber<number> min={1} /></Form.Item>
          <Form.Item name="area" label="Area (sqm)" rules={[{ required: true }]}><InputNumber<number> min={0.0001} /></Form.Item>
        </Space>
        <Button type="primary" htmlType="submit" loading={add.isPending}>Add floor</Button>
      </Form>
    </Modal>
  );
}

function MaterialDialog({ building, rpuId, onClose }: DialogProps) {
  const add = useAddBuildingMaterial(building.id, rpuId);
  const { data: parts = [] } = useStructuralParts();
  const { data: materials = [] } = useStructuralMaterials();
  const [form] = Form.useForm();
  const partId: string | undefined = Form.useWatch('structuralPartId', form);
  return (
    <Modal open title="Add structural material" footer={null} onCancel={onClose} destroyOnHidden>
      {add.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not add" description={errorText(add.error)} />}
      <Form form={form} layout="vertical" onFinish={(v) => add.mutate({
        structuralPartId: v.structuralPartId, structuralMaterialId: v.structuralMaterialId ?? null,
        otherSpecify: v.structuralMaterialId ? null : v.otherSpecify ?? null, floorNumber: v.floorNumber ?? null,
      }, { onSuccess: onClose })}>
        <Form.Item name="structuralPartId" label="Structure part" rules={[{ required: true }]}>
          <Select options={parts.map((p) => ({ value: p.id, label: p.name }))} onChange={() => form.setFieldsValue({ structuralMaterialId: undefined })} />
        </Form.Item>
        <Form.Item name="structuralMaterialId" label="Material" extra="Or leave empty and specify another below.">
          <Select allowClear disabled={!partId}
            options={materials.filter((m) => m.structuralPartId === partId).map((m) => ({ value: m.id, label: m.name }))} />
        </Form.Item>
        <Form.Item name="otherSpecify" label="Others (specify)"><Input maxLength={200} /></Form.Item>
        <Form.Item name="floorNumber" label="Floor" extra="Leave empty for all floors."><InputNumber<number> min={1} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={add.isPending}>Add material</Button>
      </Form>
    </Modal>
  );
}

