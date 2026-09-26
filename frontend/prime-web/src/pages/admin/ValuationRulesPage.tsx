import { useState } from 'react';
import { Alert, Button, Card, Col, DatePicker, Form, Input, InputNumber, Modal, Row, Select, Space, Table, Tabs, Tag, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import {
  useAllAdjustmentFactors, useApproveAdjustmentFactor, useApproveAssessmentLevel, useApproveSmv, useApproveSmvSchedule, useAssessmentLevels,
  useCreateAdjustmentFactor, useCreateAssessmentLevel, useCreateSmv, useCreateSmvSchedule, useSmvSchedules, useSmvs,
} from '../../api/valuation';
import { useActualUses, useClassifications, useImprovementKinds, usePropertyTypes, useZones } from '../../api/referenceData';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import type { AdjustmentFactorDto, AssessmentLevelDto, LookupDto, SmvDto, SmvScheduleDto, WorkflowStatus } from '../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const day = (d: Dayjs | null | undefined) => (d ? d.format('YYYY-MM-DD') : null);
const lookup = (rows: LookupDto[]) => rows.map((r) => ({ value: r.id, label: `${r.code} — ${r.name}` }));
const statusTag = (s: WorkflowStatus) => <Tag color={s === 'Approved' ? 'green' : s === 'Draft' ? 'default' : 'orange'}>{s}</Tag>;
const period = (from: string, to: string | null) => `${from} → ${to ?? 'open'}`;
const pct = (v: number) => `${new Intl.NumberFormat('en-PH', { maximumFractionDigits: 4 }).format(v)}%`;

/** The approve button of a rule; the creator cannot approve (maker-checker, CLAUDE.md §46). */
function ApproveButton({ status, onApprove, pending }: { status: WorkflowStatus; onApprove: () => void; pending: boolean }) {
  return status === 'Draft' ? <Button size="small" loading={pending} onClick={onApprove}>Approve</Button> : null;
}

function useToast() {
  const [toast, context] = message.useMessage();
  return { context, fail: (e: unknown) => toast.error(errorText(e)) };
}

/**
 * The rules valuation and assessment use (docs/analysis/value-and-assess.md §3):
 * SMVs with their schedules, adjustment factors and assessment levels. Rules
 * are created and approved, never edited; a change is a new version from its
 * effective date. Every value comes from the LGU's ordinance.
 */
export function ValuationRulesPage() {
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Title level={3} style={{ margin: 0 }}>Valuation Rules</Typography.Title>
      <Alert type="warning" showIcon title="Enter only values from the LGU's approved ordinances"
        description="PRIME invents no market values or assessment levels. Rules are never edited: a change is a new version that takes over from its effective date. Another user approves what you create." />
      <Tabs items={[
        { key: 'smv', label: 'Schedules of Market Values', children: <SmvTab /> },
        { key: 'factors', label: 'Adjustment factors', children: <FactorsTab /> },
        { key: 'levels', label: 'Assessment levels', children: <LevelsTab /> },
      ]} />
    </Space>
  );
}

// ---------------- SMVs and schedules ----------------

