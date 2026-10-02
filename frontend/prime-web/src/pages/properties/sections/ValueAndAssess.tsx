import { useState } from 'react';
import { Alert, Button, Col, DatePicker, Descriptions, Drawer, Empty, Form, Input, InputNumber, Modal, Row, Select, Space, Spin, Table, Tag, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useCreateAssessment, useEffectivity, usePreviewAssessment, useRpuValuations, useValueRpu } from '../../../api/valuation';
import { useRpuAssessments } from '../../../api/assessments';
import { useTransactionTypes } from '../../../api/transactions';
import { ApiRequestError } from '../../../lib/apiClient';
import { formatMoney } from '../../../lib/format';
import {
  effectivityRules, type AssessmentLineDto, type AssessmentPreviewDto, type CreateAssessmentRequest, type EffectivityDto, type ValuationDto,
  type ValuationLineDto,
} from '../../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const plain = new Intl.NumberFormat('en-PH', { maximumFractionDigits: 6 });
/** "TotalFloorArea" → "Total floor area". */
const humanize = (key: string) => key.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/^./, (c) => c.toUpperCase())
  .split(' ').map((w, i) => (i === 0 ? w : w.toLowerCase())).join(' ');

/**
 * Values the unit and shows the calculation (docs/analysis/value-and-assess.md §3):
 * each appraisal row, the SMV used and the total; the unit's earlier valuations;
 * and "Assess" from a valuation. Every Value run is saved as a valuation.
 */
export function ValuationDrawer({ rpuId, onClose }: { rpuId: string; onClose: () => void }) {
  const { data: valuations = [], isLoading } = useRpuValuations(rpuId);
  const value = useValueRpu(rpuId);
  const [selectedId, setSelectedId] = useState<string>();
  const [assessing, setAssessing] = useState<ValuationDto | null>(null);
  const [chosenDate, setChosenDate] = useState<Dayjs>(dayjs());
  // The transaction the assessment is for: its rule gives the effectivity, and the unit is valued as of that date (L1-2).
  const { data: types = [] } = useTransactionTypes(true);
  const [typeId, setTypeId] = useState<string>();
  const [causeDate, setCauseDate] = useState<Dayjs | null>(null);
  const type = types.find((t) => t.id === typeId);
  const needsCause = type?.effectivityRule === 'NextQuarter';
  // A reassessment's effectivity is asked for once the date of its cause is known.
  const ready = !needsCause || !!causeDate;
  const effectivity = useEffectivity(typeId, causeDate?.format('YYYY-MM-DD'), ready);
  const derived = effectivity.data?.derived ? effectivity.data : undefined;
  const asOf = derived ? dayjs(derived.effectiveDate) : chosenDate;
  const selected = valuations.find((v) => v.id === selectedId) ?? valuations[0];

  return (
    <Drawer open onClose={onClose} size={Math.min(900, window.innerWidth)} title="Valuation">
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginBottom: 12 }}>
        <Select aria-label="Transaction" allowClear placeholder="Transaction" style={{ flex: '1 1 220px', minWidth: 0, maxWidth: 360 }} value={typeId}
          onChange={(v) => { setTypeId(v); setCauseDate(null); }}
          options={types.map((t) => ({ value: t.id, label: `${t.code} — ${t.name}` }))} />
        {needsCause && <DatePicker aria-label="Date of the cause" placeholder="Date of the cause" value={causeDate} onChange={setCauseDate} />}
        <DatePicker aria-label="Value as of" value={asOf} allowClear={false} disabled={!!derived} onChange={(d) => d && setChosenDate(d)} />
        <Button type="primary" loading={value.isPending}
          onClick={() => value.mutate({ asOf: asOf.format('YYYY-MM-DD'), transactionTypeId: typeId }, { onSuccess: (v) => setSelectedId(v.id) })}>Value</Button>
      </div>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        The unit is valued under the SMV and rules in force on the chosen date. An assessment takes effect on its valuation&apos;s date.
        {type && !type.effectivityRule && ' This transaction type has no effectivity rule: choose the date.'}
        {!ready && ' Enter the date of the cause: the reassessment takes effect from the quarter after it is made.'}
      </Typography.Paragraph>
      {derived && <EffectivityNote effectivity={derived} />}
      {effectivity.isError && <Alert type="warning" showIcon style={{ marginBottom: 12 }} title={errorText(effectivity.error)} />}
      {value.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not valued" description={errorText(value.error)} />}
      {isLoading && <Spin />}
      {!isLoading && !selected && <Empty description="This unit has not been valued yet. Value it to see the calculation." />}
      {selected && (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, width: '100%' }}>
            <Select aria-label="Valuation" style={{ flex: '1 1 260px', minWidth: 0, maxWidth: 520 }} value={selected.id} onChange={setSelectedId}
              options={valuations.map((v, i) => ({
                value: v.id,
                label: `As of ${v.effectiveDate} — ${formatMoney(v.computedMarketValue)} (run ${dayjs(v.computedAt).format('YYYY-MM-DD HH:mm')})${i === 0 ? ', latest' : ''}`,
              }))} />
            <Button type="primary" onClick={() => setAssessing(selected)}>Assess this valuation</Button>
          </div>
          <Descriptions size="small" bordered column={{ xs: 1, md: 2 }}>
            <Descriptions.Item label="Market value"><b>{formatMoney(selected.computedMarketValue)}</b></Descriptions.Item>
            <Descriptions.Item label="Method">{humanize(selected.valuationMethod)}</Descriptions.Item>
            <Descriptions.Item label="SMV">
              {selected.smvOrdinanceNumber ? `${selected.smvOrdinanceNumber} (revision ${selected.smvRevisionYear})` : '—'}
            </Descriptions.Item>
            <Descriptions.Item label="Valued as of">{selected.effectiveDate}</Descriptions.Item>
          </Descriptions>
          <Typography.Title level={5} style={{ margin: 0 }}>Appraisal rows</Typography.Title>
          <Table<ValuationLineDto> size="small" rowKey="sequence" dataSource={selected.lines ?? []} pagination={false} scroll={{ x: true }}
            expandable={{ expandedRowRender: (l) => <Breakdown line={l} />, defaultExpandAllRows: (selected.lines?.length ?? 0) <= 3 }}
            columns={[
              { title: '#', dataIndex: 'sequence', width: 40 },
              { title: 'Row', render: (_, l) => l.description ?? humanize(l.source) },
              {
                title: 'Classification / use', render: (_, l) => (
                  <>
                    {[l.classificationName, l.subClassificationName, l.actualUseName].filter(Boolean).join(' / ') || '—'}
                    {(l.pricedClassificationName || l.pricedSubClassificationName) && (
                      <div><Tag color="purple">priced as {[l.pricedClassificationName ?? l.classificationName, l.pricedSubClassificationName].filter(Boolean).join(' / ')}</Tag></div>
                    )}
                  </>
                ),
              },
              { title: 'Quantity', align: 'right', render: (_, l) => (l.quantity !== null ? `${plain.format(l.quantity)} ${l.unit ?? ''}` : '—') },
              { title: 'Unit value', align: 'right', render: (_, l) => (l.unitValue !== null ? formatMoney(l.unitValue) : '—') },
              { title: 'Market value', dataIndex: 'marketValue', align: 'right', render: formatMoney },
            ]} />
        </Space>
      )}
      {assessing && <AssessModal rpuId={rpuId} valuation={assessing} transactionTypeId={typeId} causeDate={causeDate?.format('YYYY-MM-DD')} ready={ready}
        onClose={() => setAssessing(null)} />}
    </Drawer>
  );
}

