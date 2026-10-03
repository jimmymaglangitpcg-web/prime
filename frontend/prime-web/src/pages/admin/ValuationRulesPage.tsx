import { useState } from 'react';
import { Alert, Button, Card, Col, DatePicker, Descriptions, Form, Input, InputNumber, Modal, Row, Select, Space, Table, Tabs, Tag, Typography } from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import {
  useAllAdjustmentFactors, useApproveAdjustmentFactor, useApproveAssessmentLevel, useApproveAssessmentLevelCeiling, useAssessmentLevelCeilings, useCreateAssessmentLevelCeiling, useApproveSmv, useApproveSmvSchedule, useAssessmentLevels,
  useCreateAdjustmentFactor, useCreateAssessmentLevel, useCreateSmv, useCreateSmvSchedule, useSmvSchedules, useSmvs,
} from '../../api/valuation';
import {
  useActualUses, useAllMunicipalities, useBarangays, useClassifications, useImprovementKinds, usePropertyTypes, useRoadTypes, useSubClassifications,
  useZones,
} from '../../api/referenceData';
import { formatMoney } from '../../lib/format';
import {
  adjustmentRuleKinds, distanceReferences, type AdjustmentFactorDto, type AdjustmentRuleKind, type AssessmentLevelCeilingDto, type AssessmentLevelDto, type SmvBasis,
  type SmvDto, type SmvScheduleDto,
} from '../../lib/types';
import { day, errorText, lookup, pct, period, statusTag, useToast } from './ruleHelpers';
import { ApproveButton } from './ApproveButton';
import { BuildingCostsTab } from './BuildingCostsTab';
import { MachineryIndicesTab } from './MachineryIndicesTab';

/**
 * The rules valuation and assessment use (docs/analysis/value-and-assess.md §3):
 * SMVs (certified or by ordinance, with their coverage) and their unit values, adjustment
 * factors and assessment levels (docs/analysis/valuation-foundation.md §4.3). Rules
 * are created and approved, never edited; a change is a new version from its
 * effective date. Every value comes from the LGU's ordinance.
 */
export function ValuationRulesPage() {
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Title level={3} style={{ margin: 0 }}>Valuation Rules</Typography.Title>
      <Alert type="warning" showIcon title="Enter only values from the certified SMV or the LGU's ordinances"
        description="PRIME invents no market values or assessment levels. Rules are never edited: a change is a new version that takes over from its effective date. Another user approves what you create." />
      <Tabs items={[
        { key: 'smv', label: 'Schedules of Market Values', children: <SmvTab /> },
        { key: 'factors', label: 'Adjustment factors', children: <FactorsTab /> },
        { key: 'buildings', label: 'Building costs', children: <BuildingCostsTab /> },
        { key: 'machinery', label: 'Machinery indices', children: <MachineryIndicesTab /> },
        { key: 'levels', label: 'Assessment levels', children: <LevelsTab /> },
        { key: 'ceilings', label: 'Level ceilings', children: <CeilingsTab /> },
      ]} />
    </Space>
  );
}

// ---------------- SMVs and schedules ----------------

/** The stages a certified SMV goes through (docs/analysis/valuation-foundation.md §4.3); all optional records. */
const stages: { name: keyof SmvDto & string; label: string }[] = [
  { name: 'proposedOn', label: 'Proposed' },
  { name: 'publishedForCommentOn', label: 'Published for comment' },
  { name: 'consultationsHeldOn', label: 'Consultations held' },
  { name: 'submittedToBlgfOn', label: 'Submitted to the BLGF' },
  { name: 'certifiedOn', label: 'Certified' },
  { name: 'publishedOn', label: 'Published' },
];

const coverageText = (s: SmvDto) => (s.coverage.length === 0 ? 'Whole province' : s.coverage.map((c) => c.municipalityName).join(', '));