function SmvTab() {
  const { data, isLoading } = useSmvs();
  const approve = useApproveSmv();
  const { context, fail } = useToast();
  const [creating, setCreating] = useState(false);
  const [open, setOpen] = useState<SmvDto | null>(null);
  return (
    <Card title="SMVs" extra={<Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>New SMV</Button>}>
      {context}
      <Table<SmvDto> rowKey="id" size="small" loading={isLoading} dataSource={data?.items ?? []} pagination={false} scroll={{ x: true }}
        columns={[
          { title: 'Ordinance', dataIndex: 'ordinanceNumber' },
          { title: 'Ordinance date', dataIndex: 'ordinanceDate' },
          { title: 'Effective', dataIndex: 'effectivityDate' },
          { title: 'Revision year', dataIndex: 'revisionYear' },
          { title: 'Description', dataIndex: 'description', render: (v: string | null) => v ?? '—' },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          {
            title: '', render: (_, s) => (
              <Space>
                <Button size="small" onClick={() => setOpen(s)}>Schedules</Button>
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

function CreateSmvModal({ onClose }: { onClose: () => void }) {
  const create = useCreateSmv();
  const [form] = Form.useForm();
  return (
    <Modal open title="New SMV" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" onFinish={(v) => create.mutate({
        ordinanceNumber: v.ordinanceNumber, ordinanceDate: day(v.ordinanceDate)!, approvalDate: day(v.approvalDate),
        effectivityDate: day(v.effectivityDate)!, revisionYear: v.revisionYear, description: v.description || null,
      }, { onSuccess: onClose })}>
        <Form.Item name="ordinanceNumber" label="Ordinance No." rules={[{ required: true }]}><Input maxLength={100} /></Form.Item>
        <Row gutter={12}>
          <Col span={8}><Form.Item name="ordinanceDate" label="Ordinance date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="approvalDate" label="Approval date"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="effectivityDate" label="Effective" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
        </Row>
        <Form.Item name="revisionYear" label="Revision year" rules={[{ required: true }]}><InputNumber min={1900} max={2200} precision={0} /></Form.Item>
        <Form.Item name="description" label="Description"><Input maxLength={500} /></Form.Item>
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
    <Modal open title={`Schedules of SMV ${smv.ordinanceNumber}`} onCancel={onClose} footer={null} width={1100} destroyOnHidden>
      {context}
      <Button icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => setCreating(true)}>New schedule</Button>
      <Table<SmvScheduleDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
        columns={[
          { title: 'Classification', dataIndex: 'classificationName' },
          { title: 'Actual use', dataIndex: 'actualUseName' },
          { title: 'Type', dataIndex: 'propertyTypeName' },
          { title: 'Zone', dataIndex: 'zoneName', render: (v: string | null) => v ?? 'any' },
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
  const { data: actualUses = [] } = useActualUses();
  const { data: propertyTypes = [] } = usePropertyTypes();
  const { data: zones = [] } = useZones();
  const { data: kinds = [] } = useImprovementKinds();
  return (
    <Modal open title="New schedule" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} width={720} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" onFinish={(v) => create.mutate({
        classificationId: v.classificationId, actualUseId: v.actualUseId, propertyTypeId: v.propertyTypeId, zoneId: v.zoneId ?? null,
        unit: v.unit, marketValue: v.marketValue, minimumValue: v.minimumValue ?? null, maximumValue: v.maximumValue ?? null,
        effectiveDate: day(v.effectiveDate)!, improvementKindId: v.improvementKindId ?? null,
      }, { onSuccess: onClose })}>
        <Row gutter={12}>
          <Col span={8}><Form.Item name="classificationId" label="Classification" rules={[{ required: true }]}><Select showSearch optionFilterProp="label" options={lookup(classifications)} /></Form.Item></Col>
          <Col span={8}><Form.Item name="actualUseId" label="Actual use" rules={[{ required: true }]}><Select showSearch optionFilterProp="label" options={lookup(actualUses)} /></Form.Item></Col>
          <Col span={8}><Form.Item name="propertyTypeId" label="Property type" rules={[{ required: true }]}><Select options={lookup(propertyTypes)} /></Form.Item></Col>
          <Col span={12}><Form.Item name="zoneId" label="Zone" extra="Blank: any zone"><Select allowClear showSearch optionFilterProp="label" options={lookup(zones)} /></Form.Item></Col>
          <Col span={12}><Form.Item name="improvementKindId" label="Improvement kind" extra="For trees and plants"><Select allowClear showSearch optionFilterProp="label" options={lookup(kinds)} /></Form.Item></Col>
          <Col span={6}><Form.Item name="unit" label="Unit" rules={[{ required: true }]}><Input maxLength={30} placeholder="per sqm" /></Form.Item></Col>
          <Col span={6}><Form.Item name="marketValue" label="Market value" rules={[{ required: true }]}><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={6}><Form.Item name="minimumValue" label="Minimum"><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={6}><Form.Item name="maximumValue" label="Maximum"><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  );
}

// ---------------- Adjustment factors ----------------

function FactorsTab() {
  const { data = [], isLoading } = useAllAdjustmentFactors();
  const approve = useApproveAdjustmentFactor();
  const { context, fail } = useToast();
  const [creating, setCreating] = useState(false);
  return (
    <Card title="Adjustment factors" extra={<Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>New factor</Button>}>
      {context}
      <Table<AdjustmentFactorDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
        columns={[
          { title: 'SMV', dataIndex: 'smvOrdinanceNumber' },
          { title: 'Code', dataIndex: 'code' },
          { title: 'Name', dataIndex: 'name' },
          { title: 'Adjustment', dataIndex: 'percent', align: 'right', render: pct },
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

function CreateFactorModal({ onClose }: { onClose: () => void }) {
  const create = useCreateAdjustmentFactor();
  const [form] = Form.useForm();
  const { data: smvs } = useSmvs();
  const { data: classifications = [] } = useClassifications();
  return (
    <Modal open title="New adjustment factor" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} width={680} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" onFinish={(v) => create.mutate({
        smvId: v.smvId, code: v.code, name: v.name, percent: v.percent, classificationId: v.classificationId ?? null,
        description: v.description || null, legalBasis: v.legalBasis, effectiveDate: day(v.effectiveDate)!, remarks: null,
      }, { onSuccess: onClose })}>
        <Row gutter={12}>
          <Col span={12}><Form.Item name="smvId" label="SMV" rules={[{ required: true }]}>
            <Select options={(smvs?.items ?? []).map((s) => ({ value: s.id, label: `${s.ordinanceNumber} (${s.revisionYear})` }))} />
          </Form.Item></Col>
          <Col span={12}><Form.Item name="classificationId" label="Classification" extra="Blank: all"><Select allowClear showSearch optionFilterProp="label" options={lookup(classifications)} /></Form.Item></Col>
          <Col span={6}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input maxLength={30} /></Form.Item></Col>
          <Col span={12}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input maxLength={200} /></Form.Item></Col>
          <Col span={6}><Form.Item name="percent" label="Adjustment %" rules={[{ required: true }]}><InputNumber precision={4} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={16}><Form.Item name="legalBasis" label="Legal basis" rules={[{ required: true }]}><Input maxLength={500} /></Form.Item></Col>
          <Col span={8}><Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
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
