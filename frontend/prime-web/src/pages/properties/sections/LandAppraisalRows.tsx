import { useState } from 'react';
import { Alert, Button, Descriptions, Form, Input, InputNumber, Modal, Select, Space, Switch, Table, Typography } from 'antd';
import { EditOutlined, PlusOutlined } from '@ant-design/icons';
import { useAddLandAdjustment, useAddLandImprovement, useAddLandStrip, useAdjustmentFactors, useUpdateLandAppraisalInputs } from '../../../api/land';
import { usePropertyRpus } from '../../../api/rpus';
import { useActualUses, useClassifications, useImprovementKinds, useRoadTypes, useSubClassifications, useZones } from '../../../api/referenceData';
import { ApiRequestError } from '../../../lib/apiClient';
import type { LandDto } from '../../../lib/types';

const plain = new Intl.NumberFormat('en-PH', { maximumFractionDigits: 4 });
const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);

type Dialog = 'strip' | 'improvement' | 'adjustment' | 'inputs' | null;

/**
 * The land's appraisal rows (MRPAAO Att. 1; docs/analysis/mrpaao-forms-model.md
 * §8.3): strips each valued at their own SMV rate, trees and plants at the
 * rate for their kind, and adjustment factors named by code — their
 * percentage is the one in force under the SMV when the land is valued.
 */
export function LandAppraisalRows({ land, rpuId, propertyId }: { land: LandDto; rpuId: string; propertyId: string }) {
  const [dialog, setDialog] = useState<Dialog>(null);
  const section = (title: string, kind: Exclude<Dialog, null>, label: string) => (
    <Space style={{ margin: '12px 0 6px', justifyContent: 'space-between', width: '100%' }}>
      <Typography.Text strong>{title}</Typography.Text>
      <Button size="small" icon={kind === 'inputs' ? <EditOutlined /> : <PlusOutlined />} onClick={() => setDialog(kind)}>{label}</Button>
    </Space>
  );
  const stripLabel = (id: string | null) => (id === null ? 'All strips' : `Strip ${land.strips.find((s) => s.id === id)?.sequence ?? '?'}`);
  const { data: roadTypes = [] } = useRoadTypes();
  const km = (v: number | null) => (v === null ? '—' : `${plain.format(v)} km`);

  return (
    <div style={{ marginBottom: 8 }}>
      {section('What the adjustment factors read', 'inputs', 'Edit')}
      <Descriptions size="small" bordered column={{ xs: 1, md: 3 }}>
        <Descriptions.Item label="Road">
          {roadTypes.find((r) => r.id === land.roadTypeId)?.name ?? '—'}{land.roadFrontage !== null ? `, ${plain.format(land.roadFrontage)} m frontage` : ''}
        </Descriptions.Item>
        <Descriptions.Item label="Corner lot">{land.isCornerLot ? 'Yes' : 'No'}</Descriptions.Item>
        <Descriptions.Item label="Subdivision lot">{land.isSubdivisionLot ? 'Yes (no depth adjustment)' : 'No'}</Descriptions.Item>
        <Descriptions.Item label="To an all-weather road">{km(land.distanceToAllWeatherRoadKm)}</Descriptions.Item>
        <Descriptions.Item label="To the poblacion">{km(land.distanceToPoblacionKm)}</Descriptions.Item>
      </Descriptions>

      {section('Land strips', 'strip', 'Add strip')}
      <Table size="small" rowKey="id" dataSource={land.strips} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'Valued from the land record itself (no strips)' }}
        columns={[
          { title: '#', dataIndex: 'sequence', width: 40 },
          { title: 'Classification', render: (_, s) => [s.classificationName, s.subClassificationName].filter(Boolean).join(' / ') },
          { title: 'Actual use', dataIndex: 'actualUseName' },
          {
            // The class that prices the strip when it differs from the one that assesses it (valuation-foundation.md §4.3).
            title: 'Priced as', render: (_, s) => (s.valuationClassificationName || s.valuationSubClassificationName
              ? [s.valuationClassificationName ?? s.classificationName, s.valuationSubClassificationName].filter(Boolean).join(' / ')
              : <Typography.Text type="secondary">its own</Typography.Text>),
          },
          { title: 'Zone', dataIndex: 'zoneName', render: (v: string | null) => v ?? '(land’s)' },
          { title: 'Depth band', dataIndex: 'depthBand', render: (v: number | null) => v ?? '—' },
          { title: 'Area', align: 'right', render: (_, s) => `${plain.format(s.area)} ${land.areaUnit}` },
        ]} />

      {section('Trees, plants and other improvements', 'improvement', 'Add improvement')}
      <Table size="small" rowKey="id" dataSource={land.improvements} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'None recorded' }}
        columns={[
          { title: '#', dataIndex: 'sequence', width: 40 },
          { title: 'Kind', dataIndex: 'improvementKindName' },
          { title: 'Number', dataIndex: 'quantity', align: 'right', render: (v: number) => plain.format(v) },
          { title: 'Productive', dataIndex: 'isProductive', render: (v: boolean | null) => (v === null ? '—' : v ? 'Yes' : 'No') },
          { title: 'Assessed under', render: (_, i) => (i.actualUseName ? `${i.classificationName ?? ''} / ${i.actualUseName}` : 'Principal strip’s use') },
          { title: 'Owned by', dataIndex: 'separateRpuNumber', render: (v: string | null) => (v ? `Unit ${v} (valued apart)` : 'The land') },
        ]} />

      {section('Market value adjustments', 'adjustment', 'Add adjustment')}
      <Table size="small" rowKey="id" dataSource={land.adjustments} pagination={false} scroll={{ x: true }}
        locale={{ emptyText: 'None' }}
        columns={[
          { title: 'Factor', dataIndex: 'factorCode' },
          { title: 'Applies to', render: (_, a) => stripLabel(a.landStripId) },
          { title: 'Remarks', dataIndex: 'remarks', render: (v: string | null) => v ?? '—' },
        ]} />

      {dialog === 'strip' && <StripDialog land={land} rpuId={rpuId} propertyId={propertyId} onClose={() => setDialog(null)} />}
      {dialog === 'improvement' && <ImprovementDialog land={land} rpuId={rpuId} propertyId={propertyId} onClose={() => setDialog(null)} />}
      {dialog === 'adjustment' && <AdjustmentDialog land={land} rpuId={rpuId} propertyId={propertyId} onClose={() => setDialog(null)} />}
      {dialog === 'inputs' && <InputsDialog land={land} rpuId={rpuId} propertyId={propertyId} onClose={() => setDialog(null)} />}
    </div>
  );
}