function SmvTab() {
  const { data, isLoading } = useSmvs();
  const approve = useApproveSmv();
  const { context, fail } = useToast();
  const [creating, setCreating] = useState(false);
  const [open, setOpen] = useState<SmvDto | null>(null);
  return (
    <Card title="SMVs" extra={<Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>New SMV</Button>}>
      {context}
      <Typography.Paragraph type="secondary">
        Approving an SMV here confirms it was entered correctly; it does not stand in for its certification or ordinance.
        The engine uses the latest SMV in force that covers the property&apos;s municipality.
      </Typography.Paragraph>
      <Table<SmvDto> rowKey="id" size="small" loading={isLoading} dataSource={data?.items ?? []} pagination={false} scroll={{ x: true }}
        expandable={{ expandedRowRender: (s) => <SmvStages smv={s} /> }}
        columns={[
          { title: 'Reference', dataIndex: 'reference' },
          { title: 'Basis', dataIndex: 'basis', render: (b: SmvBasis) => <Tag color={b === 'Certified' ? 'blue' : 'default'}>{b}</Tag> },
          { title: 'Effective', dataIndex: 'effectivityDate' },
          { title: 'Revision year', dataIndex: 'revisionYear' },
          { title: 'Coverage', render: (_, s) => coverageText(s) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          {
            title: '', render: (_, s) => (
              <Space>
                <Button size="small" onClick={() => setOpen(s)}>Unit values</Button>
                <ApproveButton status={s.status} pending={approve.isPending} onApprove={() => approve.mutate(s.id, { onError: fail })} />
              </Space>
            ),
          },
        ]} />
      {creating && <CreateSmvModal onClose={() => setCreating(false)} />}
      {open && <SchedulesModal smv={open} onClose={() => setOpen(null)} />}
    </Card>
  );
}

function SmvStages({ smv: s }: { smv: SmvDto }) {
  return (
    <Descriptions size="small" column={{ xs: 1, md: 3 }}>
      {s.basis === 'Ordinance' && <Descriptions.Item label="Ordinance">{s.ordinanceNumber} ({s.ordinanceDate}{s.approvalDate ? `, approved ${s.approvalDate}` : ''})</Descriptions.Item>}
      {s.certificationReference && <Descriptions.Item label="Certification">{s.certificationReference}</Descriptions.Item>}
      {stages.map((st) => <Descriptions.Item key={st.name} label={st.label}>{(s[st.name] as string | null) ?? '—'}</Descriptions.Item>)}
      <Descriptions.Item label="Publication">{s.publicationReference ?? '—'}</Descriptions.Item>
      <Descriptions.Item label="Description" span="filled">{s.description ?? '—'}</Descriptions.Item>
    </Descriptions>
  );
}

function CreateSmvModal({ onClose }: { onClose: () => void }) {
  const create = useCreateSmv();
  const [form] = Form.useForm();
  const basis = (Form.useWatch('basis', form) as SmvBasis | undefined) ?? 'Certified';
  const { data: municipalities = [] } = useAllMunicipalities();
  return (
    <Modal open title="New SMV" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} width={760} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" initialValues={{ basis: 'Certified', municipalityIds: [] }} onFinish={(v) => create.mutate({
        basis: v.basis,
        ordinanceNumber: v.basis === 'Ordinance' ? v.ordinanceNumber : null, ordinanceDate: v.basis === 'Ordinance' ? day(v.ordinanceDate) : null,
        approvalDate: v.basis === 'Ordinance' ? day(v.approvalDate) : null,
        certificationReference: v.basis === 'Certified' ? v.certificationReference : null,
        effectivityDate: day(v.effectivityDate)!, revisionYear: v.revisionYear, description: v.description || null,
        publicationReference: v.publicationReference || null, municipalityIds: v.municipalityIds ?? [],
        ...Object.fromEntries(stages.map((st) => [st.name, day(v[st.name])])),
      }, { onSuccess: onClose })}>
        <Row gutter={12}>
          <Col xs={24} md={8}>
            <Form.Item name="basis" label="Basis" extra="RA 12001: certified by the Secretary of Finance. Earlier SMVs: by ordinance.">
              <Select options={[{ value: 'Certified', label: 'Certified (RA 12001)' }, { value: 'Ordinance', label: 'Ordinance' }]} />
            </Form.Item>
          </Col>
          {basis === 'Certified' ? (
            <Col xs={24} md={16}>
              <Form.Item name="certificationReference" label="Certification reference" rules={[{ required: true }, { max: 100 }]}><Input /></Form.Item>
            </Col>
          ) : (
            <>
              <Col xs={24} md={6}><Form.Item name="ordinanceNumber" label="Ordinance No." rules={[{ required: true }, { max: 50 }]}><Input /></Form.Item></Col>
              <Col xs={12} md={5}><Form.Item name="ordinanceDate" label="Ordinance date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
              <Col xs={12} md={5}><Form.Item name="approvalDate" label="Approved"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
            </>
          )}
          <Col xs={12} md={8}><Form.Item name="effectivityDate" label="Effective" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={12} md={8}><Form.Item name="revisionYear" label="Revision year" rules={[{ required: true }]}><InputNumber min={1900} max={2200} precision={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={24} md={8}><Form.Item name="publicationReference" label="Published in"><Input maxLength={200} placeholder="Official Gazette, newspaper" /></Form.Item></Col>
          {stages.map((st) => (
            <Col key={st.name} xs={12} md={8}><Form.Item name={st.name} label={st.label}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          ))}
          <Col span={24}>
            <Form.Item name="municipalityIds" label="Coverage" extra="Leave empty for the whole province. A change of coverage is a new SMV.">
              <Select mode="multiple" allowClear showSearch optionFilterProp="label" placeholder="Whole province"
                options={municipalities.map((m) => ({ value: m.id, label: m.name }))} />
            </Form.Item>
          </Col>
          <Col span={24}><Form.Item name="description" label="Description"><Input maxLength={1000} /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  );
}