/** "Made today, it takes effect on …" with the rule, its basis and any late-reassessment flag. */
function EffectivityNote({ effectivity: e }: { effectivity: EffectivityDto }) {
  return (
    <Alert type={e.causeWindowExceeded ? 'warning' : 'info'} showIcon style={{ marginBottom: 12 }}
      title={`If approved today (${e.madeOn}), a ${e.transactionCode} assessment takes effect on ${e.effectiveDate} (Q${e.quarter} ${e.year}).`}
      description={<>
        {effectivityRules.find((r) => r.value === e.rule)?.label}{e.legalBasis ? ` — ${e.legalBasis}` : ''}.
        {e.causeWindowExceeded && ` Made more than ${e.causeWindowDays} days after the cause: the assessment will be flagged.`}
        {' '}Approval on a later date that changes this asks for a new valuation.
      </>} />
  );
}

/** Breakdown entries that are yes/no flags; and the building figures' FAAS names (valuation-foundation.md §4.5). */
const breakdownFlags = new Set(['DepreciationCarriedOver', 'DepreciationCapped', 'MinimumApplied', 'IsBrandNew', 'InOperation']);
const breakdownLabels: Record<string, string> = {
  DepreciationCarriedOver: 'Depreciation carried over',
  DepreciationCapped: 'Depreciation at its limit',
  BaseValue: 'Base value (core)',
  Share: 'Share of the appraised value',
};

