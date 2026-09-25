import { useState } from 'react';
import { Alert, Button, Descriptions, Drawer, Empty, Space, Spin, Table, Tag, Typography } from 'antd';
import { useAppraisalRecord, useRpuAssessments } from '../../../api/assessments';
import { ApiRequestError } from '../../../lib/apiClient';
import { formatMoney } from '../../../lib/format';
import { PrintFormButton } from '../../../components/PrintFormButton';
import type { AppraisalRecordDto, AssessmentSummaryDto, WorkflowStatus } from '../../../lib/types';

const statusColor: Partial<Record<WorkflowStatus, string>> = {
  PendingReview: 'gold', Approved: 'green', Posted: 'green', Rejected: 'red', Cancelled: 'red', Voided: 'red',
};
const statusTag = (s: WorkflowStatus) => <Tag color={statusColor[s] ?? 'default'}>{s}</Tag>;
const plain = new Intl.NumberFormat('en-PH', { maximumFractionDigits: 6 });
/** "TotalFloorArea" → "Total floor area". */
const humanize = (key: string) => key.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/^./, (c) => c.toUpperCase())
  .split(' ').map((w, i) => (i === 0 ? w : w.toLowerCase())).join(' ');
const dash = (v: string | number | null | undefined) => (v === null || v === undefined || v === '' ? '—' : v);

/** Assessments of one RPU, each with its appraisal record (docs/FORMS-REVISION-PLAN.md A7). */
export function AssessmentsForRpu({ rpuId }: { rpuId: string }) {
  const { data = [], isLoading } = useRpuAssessments(rpuId);
  const [selected, setSelected] = useState<string>();
  return (
    <div style={{ padding: '8px 24px' }}>
      <Typography.Text strong>Assessments</Typography.Text>
      <Table<AssessmentSummaryDto>
        size="small" rowKey="id" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }} style={{ marginTop: 8 }}
        locale={{ emptyText: <Empty description="No assessments yet" image={Empty.PRESENTED_IMAGE_SIMPLE} /> }}
        columns={[
          { title: 'FAAS No.', dataIndex: 'faasNumber', render: (v: string | null) => v ?? '—' },
          { title: 'Year', dataIndex: 'assessmentYear' },
          { title: 'Effective', dataIndex: 'effectiveDate' },
          { title: 'Market value', dataIndex: 'marketValue', align: 'right', render: formatMoney },
          { title: 'Level', dataIndex: 'assessmentPercentage', align: 'right', render: (v: number) => `${plain.format(v)}%` },
          { title: 'Assessed value', dataIndex: 'assessedValue', align: 'right', render: formatMoney },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          {
            title: 'Actions', render: (_, a) => (
              <Space size={4} wrap>
                <Button size="small" onClick={() => setSelected(a.id)}>Appraisal record</Button>
                {/* The FAAS is issued once the assessment is approved; before that it opens as a preview. */}
                <PrintFormButton formCode="FAAS" subjectId={a.id} issuable={a.status === 'Approved' || a.status === 'Posted'} label="Print FAAS" />
              </Space>
            ),
          },
        ]}
      />
      {selected && <AppraisalRecordDrawer assessmentId={selected} onClose={() => setSelected(undefined)} />}
    </div>
  );
}

function AppraisalRecordDrawer({ assessmentId, onClose }: { assessmentId: string; onClose: () => void }) {
  const { data: r, isLoading, error } = useAppraisalRecord(assessmentId);
  return (
    <Drawer open onClose={onClose} size={Math.min(860, window.innerWidth)}
      title={r ? `Appraisal record ${r.faasNumber ?? '(unnumbered)'} — ${r.kind}, ${r.assessment.year}` : 'Appraisal record'}>
      {isLoading && <Spin />}
      {error && <Alert type="error" showIcon title="Could not load" description={error instanceof ApiRequestError ? error.apiError.message : (error as Error).message} />}
      {r && <AppraisalRecordView r={r} />}
    </Drawer>
  );
}