function SchedulesModal({ smv, onClose }: { smv: SmvDto; onClose: () => void }) {
  const { data = [], isLoading } = useSmvSchedules(smv.id);
  const approve = useApproveSmvSchedule(smv.id);
  const { context, fail } = useToast();
  const [creating, setCreating] = useState(false);
  return (
    <Modal open title={`Unit values of SMV ${smv.reference}`} onCancel={onClose} footer={null} width={1100} destroyOnHidden>
      {context}
      <Typography.Paragraph type="secondary">
        A land&apos;s unit value is found in this order: sub-class + zone, sub-class + barangay, sub-class, zone, barangay, neither.
        At each step a value naming the land&apos;s actual use comes before one that does not. Coverage: {coverageText(smv)}.
      </Typography.Paragraph>
      <Button icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => setCreating(true)}>New unit value</Button>
      <Table<SmvScheduleDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
        columns={[
          { title: 'Classification', dataIndex: 'classificationName' },
          { title: 'Sub-class', dataIndex: 'subClassificationName', render: (v: string | null) => v ?? 'any' },
          { title: 'Actual use', dataIndex: 'actualUseName', render: (v: string | null) => v ?? 'any' },
          { title: 'Type', dataIndex: 'propertyTypeName' },
          { title: 'Zone', dataIndex: 'zoneName', render: (v: string | null) => v ?? 'any' },
          { title: 'Barangay', dataIndex: 'barangayName', render: (v: string | null) => v ?? 'any' },
          { title: 'Improvement', dataIndex: 'improvementKindName', render: (v: string | null) => v ?? '—' },
          { title: 'Market value', align: 'right', render: (_, s) => `${formatMoney(s.marketValue)} / ${s.unit}` },
          { title: 'Period', render: (_, s) => period(s.effectiveDate, s.endDate) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          { title: '', render: (_, s) => <ApproveButton status={s.status} pending={approve.isPending} onApprove={() => approve.mutate(s.id, { onError: fail })} /> },
        ]} />
      {creating && <CreateScheduleModal smv={smv} onClose={() => setCreating(false)} />}
    </Modal>
  );
}