type DialogProps = { land: LandDto; rpuId: string; propertyId: string; onClose: () => void };

function StripDialog({ land, rpuId, propertyId, onClose }: DialogProps) {
  const add = useAddLandStrip(land.id, rpuId, propertyId);
  const { data: classifications = [] } = useClassifications();
  const { data: subs = [] } = useSubClassifications();
  const { data: uses = [] } = useActualUses();
  const { data: zones = [] } = useZones();
  return (
    <Modal open title="Add land strip" footer={null} onCancel={onClose} destroyOnHidden>
      {add.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not add" description={errorText(add.error)} />}
      {land.strips.length === 0 && (
        <Alert type="info" showIcon style={{ marginBottom: 12 }} title={`The registered ${plain.format(land.area)} ${land.areaUnit} becomes strip 1.`} />
      )}
      <Form layout="vertical" onFinish={(v) => add.mutate(
        {
          classificationId: v.classificationId, subClassificationId: v.subClassificationId ?? null, actualUseId: v.actualUseId, zoneId: v.zoneId ?? null, area: v.area,
          valuationClassificationId: v.valuationClassificationId ?? null, valuationSubClassificationId: v.valuationSubClassificationId ?? null,
          depthBand: v.depthBand ?? null,
        },
        { onSuccess: onClose })}>
        <Form.Item name="classificationId" label="Classification" rules={[{ required: true }]}>
          <Select options={classifications.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Form.Item name="subClassificationId" label="Sub-classification">
          <Select allowClear options={subs.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Form.Item name="actualUseId" label="Actual use" rules={[{ required: true }]}>
          <Select options={uses.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Form.Item name="zoneId" label="Zone" extra="Leave empty to use the land's zone.">
          <Select allowClear options={zones.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Form.Item name="area" label={`Area (${land.areaUnit})`} rules={[{ required: true }]}>
          <InputNumber<number> min={0.0001} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="depthBand" label="Depth band" extra="For a depth factor: the band beyond the standard depth (1, 2, …). Leave empty for the standard strip.">
          <InputNumber<number> min={1} max={50} precision={0} style={{ width: '100%' }} />
        </Form.Item>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
          Priced by another class (optional): e.g. agricultural land in a predominantly commercial area, valued at the commercial unit value
          but assessed at the level of its own class and actual use.
        </Typography.Paragraph>
        <Form.Item name="valuationClassificationId" label="Priced as classification" extra="Leave empty to price the strip by its own classification.">
          <Select allowClear options={classifications.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Form.Item name="valuationSubClassificationId" label="Priced as sub-classification">
          <Select allowClear options={subs.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={add.isPending}>Add strip</Button>
      </Form>
    </Modal>
  );
}

function ImprovementDialog({ land, rpuId, propertyId, onClose }: DialogProps) {
  const add = useAddLandImprovement(land.id, rpuId, propertyId);
  const { data: kinds = [] } = useImprovementKinds();
  const { data: classifications = [] } = useClassifications();
  const { data: uses = [] } = useActualUses();
  // Other-improvement units standing on this land may own trees and plants apart from it (§4.4).
  const { data: units = [] } = usePropertyRpus(propertyId);
  const owners = units.filter((u) => u.rpuType === 'OtherImprovement' && u.landRpuId === rpuId);
  return (
    <Modal open title="Add tree, plant or other improvement" footer={null} onCancel={onClose} destroyOnHidden>
      {add.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not add" description={errorText(add.error)} />}
      {kinds.length === 0 && (
        <Alert type="info" showIcon style={{ marginBottom: 12 }} title="No improvement kinds are configured"
          description="Kinds (and their SMV rates) come from the LGU's Schedule of Market Values." />
      )}
      <Form layout="vertical" onFinish={(v) => add.mutate(
        {
          improvementKindId: v.improvementKindId, quantity: v.quantity, isProductive: v.isProductive ?? null,
          classificationId: v.classificationId ?? null, actualUseId: v.actualUseId ?? null, description: v.description ?? null,
          separateRpuId: v.separateRpuId ?? null,
        },
        { onSuccess: onClose })}>
        <Form.Item name="improvementKindId" label="Kind" rules={[{ required: true }]}>
          <Select options={kinds.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Form.Item name="quantity" label="Number" rules={[{ required: true }]}>
          <InputNumber<number> min={0.0001} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="isProductive" label="Productive" extra="Only where the SMV distinguishes productive and non-productive.">
          <Select allowClear options={[{ value: true, label: 'Productive' }, { value: false, label: 'Non-productive' }]} />
        </Form.Item>
        <Form.Item name="classificationId" label="Classification" extra="Leave both empty to assess under the principal strip's.">
          <Select allowClear options={classifications.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Form.Item name="actualUseId" label="Actual use">
          <Select allowClear options={uses.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Form.Item name="description" label="Description"><Input maxLength={500} /></Form.Item>
        <Form.Item name="separateRpuId" label="Owned by"
          extra={owners.length === 0 ? 'To record trees owned apart from the land, first add an other-improvement unit on this land.' : 'Valued and assessed under that unit, not with the land.'}>
          <Select allowClear placeholder="The land" disabled={owners.length === 0} options={owners.map((u) => ({ value: u.id, label: `Unit ${u.rpuNumber}` }))} />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={add.isPending}>Add improvement</Button>
      </Form>
    </Modal>
  );
}

function AdjustmentDialog({ land, rpuId, propertyId, onClose }: DialogProps) {
  const add = useAddLandAdjustment(land.id, rpuId, propertyId);
  const { data: factors = [] } = useAdjustmentFactors();
  const codes = [...new Map(factors.filter((f) => f.status === 'Approved').map((f) => [f.code, f])).values()];
  return (
    <Modal open title="Add market value adjustment" footer={null} onCancel={onClose} destroyOnHidden>
      {add.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not add" description={errorText(add.error)} />}
      <Typography.Paragraph type="secondary">
        The percentage is not fixed here: valuation uses the factor of this code in force under the SMV that prices each strip.
      </Typography.Paragraph>
      <Form layout="vertical" onFinish={(v) => add.mutate({ factorCode: v.factorCode, landStripId: v.landStripId ?? null, remarks: v.remarks ?? null }, { onSuccess: onClose })}>
        <Form.Item name="factorCode" label="Factor" rules={[{ required: true }]}>
          <Select options={codes.map((f) => ({ value: f.code, label: `${f.code} — ${f.name}` }))}
            notFoundContent="No approved adjustment factors (configured per SMV)" />
        </Form.Item>
        <Form.Item name="landStripId" label="Applies to">
          <Select allowClear placeholder="All strips" options={land.strips.map((s) => ({ value: s.id, label: `Strip ${s.sequence}` }))} />
        </Form.Item>
        <Form.Item name="remarks" label="Remarks"><Input maxLength={500} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={add.isPending}>Add adjustment</Button>
      </Form>
    </Modal>
  );
}

/** The facts the adjustment factors read, changed with a reason (audited); valuations already made keep their values. */
function InputsDialog({ land, rpuId, propertyId, onClose }: DialogProps) {
  const update = useUpdateLandAppraisalInputs(land.id, rpuId, propertyId, land.rowVersion);
  const { data: roadTypes = [] } = useRoadTypes();
  return (
    <Modal open title="What the adjustment factors read" footer={null} onCancel={onClose} destroyOnHidden>
      {update.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not saved" description={errorText(update.error)} />}
      <Form layout="vertical" initialValues={{
        roadTypeId: land.roadTypeId ?? undefined, roadFrontage: land.roadFrontage ?? undefined, isCornerLot: land.isCornerLot,
        distanceToAllWeatherRoadKm: land.distanceToAllWeatherRoadKm ?? undefined, distanceToPoblacionKm: land.distanceToPoblacionKm ?? undefined,
        isSubdivisionLot: land.isSubdivisionLot,
      }} onFinish={(v) => update.mutate({
        roadTypeId: v.roadTypeId ?? null, roadFrontage: v.roadFrontage ?? null, isCornerLot: !!v.isCornerLot,
        distanceToAllWeatherRoadKm: v.distanceToAllWeatherRoadKm ?? null, distanceToPoblacionKm: v.distanceToPoblacionKm ?? null,
        isSubdivisionLot: !!v.isSubdivisionLot, reason: v.reason.trim(),
      }, { onSuccess: onClose })}>
        <Form.Item name="roadTypeId" label="Kind of road"><Select allowClear options={roadTypes.map((r) => ({ value: r.id, label: r.name }))} /></Form.Item>
        <Form.Item name="roadFrontage" label="Road frontage (m)"><InputNumber<number> min={0} style={{ width: '100%' }} /></Form.Item>
        <Space wrap size="large">
          <Form.Item name="isCornerLot" label="Corner lot" valuePropName="checked"><Switch /></Form.Item>
          <Form.Item name="isSubdivisionLot" label="Subdivision lot" valuePropName="checked"><Switch /></Form.Item>
        </Space>
        <Form.Item name="distanceToAllWeatherRoadKm" label="Distance to an all-weather road (km)"><InputNumber<number> min={0} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="distanceToPoblacionKm" label="Distance to the poblacion (km)"><InputNumber<number> min={0} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="reason" label="Reason for the change" rules={[{ required: true, whitespace: true, message: 'Give the reason' }]}>
          <Input maxLength={500} placeholder="e.g. ocular inspection of 2026-10-01" />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={update.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}
