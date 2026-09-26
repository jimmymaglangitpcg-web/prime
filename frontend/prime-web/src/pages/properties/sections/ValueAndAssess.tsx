import { useState } from 'react';
import { Alert, Button, Col, DatePicker, Descriptions, Drawer, Empty, Form, Input, InputNumber, Modal, Row, Select, Space, Spin, Table, Tag, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useCreateAssessment, usePreviewAssessment, useRpuValuations, useValueRpu } from '../../../api/valuation';
import { useRpuAssessments } from '../../../api/assessments';
import { ApiRequestError } from '../../../lib/apiClient';
import { formatMoney } from '../../../lib/format';
import type { AssessmentLineDto, AssessmentPreviewDto, CreateAssessmentRequest, ValuationDto, ValuationLineDto } from '../../../lib/types';

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
  const selected = valuations.find((v) => v.id === selectedId) ?? valuations[0];

  return (
    <Drawer open onClose={onClose} size={Math.min(900, window.innerWidth)} title="Valuation"
      extra={<Button type="primary" loading={value.isPending} onClick={() => value.mutate(undefined, { onSuccess: (v) => setSelectedId(v.id) })}>Value now</Button>}>
      {value.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not valued" description={errorText(value.error)} />}
      {isLoading && <Spin />}
      {!isLoading && !selected && <Empty description="This unit has not been valued yet. Value it to see the calculation." />}
      {selected && (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Space wrap>
            <Select aria-label="Valuation" style={{ minWidth: 360 }} value={selected.id} onChange={setSelectedId}
              options={valuations.map((v, i) => ({
                value: v.id, label: `${dayjs(v.computedAt).format('YYYY-MM-DD HH:mm')} — ${formatMoney(v.computedMarketValue)}${i === 0 ? ' (latest)' : ''}`,
              }))} />
            <Button type="primary" onClick={() => setAssessing(selected)}>Assess this valuation</Button>
          </Space>
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
              { title: 'Classification / use', render: (_, l) => [l.classificationName, l.subClassificationName, l.actualUseName].filter(Boolean).join(' / ') || '—' },
              { title: 'Quantity', align: 'right', render: (_, l) => (l.quantity !== null ? `${plain.format(l.quantity)} ${l.unit ?? ''}` : '—') },
              { title: 'Unit value', align: 'right', render: (_, l) => (l.unitValue !== null ? formatMoney(l.unitValue) : '—') },
              { title: 'Market value', dataIndex: 'marketValue', align: 'right', render: formatMoney },
            ]} />
        </Space>
      )}
      {assessing && <AssessModal rpuId={rpuId} valuation={assessing} onClose={() => setAssessing(null)} />}
    </Drawer>
  );
}

/** One row's calculation, inputs first and the market value last (the order the valuation stored). */
function Breakdown({ line }: { line: ValuationLineDto }) {
  return (
    <Descriptions size="small" column={{ xs: 1, md: 3 }}>
      {line.breakdown.map((b) => (
        <Descriptions.Item key={b.key} label={humanize(b.key)}>
          {b.key === 'MarketValue' ? <b>{formatMoney(b.value)}</b> : plain.format(b.value)}
        </Descriptions.Item>
      ))}
    </Descriptions>
  );
}

interface AssessForm { assessmentYear: number; effectiveDate: Dayjs; previousAssessmentId?: string; remarks?: string }

/**
 * Creates a draft assessment of a valuation after a preview of its rows and
 * levels. PRIME proposes no effective date — when an assessment takes effect
 * is the LGU's rule to confirm (docs/analysis/value-and-assess.md §6.5).
 */
function AssessModal({ rpuId, valuation, onClose }: { rpuId: string; valuation: ValuationDto; onClose: () => void }) {
  const [form] = Form.useForm<AssessForm>();
  const { data: assessments = [] } = useRpuAssessments(rpuId);
  const posted = assessments.filter((a) => a.status === 'Posted').sort((a, b) => b.effectiveDate.localeCompare(a.effectiveDate));
  const preview = usePreviewAssessment();
  const create = useCreateAssessment(rpuId);
  const [shown, setShown] = useState<AssessmentPreviewDto | null>(null);
  const previousId = Form.useWatch('previousAssessmentId', form);
  const previous = posted.find((a) => a.id === previousId);

  const request = (v: AssessForm): CreateAssessmentRequest => ({
    valuationId: valuation.id, assessmentYear: v.assessmentYear, effectiveDate: v.effectiveDate.format('YYYY-MM-DD'),
    previousAssessmentId: v.previousAssessmentId ?? null, revisionReference: null, remarks: v.remarks?.trim() || null,
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
        Market value {formatMoney(valuation.computedMarketValue)}, valued {dayjs(valuation.computedAt).format('YYYY-MM-DD HH:mm')}.
      </Typography.Paragraph>
      {(preview.isError || create.isError) && (
        <Alert type="error" showIcon style={{ marginBottom: 12 }} title={create.isError ? 'Not created' : 'No preview'} description={errorText(create.error ?? preview.error)} />
      )}
      <Form form={form} layout="vertical" initialValues={{ previousAssessmentId: posted[0]?.id }} onValuesChange={() => setShown(null)}>
        <Row gutter={12}>
          <Col xs={12} md={6}>
            <Form.Item name="assessmentYear" label="Assessment year" rules={[{ required: true, message: 'Enter the year' }]}>
              <InputNumber min={1900} max={2200} precision={0} style={{ width: '100%' }} />
            </Form.Item>
          </Col>
          <Col xs={12} md={6}>
            <Form.Item name="effectiveDate" label="Effective date" rules={[{ required: true, message: 'Enter the effective date' }]}
              extra="Not proposed: the effectivity rule is the LGU's to confirm.">
              <DatePicker style={{ width: '100%' }} />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="previousAssessmentId" label="Previous assessment" extra="The unit's latest posted assessment is proposed.">
              <Select allowClear placeholder="None — first assessment of this unit"
                options={posted.map((a) => ({ value: a.id, label: `${a.assessmentYear}, effective ${a.effectiveDate} — AV ${formatMoney(a.assessedValue)}` }))} />
            </Form.Item>
          </Col>
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