function CreateScheduleModal({ smv, onClose }: { smv: SmvDto; onClose: () => void }) {
  const create = useCreateSmvSchedule(smv.id);
  const [form] = Form.useForm();
  const { data: classifications = [] } = useClassifications();
  const { data: subClasses = [] } = useSubClassifications();
  const { data: actualUses = [] } = useActualUses();
  const { data: propertyTypes = [] } = usePropertyTypes();
  const { data: zones = [] } = useZones();
  const { data: kinds = [] } = useImprovementKinds();
  const { data: allMunicipalities = [] } = useAllMunicipalities();
  // A barangay value is for a barangay the SMV covers.
  const towns = smv.coverage.length > 0 ? smv.coverage.map((c) => ({ id: c.municipalityId, name: c.municipalityName })) : allMunicipalities;
  const town = Form.useWatch('municipalityId', form) as string | undefined;
  const { data: barangays = [] } = useBarangays(town);
  return (
    <Modal open title="New unit value" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} width={760} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" initialValues={{ unit: 'per sqm' }} onFinish={(v) => create.mutate({
        classificationId: v.classificationId, subClassificationId: v.subClassificationId ?? null, actualUseId: v.actualUseId ?? null,
        propertyTypeId: v.propertyTypeId, zoneId: v.zoneId ?? null, barangayId: v.barangayId ?? null,
        unit: v.unit, marketValue: v.marketValue, minimumValue: v.minimumValue ?? null, maximumValue: v.maximumValue ?? null,
        effectiveDate: day(v.effectiveDate)!, improvementKindId: v.improvementKindId ?? null,
      }, { onSuccess: onClose })}>
        <Row gutter={12}>
          <Col xs={24} md={8}><Form.Item name="classificationId" label="Classification" rules={[{ required: true }]}><Select showSearch optionFilterProp="label" options={lookup(classifications)} /></Form.Item></Col>
          <Col xs={12} md={8}><Form.Item name="subClassificationId" label="Sub-class" extra="Blank: any"><Select allowClear showSearch optionFilterProp="label" options={lookup(subClasses)} /></Form.Item></Col>
          <Col xs={12} md={8}><Form.Item name="propertyTypeId" label="Property type" rules={[{ required: true }]}><Select options={lookup(propertyTypes)} /></Form.Item></Col>
          <Col xs={24} md={8}><Form.Item name="actualUseId" label="Actual use" extra="Blank: any. The actual use decides the level."><Select allowClear showSearch optionFilterProp="label" options={lookup(actualUses)} /></Form.Item></Col>
          <Col xs={12} md={8}><Form.Item name="zoneId" label="Zone" extra="Blank: any"><Select allowClear showSearch optionFilterProp="label" options={lookup(zones)} /></Form.Item></Col>
          <Col xs={12} md={8}><Form.Item name="improvementKindId" label="Improvement kind" extra="For trees and plants"><Select allowClear showSearch optionFilterProp="label" options={lookup(kinds)} /></Form.Item></Col>
          <Col xs={12} md={8}>
            <Form.Item name="municipalityId" label="Municipality (for a barangay value)">
              <Select allowClear showSearch optionFilterProp="label" options={towns.map((m) => ({ value: m.id, label: m.name }))}
                onChange={() => form.setFieldValue('barangayId', undefined)} />
            </Form.Item>
          </Col>
          <Col xs={12} md={8}><Form.Item name="barangayId" label="Barangay" extra="Blank: any"><Select allowClear showSearch optionFilterProp="label" disabled={!town} options={barangays.map((b) => ({ value: b.id, label: b.name }))} /></Form.Item></Col>
          <Col xs={12} md={8}><Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="unit" label="Unit" rules={[{ required: true }]}><Input maxLength={20} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="marketValue" label="Unit value" rules={[{ required: true }]}><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="minimumValue" label="Minimum"><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="maximumValue" label="Maximum"><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  );
}

// ---------------- Adjustment factors ----------------

/** A factor's table in words: "Paved 0%; Dirt −10%", "≤ 2 km 0%; 2–5 km −5%", "band 1 −20%". */
function factorRule(f: AdjustmentFactorDto) {
  const rows = f.rows.map((r) => {
    if (r.roadTypeName) return `${r.roadTypeName} ${pct(r.percent)}`;
    if (r.depthBand !== null) return `band ${r.depthBand} ${pct(r.percent)}`;
    const band = r.overValue === null ? `≤ ${r.upToValue} km` : r.upToValue === null ? `> ${r.overValue} km` : `${r.overValue}–${r.upToValue} km`;
    return `${band} ${pct(r.percent)}`;
  });
  switch (f.ruleKind) {
    case 'Flat': return pct(f.percent);
    case 'Corner': return `${pct(f.percent)} on corner lots`;
    case 'ByDistance': return `${distanceReferences.find((d) => d.value === f.distanceReference)?.label ?? ''}: ${rows.join('; ')}`;
    case 'Depth': return `beyond ${f.standardDepth} m: ${rows.join('; ')}`;
    default: return rows.join('; ');
  }
}