function AppraisalRecordView({ r }: { r: AppraisalRecordDto }) {
  const p = r.property;
  const section = (title: string) => <Typography.Title level={5} style={{ marginTop: 16 }}>{title}</Typography.Title>;
  return (
    <>
      <Descriptions size="small" bordered column={{ xs: 1, md: 2 }}>
        <Descriptions.Item label="Status">{statusTag(r.status)}</Descriptions.Item>
        <Descriptions.Item label="FAAS No.">{dash(r.faasNumber)}</Descriptions.Item>
        <Descriptions.Item label="RPU">{r.rpu.number} ({r.rpu.type})</Descriptions.Item>
        <Descriptions.Item label="Tax Declaration">
          {r.taxDeclaration ? `${r.taxDeclaration.number} (rev. ${r.taxDeclaration.revisionNumber})` : `None in force on ${r.assessment.effectiveDate}`}
        </Descriptions.Item>
        <Descriptions.Item label="PIN">{p.pin}</Descriptions.Item>
        <Descriptions.Item label="Location">{[p.street, p.sitio, p.barangay, p.municipality, p.province].filter(Boolean).join(', ')}</Descriptions.Item>
        <Descriptions.Item label="Lot / Block / Survey">{[p.lotNumber, p.blockNumber, p.surveyNumber].map(dash).join(' / ')}</Descriptions.Item>
        <Descriptions.Item label="Title / Tax map">{[p.titleNumber, p.taxMapNumber].map(dash).join(' / ')}</Descriptions.Item>
      </Descriptions>

      {section(`Declared parties as of ${r.partiesAsOf}`)}
      <Table size="small" rowKey={(x) => `${x.name}-${x.role}`} dataSource={r.parties} pagination={false}
        locale={{ emptyText: `No party recorded as of ${r.partiesAsOf}` }}
        columns={[
          { title: 'Name', dataIndex: 'name' },
          { title: 'Capacity', dataIndex: 'roleLabel' },
          { title: 'Share', render: (_, x) => (x.role === 'Owner' ? `${plain.format(x.sharePercent)}%` : '—') },
          { title: 'Address', dataIndex: 'address', render: dash },
        ]} />

      {r.land && (
        <>
          {section('Land')}
          <Descriptions size="small" bordered column={{ xs: 1, md: 2 }}>
            <Descriptions.Item label="Area">{plain.format(r.land.area)} {r.land.areaUnit}</Descriptions.Item>
            <Descriptions.Item label="Classification">{r.land.classification}</Descriptions.Item>
            <Descriptions.Item label="Actual use">{r.land.actualUse}</Descriptions.Item>
            <Descriptions.Item label="Sub-classification">{dash(r.land.subClassification)}</Descriptions.Item>
            <Descriptions.Item label="Zone">{dash(r.land.zone)}</Descriptions.Item>
            <Descriptions.Item label="Location factor">{dash(r.land.locationFactor)}</Descriptions.Item>
            <Descriptions.Item label="Road">{[r.land.roadType, r.land.roadFrontage !== null ? `${r.land.roadFrontage} m frontage` : null].filter(Boolean).join(', ') || '—'}</Descriptions.Item>
            <Descriptions.Item label="Corner lot">{r.land.isCornerLot ? 'Yes' : 'No'}</Descriptions.Item>
          </Descriptions>
        </>
      )}
      {r.building && (
        <>
          {section('Building')}
          <Descriptions size="small" bordered column={{ xs: 1, md: 2 }}>
            <Descriptions.Item label="Type">{r.building.buildingType}</Descriptions.Item>
            <Descriptions.Item label="Structural type">{r.building.structuralType}</Descriptions.Item>
            <Descriptions.Item label="Actual use">{r.building.actualUse}</Descriptions.Item>
            <Descriptions.Item label="Storeys">{r.building.numberOfStoreys}</Descriptions.Item>
            <Descriptions.Item label="Floor area / total">{plain.format(r.building.floorArea)} / {plain.format(r.building.totalFloorArea)} sqm</Descriptions.Item>
            <Descriptions.Item label="Constructed / completed">{dash(r.building.yearConstructed)} / {dash(r.building.yearCompleted)}</Descriptions.Item>
            <Descriptions.Item label="Condition">{r.building.condition}</Descriptions.Item>
            <Descriptions.Item label="Completion">{plain.format(r.building.completionPercentage)}%</Descriptions.Item>
          </Descriptions>
          {r.building.components.length > 0 && (
            <Table size="small" rowKey={(_, i) => String(i)} dataSource={r.building.components} pagination={false} style={{ marginTop: 8 }}
              columns={[
                { title: 'Component', dataIndex: 'componentType' },
                { title: 'Description', dataIndex: 'description', render: dash },
                { title: 'Qty', dataIndex: 'quantity', render: dash },
                { title: 'Cost', dataIndex: 'cost', align: 'right', render: (v: number | null) => (v === null ? '—' : formatMoney(v)) },
              ]} />
          )}
        </>
      )}
      {r.machinery && (
        <>
          {section('Machinery')}
          <Descriptions size="small" bordered column={{ xs: 1, md: 2 }}>
            <Descriptions.Item label="Type">{r.machinery.machineryType}</Descriptions.Item>
            <Descriptions.Item label="Description">{dash(r.machinery.description)}</Descriptions.Item>
            <Descriptions.Item label="Brand / model / serial">{[r.machinery.brand, r.machinery.model, r.machinery.serialNumber].map(dash).join(' / ')}</Descriptions.Item>
            <Descriptions.Item label="Capacity">{r.machinery.capacity !== null ? `${plain.format(r.machinery.capacity)} ${r.machinery.capacityUnit ?? ''}` : '—'}</Descriptions.Item>
            <Descriptions.Item label="Acquired">{dash(r.machinery.dateAcquired)}</Descriptions.Item>
            <Descriptions.Item label="Condition when acquired">{r.machinery.isBrandNew ? 'Brand new' : 'Not brand new'}</Descriptions.Item>
            <Descriptions.Item label="Economic / remaining life">{dash(r.machinery.economicLifeYears)} / {dash(r.machinery.remainingLifeYears)} years</Descriptions.Item>
          </Descriptions>
        </>
      )}

      {section('Valuation')}
      <Descriptions size="small" bordered column={{ xs: 1, md: 2 }}>
        <Descriptions.Item label="Method">{r.valuation.method}</Descriptions.Item>
        <Descriptions.Item label="Computed">{new Date(r.valuation.computedAt).toLocaleString()}</Descriptions.Item>
        <Descriptions.Item label="SMV" span="filled">
          {r.valuation.smv
            ? `Ordinance ${r.valuation.smv.ordinanceNumber} (${r.valuation.smv.ordinanceDate}), effective ${r.valuation.smv.effectivityDate}, revision ${r.valuation.smv.revisionYear}`
            : 'Not SMV-based'}
        </Descriptions.Item>
        {r.valuation.scheduleRate !== null && (
          <Descriptions.Item label="Schedule rate" span="filled">{formatMoney(r.valuation.scheduleRate)} {r.valuation.scheduleUnit}</Descriptions.Item>
        )}
      </Descriptions>
      <Table size="small" rowKey="key" dataSource={r.valuation.breakdown} pagination={false} style={{ marginTop: 8 }}
        columns={[
          { title: 'Calculation', dataIndex: 'key', render: humanize },
          { title: 'Value', dataIndex: 'value', align: 'right', render: (v: number, line) => (line.key === 'MarketValue' ? <strong>{formatMoney(v)}</strong> : plain.format(v)) },
        ]} />

      {section('Assessment')}
      <Descriptions size="small" bordered column={{ xs: 1, md: 2 }}>
        <Descriptions.Item label="Year / effective">{r.assessment.year} / {r.assessment.effectiveDate}</Descriptions.Item>
        <Descriptions.Item label="Property type">{r.assessment.propertyType}</Descriptions.Item>
        <Descriptions.Item label="Classification">{r.assessment.classification}</Descriptions.Item>
        <Descriptions.Item label="Actual use">{r.assessment.actualUse}</Descriptions.Item>
        <Descriptions.Item label="Market value">{formatMoney(r.assessment.marketValue)}</Descriptions.Item>
        <Descriptions.Item label="Assessment level">
          {plain.format(r.assessment.assessmentLevelPercent)}% (bracket {formatMoney(r.assessment.levelLowerValue)} – {r.assessment.levelUpperValue !== null ? formatMoney(r.assessment.levelUpperValue) : 'and above'})
        </Descriptions.Item>
        <Descriptions.Item label="Level ordinance">{r.assessment.levelOrdinanceNumber}{r.assessment.levelOrdinanceDate ? ` (${r.assessment.levelOrdinanceDate})` : ''}</Descriptions.Item>
        <Descriptions.Item label="Assessed value"><strong>{formatMoney(r.assessment.assessedValue)}</strong></Descriptions.Item>
        <Descriptions.Item label="Previous assessment" span="filled">
          {r.previous
            ? `${r.previous.year} (effective ${r.previous.effectiveDate}): AV ${formatMoney(r.previous.assessedValue)}; change ${r.previous.assessedValueChange >= 0 ? '+' : ''}${formatMoney(r.previous.assessedValueChange)}`
            : 'None — first assessment'}
        </Descriptions.Item>
        {r.assessment.remarks && <Descriptions.Item label="Remarks" span="filled">{r.assessment.remarks}</Descriptions.Item>}
      </Descriptions>

      {section('Signatures')}
      <Typography.Paragraph type="secondary">
        Recorded by {r.recordedBy ?? '(system)'} on {new Date(r.recordedAt).toLocaleString()}.
      </Typography.Paragraph>
      <Table size="small" rowKey={(x) => `${x.label}-${x.signedAt}`} dataSource={r.signatures} pagination={false} locale={{ emptyText: 'Not yet signed' }}
        columns={[
          { title: 'Step', dataIndex: 'label' },
          { title: 'Name', dataIndex: 'name' },
          { title: 'Position', dataIndex: 'position', render: dash },
          { title: 'Signed', dataIndex: 'signedAt', render: (v: string) => new Date(v).toLocaleString() },
        ]} />

      {r.notices.length > 0 && (
        <>
          {section('Notices of Assessment')}
          <Table size="small" rowKey="id" dataSource={r.notices} pagination={false}
            columns={[
              { title: 'No.', dataIndex: 'number', render: dash },
              { title: 'Status', dataIndex: 'status' },
              { title: 'Received', dataIndex: 'receivedDate', render: dash },
              { title: 'Appeal until', dataIndex: 'appealDeadline', render: dash },
            ]} />
        </>
      )}
    </>
  );
}