/** One row's calculation, inputs first and the market value last (the order the valuation stored). */
function Breakdown({ line }: { line: ValuationLineDto }) {
  const label = (key: string) => breakdownLabels[key]
    ?? (key.startsWith('ExtraItem:') ? `Extra item ${key.slice(10)}` : key.startsWith('Input:') ? `Input: ${key.slice(6)}` : humanize(key));
  const value = (key: string, v: number) =>
    key === 'MarketValue' ? <b>{formatMoney(v)}</b>
      : breakdownFlags.has(key) ? (v ? 'Yes' : 'No')
        : key === 'Share' ? `${plain.format(v * 100)}%`
        : key.endsWith('Percent') || key === 'CompletionPercentage' ? `${plain.format(v)}%` : plain.format(v);
  return (
    <Descriptions size="small" column={{ xs: 1, md: 3 }}>
      {line.breakdown.map((b) => (
        <Descriptions.Item key={b.key} label={label(b.key)}>{value(b.key, b.value)}</Descriptions.Item>
      ))}
    </Descriptions>
  );
}

interface AssessForm { assessmentYear: number; effectiveDate: Dayjs; previousAssessmentId?: string; remarks?: string; overrideReason?: string }

/**
 * Creates a draft assessment of a valuation after a preview of its rows and
 * levels. The assessment takes effect on the valuation's date
 * (docs/analysis/valuation-foundation.md §4.1, Q1), which must be the date the
 * transaction type's rule gives, unless overridden with a reason (§4.2, L1-2).
 */