function FactorsTab() {
  const { data = [], isLoading } = useAllAdjustmentFactors();
  const approve = useApproveAdjustmentFactor();
  const { context, fail } = useToast();
  const [creating, setCreating] = useState(false);
  return (
    <Card title="Adjustment factors" extra={<Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>New factor</Button>}>
      {context}
      <Typography.Paragraph type="secondary">
        The appraiser names a factor on a land; its rule takes the percentage from the land&apos;s road, corner status, distance or the strip&apos;s depth band.
        Several factors on one strip add up.
      </Typography.Paragraph>
      <Table<AdjustmentFactorDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
        columns={[
          { title: 'SMV', dataIndex: 'smvOrdinanceNumber' },
          { title: 'Code', dataIndex: 'code' },
          { title: 'Name', dataIndex: 'name' },
          { title: 'Rule', dataIndex: 'ruleKind', render: (k: AdjustmentRuleKind) => adjustmentRuleKinds.find((r) => r.value === k)?.label.split(' (')[0] ?? k },
          { title: 'Adjustment', render: (_, f) => factorRule(f) },
          { title: 'Classification', dataIndex: 'classificationName', render: (v: string | null) => v ?? 'all' },
          { title: 'Legal basis', dataIndex: 'legalBasis' },
          { title: 'Period', render: (_, f) => period(f.effectiveDate, f.endDate) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          { title: '', render: (_, f) => <ApproveButton status={f.status} pending={approve.isPending} onApprove={() => approve.mutate(f.id, { onError: fail })} /> },
        ]} />
      {creating && <CreateFactorModal onClose={() => setCreating(false)} />}
    </Card>
  );
}

interface FactorRowForm { roadTypeId?: string; overValue?: number; upToValue?: number; depthBand?: number; percent?: number }

function CreateFactorModal({ onClose }: { onClose: () => void }) {
  const create = useCreateAdjustmentFactor();
  const [form] = Form.useForm();
  const { data: smvs } = useSmvs();
  const { data: classifications = [] } = useClassifications();
  const { data: roadTypes = [] } = useRoadTypes();
  const kind = (Form.useWatch('ruleKind', form) as AdjustmentRuleKind | undefined) ?? 'Flat';
  const usesPercent = kind === 'Flat' || kind === 'Corner';
  return (
    <Modal open title="New adjustment factor" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} width={760} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" initialValues={{ ruleKind: 'Flat', rows: [] }} onFinish={(v) => create.mutate({
        smvId: v.smvId, code: v.code, name: v.name, percent: usesPercent ? v.percent : 0, classificationId: v.classificationId ?? null,
        description: v.description || null, legalBasis: v.legalBasis, effectiveDate: day(v.effectiveDate)!, remarks: null,
        ruleKind: v.ruleKind, distanceReference: v.ruleKind === 'ByDistance' ? v.distanceReference : null,
        standardDepth: v.ruleKind === 'Depth' ? v.standardDepth : null,
        rows: usesPercent ? [] : (v.rows ?? []).map((r: FactorRowForm) => ({
          roadTypeId: kind === 'ByRoadType' ? r.roadTypeId ?? null : null,
          overValue: kind === 'ByDistance' ? r.overValue ?? null : null, upToValue: kind === 'ByDistance' ? r.upToValue ?? null : null,
          depthBand: kind === 'Depth' ? r.depthBand ?? null : null, percent: r.percent ?? 0,
        })),
      }, { onSuccess: onClose })}>
        <Row gutter={12}>
          <Col xs={24} md={12}><Form.Item name="smvId" label="SMV" rules={[{ required: true }]}>
            <Select showSearch optionFilterProp="label" options={(smvs?.items ?? []).map((s) => ({ value: s.id, label: `${s.reference} (${s.revisionYear})` }))} />
          </Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="classificationId" label="Classification" extra={kind === 'Depth' ? 'Depth applies to the class named here (residential).' : 'Blank: all'}>
            <Select allowClear showSearch optionFilterProp="label" options={lookup(classifications)} />
          </Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="code" label="Code" rules={[{ required: true }, { max: 20 }]}><Input /></Form.Item></Col>
          <Col xs={12} md={18}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input maxLength={200} /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="ruleKind" label="Rule"><Select options={adjustmentRuleKinds} /></Form.Item></Col>
          {usesPercent && (
            <Col xs={12} md={6}><Form.Item name="percent" label="Adjustment %" rules={[{ required: true }]}><InputNumber precision={4} style={{ width: '100%' }} /></Form.Item></Col>
          )}
          {kind === 'ByDistance' && (
            <Col xs={24} md={12}><Form.Item name="distanceReference" label="Distance" rules={[{ required: true }]}><Select options={distanceReferences} /></Form.Item></Col>
          )}
          {kind === 'Depth' && (
            <Col xs={12} md={6}><Form.Item name="standardDepth" label="Standard depth (m)" rules={[{ required: true }]}><InputNumber min={0.001} style={{ width: '100%' }} /></Form.Item></Col>
          )}
        </Row>
        {!usesPercent && (
          <Form.List name="rows">
            {(fields, { add, remove }) => (
              <>
                <Typography.Text strong>
                  {kind === 'ByRoadType' ? 'Percent per kind of road' : kind === 'ByDistance' ? 'Percent per distance band (over … not over …, km)' : 'Percent per depth band'}
                </Typography.Text>
                {fields.map((field) => (
                  <Space key={field.key} align="baseline" wrap style={{ display: 'flex' }}>
                    {kind === 'ByRoadType' && (
                      <Form.Item name={[field.name, 'roadTypeId']} rules={[{ required: true, message: 'Road type' }]}>
                        <Select aria-label="Road type" placeholder="Road type" style={{ width: 200 }} options={lookup(roadTypes)} />
                      </Form.Item>
                    )}
                    {kind === 'ByDistance' && (
                      <>
                        <Form.Item name={[field.name, 'overValue']}><InputNumber aria-label="Over km" placeholder="over (km)" min={0} style={{ width: 120 }} /></Form.Item>
                        <Form.Item name={[field.name, 'upToValue']}><InputNumber aria-label="Up to km" placeholder="not over (km)" min={0} style={{ width: 130 }} /></Form.Item>
                      </>
                    )}
                    {kind === 'Depth' && (
                      <Form.Item name={[field.name, 'depthBand']} rules={[{ required: true, message: 'Band' }]}>
                        <InputNumber aria-label="Depth band" placeholder="band" min={1} precision={0} style={{ width: 100 }} />
                      </Form.Item>
                    )}
                    <Form.Item name={[field.name, 'percent']} rules={[{ required: true, message: '%' }]}>
                      <InputNumber aria-label="Row percent" placeholder="%" precision={4} style={{ width: 110 }} />
                    </Form.Item>
                    <MinusCircleOutlined aria-label="Remove row" onClick={() => remove(field.name)} />
                  </Space>
                ))}
                <Button type="dashed" icon={<PlusOutlined />} onClick={() => add()} style={{ margin: '4px 0 12px' }}>Add row</Button>
              </>
            )}
          </Form.List>
        )}
        <Row gutter={12}>
          <Col xs={24} md={16}><Form.Item name="legalBasis" label="Legal basis" rules={[{ required: true }]}><Input maxLength={500} /></Form.Item></Col>
          <Col xs={24} md={8}><Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={24}><Form.Item name="description" label="Description"><Input maxLength={500} /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  );
}

// ---------------- Assessment levels ----------------

interface LevelGroup { key: string; label: string; levels: AssessmentLevelDto[] }

function LevelsTab() {
  const { data = [], isLoading } = useAssessmentLevels();
  const approve = useApproveAssessmentLevel();
  const { context, fail } = useToast();
  const [creating, setCreating] = useState(false);
  // The brackets of one classification / actual use / property type, together.
  const groups: LevelGroup[] = Object.values(data.reduce<Record<string, LevelGroup>>((acc, l) => {
    const key = `${l.classificationId}|${l.actualUseId}|${l.propertyTypeId}`;
    (acc[key] ??= { key, label: `${l.classificationName} / ${l.actualUseName} / ${l.propertyTypeName}`, levels: [] }).levels.push(l);
    return acc;
  }, {}));
  return (
    <Card title="Assessment levels" extra={<Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>New level</Button>}>
      {context}
      <Typography.Paragraph type="secondary">
        A value falls in a bracket when it is over the lower value and not over the upper value (a lower value of 0 includes 0) —
        DOMAIN VERIFICATION REQUIRED against the ordinance. Brackets with separate ranges stay in force side by side.
      </Typography.Paragraph>
      {/* Rendered once loaded, so the groups open expanded. */}
      {!isLoading && <Table<LevelGroup> rowKey="key" size="small" dataSource={groups} pagination={false}
        expandable={{
          defaultExpandAllRows: true,
          expandedRowRender: (g) => (
            <Table<AssessmentLevelDto> rowKey="id" size="small" pagination={false} scroll={{ x: true }}
              dataSource={[...g.levels].sort((a, b) => b.effectiveDate.localeCompare(a.effectiveDate) || a.lowerValue - b.lowerValue)}
              columns={[
                { title: 'Over', dataIndex: 'lowerValue', align: 'right', render: formatMoney },
                { title: 'Not over', dataIndex: 'upperValue', align: 'right', render: (v: number | null) => (v === null ? '—' : formatMoney(v)) },
                { title: 'Level', dataIndex: 'assessmentPercentage', align: 'right', render: pct },
                { title: 'Ordinance', dataIndex: 'ordinanceNumber' },
                { title: 'Period', render: (_, l) => period(l.effectiveDate, l.endDate) },
                { title: 'Status', dataIndex: 'status', render: statusTag },
                { title: '', render: (_, l) => <ApproveButton status={l.status} pending={approve.isPending} onApprove={() => approve.mutate(l.id, { onError: fail })} /> },
              ]} />
          ),
        }}
        columns={[{ title: 'Classification / actual use / property type', dataIndex: 'label' }, { title: 'Levels', render: (_, g) => g.levels.length }]} />}
      {creating && <CreateLevelModal onClose={() => setCreating(false)} />}
    </Card>
  );
}