function AssessModal({ rpuId, valuation, transactionTypeId, causeDate, ready, onClose }: {
  rpuId: string; valuation: ValuationDto; transactionTypeId?: string; causeDate?: string; ready: boolean; onClose: () => void;
}) {
  const [form] = Form.useForm<AssessForm>();
  const { data: assessments = [] } = useRpuAssessments(rpuId);
  const posted = assessments.filter((a) => a.status === 'Posted').sort((a, b) => b.effectiveDate.localeCompare(a.effectiveDate));
  const preview = usePreviewAssessment();
  const create = useCreateAssessment(rpuId);
  const [shown, setShown] = useState<AssessmentPreviewDto | null>(null);
  const previousId = Form.useWatch('previousAssessmentId', form);
  const previous = posted.find((a) => a.id === previousId);
  const { data: effectivity } = useEffectivity(transactionTypeId, causeDate, ready);
  const ruleDate = effectivity?.derived ? effectivity.effectiveDate : undefined;
  const overriding = !!ruleDate && ruleDate !== valuation.effectiveDate;

  const request = (v: AssessForm): CreateAssessmentRequest => ({
    valuationId: valuation.id, assessmentYear: v.assessmentYear, effectiveDate: v.effectiveDate.format('YYYY-MM-DD'),
    previousAssessmentId: v.previousAssessmentId ?? null, revisionReference: null, remarks: v.remarks?.trim() || null,
    transactionTypeId: transactionTypeId ?? null, causeDate: causeDate ?? null,
    effectivityOverrideReason: overriding ? v.overrideReason?.trim() || null : null,
  });

  return (
    <Modal open title="Assess this valuation" onCancel={onClose} width={820} destroyOnHidden footer={[
      <Button key="cancel" onClick={onClose}>Close</Button>,
      <Button key="preview" loading={preview.isPending}
        onClick={() => form.validateFields().then((v) => preview.mutate(request(v), { onSuccess: setShown, onError: () => setShown(null) }))}>Preview</Button>,
      <Button key="create" type="primary" disabled={!shown} loading={create.isPending}
        onClick={() => form.validateFields().then((v) => create.mutate(request(v), { onSuccess: onClose }))}>Create draft assessment</Button>,
    ]}>
      <Typography.Paragraph type="secondary">
        Market value {formatMoney(valuation.computedMarketValue)}, valued as of {valuation.effectiveDate}.
        {effectivity ? ` Transaction ${effectivity.transactionCode}.` : ' No transaction type chosen: the date is entered.'}
      </Typography.Paragraph>
      {overriding && (
        <Alert type="warning" showIcon style={{ marginBottom: 12 }} title={`The transaction's rule gives ${ruleDate}, not ${valuation.effectiveDate}.`}
          description="Close this, value the unit as of the rule's date and assess that valuation — or give the reason for overriding the rule (it is kept with the assessment)." />
      )}
      {effectivity?.causeWindowExceeded && (
        <Alert type="warning" showIcon style={{ marginBottom: 12 }}
          title={`Made more than ${effectivity.causeWindowDays} days after the cause (${effectivity.causeDate}); the assessment will be flagged.`} />
      )}
      {(preview.isError || create.isError) && (
        <Alert type="error" showIcon style={{ marginBottom: 12 }} title={create.isError ? 'Not created' : 'No preview'} description={errorText(create.error ?? preview.error)} />
      )}
      <Form form={form} layout="vertical" onValuesChange={() => setShown(null)}
        initialValues={{ previousAssessmentId: posted[0]?.id, effectiveDate: dayjs(valuation.effectiveDate), assessmentYear: dayjs(valuation.effectiveDate).year() }}>
        <Row gutter={12}>
          <Col xs={12} md={6}>
            <Form.Item name="assessmentYear" label="Assessment year" rules={[{ required: true, message: 'Enter the year' }]}>
              <InputNumber min={1900} max={2200} precision={0} style={{ width: '100%' }} />
            </Form.Item>
          </Col>
          <Col xs={12} md={6}>
            <Form.Item name="effectiveDate" label="Effective date" rules={[{ required: true, message: 'Enter the effective date' }]}
              extra="The valuation's date. To assess another date, value the unit as of that date.">
              <DatePicker style={{ width: '100%' }} disabled />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="previousAssessmentId" label="Previous assessment" extra="The unit's latest posted assessment is proposed.">
              <Select allowClear placeholder="None — first assessment of this unit"
                options={posted.map((a) => ({ value: a.id, label: `${a.assessmentYear}, effective ${a.effectiveDate} — AV ${formatMoney(a.assessedValue)}` }))} />
            </Form.Item>
          </Col>
          {overriding && (
            <Col span={24}>
              <Form.Item name="overrideReason" label="Reason for overriding the rule" rules={[{ required: true, whitespace: true, message: 'Give the reason' }]}>
                <Input maxLength={1000} />
              </Form.Item>
            </Col>
          )}
          <Col span={24}><Form.Item name="remarks" label="Remarks"><Input maxLength={1000} /></Form.Item></Col>
        </Row>
      </Form>
      {shown && (
        <>
          <Table<AssessmentLineDto> size="small" rowKey="sequence" dataSource={shown.lines} pagination={false} scroll={{ x: true }}
            columns={[
              { title: 'Classification', dataIndex: 'classificationName' },
              { title: 'Actual use', dataIndex: 'actualUseName' },
              { title: 'Market value', dataIndex: 'marketValue', align: 'right', render: formatMoney },
              { title: 'Level', dataIndex: 'assessmentPercentage', align: 'right', render: (v: number) => `${plain.format(v)}%` },
              { title: 'Assessed value', dataIndex: 'assessedValue', align: 'right', render: formatMoney },
            ]}
            summary={() => (
              <Table.Summary.Row>
                <Table.Summary.Cell index={0} colSpan={2} align="right"><b>Total</b></Table.Summary.Cell>
                <Table.Summary.Cell index={1} align="right"><b>{formatMoney(shown.marketValue)}</b></Table.Summary.Cell>
                <Table.Summary.Cell index={2} />
                <Table.Summary.Cell index={3} align="right"><b>{formatMoney(shown.assessedValue)}</b></Table.Summary.Cell>
              </Table.Summary.Row>
            )} />
          {previous && <BeforeAfter previous={{ marketValue: previous.marketValue, assessedValue: previous.assessedValue }} next={shown} />}
        </>
      )}
    </Modal>
  );
}

/** Previous and new values and the difference (CLAUDE.md §51). */
export function BeforeAfter({ previous, next }: { previous: { marketValue: number; assessedValue: number }; next: { marketValue: number; assessedValue: number } }) {
  const diff = (a: number, b: number) => {
    const d = b - a;
    return <Tag color={d > 0 ? 'orange' : d < 0 ? 'blue' : 'default'}>{d > 0 ? '+' : ''}{formatMoney(d)}</Tag>;
  };
  return (
    <Descriptions size="small" bordered column={3} style={{ marginTop: 12 }} title="Before and after">
      <Descriptions.Item label="Previous MV">{formatMoney(previous.marketValue)}</Descriptions.Item>
      <Descriptions.Item label="New MV">{formatMoney(next.marketValue)}</Descriptions.Item>
      <Descriptions.Item label="Difference">{diff(previous.marketValue, next.marketValue)}</Descriptions.Item>
      <Descriptions.Item label="Previous AV">{formatMoney(previous.assessedValue)}</Descriptions.Item>
      <Descriptions.Item label="New AV">{formatMoney(next.assessedValue)}</Descriptions.Item>
      <Descriptions.Item label="Difference">{diff(previous.assessedValue, next.assessedValue)}</Descriptions.Item>
    </Descriptions>
  );
}