// ---------------- Statutory maximum levels (L3-2) ----------------

/**
 * The highest levels the law allows (docs/analysis/assessment-listing-exemptions.md §4.2): loaded from the province's
 * content pack, approved by a second user. A level above the ceiling in force for its keys cannot be created or approved.
 */
function CeilingsTab() {
  const { data = [], isLoading } = useAssessmentLevelCeilings();
  const approve = useApproveAssessmentLevelCeiling();
  const { context, fail } = useToast();
  const [modal, modalContext] = Modal.useModal();
  const [creating, setCreating] = useState(false);
  return (
    <Card title="Statutory maximum assessment levels" extra={<Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>New ceiling</Button>}>
      {context}{modalContext}
      <Typography.Paragraph type="secondary">
        Optional. While a ceiling is in force, an assessment level of its property type (and its classification or actual use, where it names one)
        whose bracket overlaps the ceiling's cannot be above it. The maximums come from the law and are loaded as content; none is built in.
      </Typography.Paragraph>
      <Table<AssessmentLevelCeilingDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
        locale={{ emptyText: 'No ceilings: assessment levels are not checked' }}
        columns={[
          { title: 'Code', dataIndex: 'code' },
          { title: 'Applies to', render: (_, c) => [c.propertyTypeName, c.classificationName ?? 'any classification', c.actualUseName ?? 'any use'].join(' / ') },
          { title: 'Over', dataIndex: 'lowerValue', align: 'right', render: formatMoney },
          { title: 'Not over', dataIndex: 'upperValue', align: 'right', render: (v: number | null) => (v === null ? '—' : formatMoney(v)) },
          { title: 'Maximum', dataIndex: 'maximumPercentage', align: 'right', render: pct },
          { title: 'Legal basis', dataIndex: 'legalBasis' },
          { title: 'Period', render: (_, c) => period(c.effectiveDate, c.endDate) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          {
            title: '', render: (_, c) => <ApproveButton status={c.status} pending={approve.isPending} onApprove={() => approve.mutate(c.id, {
              onError: fail,
              onSuccess: (r) => { if (r.warning) modal.warning({ title: 'Levels above the new ceiling', content: r.warning }); },
            })} />,
          },
        ]} />
      {creating && <CreateCeilingModal onClose={() => setCreating(false)} />}
    </Card>
  );
}

function CreateCeilingModal({ onClose }: { onClose: () => void }) {
  const create = useCreateAssessmentLevelCeiling();
  const [form] = Form.useForm();
  const { data: classifications = [] } = useClassifications();
  const { data: actualUses = [] } = useActualUses();
  const { data: propertyTypes = [] } = usePropertyTypes();
  return (
    <Modal open title="New assessment-level ceiling (Draft)" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} width={720} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" initialValues={{ lowerValue: 0 }} onFinish={(v) => create.mutate({
        code: v.code, description: v.description || null, legalBasis: v.legalBasis, remarks: null, effectiveDate: day(v.effectiveDate)!,
        propertyTypeId: v.propertyTypeId, classificationId: v.classificationId ?? null, actualUseId: v.actualUseId ?? null,
        lowerValue: v.lowerValue, upperValue: v.upperValue ?? null, maximumPercentage: v.maximumPercentage,
      }, { onSuccess: onClose })}>
        <Row gutter={12}>
          <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input maxLength={50} /></Form.Item></Col>
          <Col span={16}><Form.Item name="legalBasis" label="Legal basis" rules={[{ required: true }]}><Input maxLength={500} placeholder="e.g. the section of the law that sets it" /></Form.Item></Col>
          <Col span={8}><Form.Item name="propertyTypeId" label="Property type" rules={[{ required: true }]}><Select options={lookup(propertyTypes)} /></Form.Item></Col>
          <Col span={8}><Form.Item name="classificationId" label="Classification" extra="Blank: any"><Select allowClear showSearch optionFilterProp="label" options={lookup(classifications)} /></Form.Item></Col>
          <Col span={8}><Form.Item name="actualUseId" label="Actual use" extra="Blank: any"><Select allowClear showSearch optionFilterProp="label" options={lookup(actualUses)} /></Form.Item></Col>
          <Col span={8}><Form.Item name="lowerValue" label="Over (market value)" rules={[{ required: true }]}><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="upperValue" label="Not over" extra="Blank: no upper limit"><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="maximumPercentage" label="Maximum level %" rules={[{ required: true }]}><InputNumber min={0.0001} max={100} precision={4} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={16}><Form.Item name="description" label="Description"><Input maxLength={1000} /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  );
}

function CreateLevelModal({ onClose }: { onClose: () => void }) {
  const create = useCreateAssessmentLevel();
  const [form] = Form.useForm();
  const { data: classifications = [] } = useClassifications();
  const { data: actualUses = [] } = useActualUses();
  const { data: propertyTypes = [] } = usePropertyTypes();
  return (
    <Modal open title="New assessment level" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} width={720} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Typography.Paragraph type="secondary">A new level closes the open levels of the same key whose value range it overlaps, from its effective date.</Typography.Paragraph>
      <Form form={form} layout="vertical" initialValues={{ lowerValue: 0 }} onFinish={(v) => create.mutate({
        ordinanceNumber: v.ordinanceNumber, ordinanceDate: day(v.ordinanceDate), classificationId: v.classificationId, actualUseId: v.actualUseId,
        propertyTypeId: v.propertyTypeId, lowerValue: v.lowerValue, upperValue: v.upperValue ?? null, assessmentPercentage: v.assessmentPercentage,
        effectiveDate: day(v.effectiveDate)!,
      }, { onSuccess: onClose })}>
        <Row gutter={12}>
          <Col span={12}><Form.Item name="ordinanceNumber" label="Ordinance No." rules={[{ required: true }]}><Input maxLength={100} /></Form.Item></Col>
          <Col span={12}><Form.Item name="ordinanceDate" label="Ordinance date"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="classificationId" label="Classification" rules={[{ required: true }]}><Select showSearch optionFilterProp="label" options={lookup(classifications)} /></Form.Item></Col>
          <Col span={8}><Form.Item name="actualUseId" label="Actual use" rules={[{ required: true }]}><Select showSearch optionFilterProp="label" options={lookup(actualUses)} /></Form.Item></Col>
          <Col span={8}><Form.Item name="propertyTypeId" label="Property type" rules={[{ required: true }]}><Select options={lookup(propertyTypes)} /></Form.Item></Col>
          <Col span={8}><Form.Item name="lowerValue" label="Over (market value)" rules={[{ required: true }]}><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="upperValue" label="Not over" extra="Blank: no upper limit"><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="assessmentPercentage" label="Assessment level %" rules={[{ required: true }]}><InputNumber min={0} max={100} precision={4} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  );
}
