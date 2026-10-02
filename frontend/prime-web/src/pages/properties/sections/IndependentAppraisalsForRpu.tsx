import { useState } from 'react';
import { Alert, Button, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table, Tag, Tooltip, Typography } from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useCreateIndependentAppraisal, useIndependentAppraisals, useWithdrawIndependentAppraisal } from '../../../api/independentAppraisals';
import { useLandByRpu } from '../../../api/land';
import { useBuildingByRpu } from '../../../api/buildings';
import { useMachineryUnitsByRpu } from '../../../api/machinery';
import { ApiRequestError } from '../../../lib/apiClient';
import { formatMoney } from '../../../lib/format';
import {
  appraisalApproaches, type AppraisalApproach, type IndependentAppraisalDto, type IndependentAppraisalSubject, type RpuSummaryDto,
} from '../../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const plain = new Intl.NumberFormat('en-PH', { maximumFractionDigits: 6 });

/**
 * A unit's independent appraisals (docs/analysis/valuation-foundation.md §4.7): values the appraiser determined
 * outside the SMV, by the market, income or cost approach, with the basis, the evidence and any named inputs.
 * The current one of a subject replaces its SMV value in the next valuation; the assessment's approval reviews it.
 */
export function IndependentAppraisalsForRpu({ rpu }: { rpu: RpuSummaryDto }) {
  const { data = [], isLoading } = useIndependentAppraisals(rpu.id);
  const [recording, setRecording] = useState(false);
  const [withdrawing, setWithdrawing] = useState<IndependentAppraisalDto | null>(null);
  if (rpu.rpuType === 'OtherImprovement' || rpu.rpuType === 'MineralRight') {
    return null;
  }
  return (
    <div style={{ padding: '8px 24px' }}>
      <Space style={{ justifyContent: 'space-between', width: '100%' }} wrap>
        <Typography.Text strong>Independent appraisals</Typography.Text>
        <Button size="small" icon={<PlusOutlined />} onClick={() => setRecording(true)}>Record appraisal</Button>
      </Space>
      {data.length === 0 && !isLoading
        ? <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0' }}>None. Record one for what the SMV does not cover, or for special-purpose property.</Typography.Paragraph>
        : (
          <Table<IndependentAppraisalDto> size="small" rowKey="id" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }}
            style={{ marginTop: 8 }}
            expandable={{
              rowExpandable: (a) => a.inputs.length > 0,
              expandedRowRender: (a) => (
                <ul style={{ margin: 0 }}>{a.inputs.map((i) => <li key={i.sequence}>{i.name}: {plain.format(i.value)}{i.unit ? ` ${i.unit}` : ''}</li>)}</ul>
              ),
            }}
            columns={[
              { title: 'Subject', dataIndex: 'subjectLabel' },
              { title: 'Approach', dataIndex: 'approach', render: (v: AppraisalApproach) => appraisalApproaches.find((a) => a.value === v)?.label ?? v },
              { title: 'Value', dataIndex: 'value', align: 'right', render: formatMoney },
              { title: 'Appraised', dataIndex: 'appraisedOn' },
              { title: 'Basis', dataIndex: 'basis', render: (v: string) => <Typography.Text ellipsis={{ tooltip: v }} style={{ maxWidth: 240 }}>{v}</Typography.Text> },
              { title: 'Evidence', dataIndex: 'evidence', render: (v: string) => <Typography.Text ellipsis={{ tooltip: v }} style={{ maxWidth: 200 }}>{v}</Typography.Text> },
              {
                title: 'Status', render: (_, a) => a.isCurrent ? <Tag color="green">Current</Tag>
                  : <Tooltip title={a.endReason}><Tag>Ended</Tag></Tooltip>,
              },
              { title: '', render: (_, a) => a.isCurrent && <Button size="small" onClick={() => setWithdrawing(a)}>Withdraw</Button> },
            ]} />
        )}
      {recording && <RecordModal rpu={rpu} onClose={() => setRecording(false)} />}
      {withdrawing && <WithdrawModal appraisal={withdrawing} onClose={() => setWithdrawing(null)} />}
    </div>
  );
}

interface InputForm { name?: string; value?: number; unit?: string }

/** The subjects of the unit: its land, building and its additional items, or its machines. */
function useSubjects(rpu: RpuSummaryDto) {
  const land = useLandByRpu(rpu.rpuType === 'Land' ? rpu.id : undefined);
  const building = useBuildingByRpu(rpu.rpuType === 'Building' ? rpu.id : undefined);
  const machines = useMachineryUnitsByRpu(rpu.rpuType === 'Machinery' ? rpu.id : undefined);
  const subjects: { value: string; label: string; subject: IndependentAppraisalSubject }[] = [];
  if (land.data) subjects.push({ value: land.data.id, label: 'The land (its strips)', subject: 'Land' });
  if (building.data) {
    subjects.push({ value: building.data.id, label: `The building (${building.data.structuralTypeName})`, subject: 'Building' });
    for (const c of building.data.components.filter((x) => x.isAdditionalItem)) {
      subjects.push({ value: c.id, label: `Extra item: ${c.componentTypeName}${c.description ? ` (${c.description})` : ''}`, subject: 'BuildingComponent' });
    }
  }
  for (const m of machines.data ?? []) {
    subjects.push({ value: m.id, label: `Machine: ${[m.brand, m.model, m.serialNumber].filter(Boolean).join(' ') || m.machineryTypeName}`, subject: 'Machinery' });
  }
  return subjects;
}

function RecordModal({ rpu, onClose }: { rpu: RpuSummaryDto; onClose: () => void }) {
  const create = useCreateIndependentAppraisal(rpu.id);
  const subjects = useSubjects(rpu);
  return (
    <Modal open title="Record an independent appraisal" footer={null} onCancel={onClose} width={680} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not recorded" description={errorText(create.error)} />}
      <Typography.Paragraph type="secondary">
        The value replaces the subject&apos;s SMV value in the next valuation; a later appraisal of the same subject replaces this one.
        PRIME does not compute the approach: record the figures you used as inputs.
      </Typography.Paragraph>
      <Form layout="vertical" initialValues={{ approach: 'Market', appraisedOn: dayjs(), inputs: [] }}
        onFinish={(v) => {
          const s = subjects.find((x) => x.value === v.subjectId)!;
          create.mutate({
            rpuId: rpu.id, subject: s.subject, subjectId: s.value, approach: v.approach, value: v.value,
            appraisedOn: (v.appraisedOn as Dayjs).format('YYYY-MM-DD'), basis: v.basis.trim(), evidence: v.evidence.trim(),
            inputs: (v.inputs ?? []).map((i: InputForm) => ({ name: (i.name ?? '').trim(), value: i.value ?? 0, unit: i.unit?.trim() || null })),
          }, { onSuccess: onClose });
        }}>
        <Form.Item name="subjectId" label="What is appraised" rules={[{ required: true }]}>
          <Select options={subjects} notFoundContent="Record the land, building or machine first" />
        </Form.Item>
        <Space wrap>
          <Form.Item name="approach" label="Approach" rules={[{ required: true }]}><Select style={{ width: 180 }} options={appraisalApproaches} /></Form.Item>
          <Form.Item name="value" label="Market value" rules={[{ required: true }]}><InputNumber<number> min={0} style={{ width: 180 }} /></Form.Item>
          <Form.Item name="appraisedOn" label="Appraised on" rules={[{ required: true }]}><DatePicker /></Form.Item>
        </Space>
        <Form.Item name="basis" label="Basis" rules={[{ required: true, whitespace: true }, { max: 2000 }]}>
          <Input.TextArea rows={2} placeholder="e.g. net income capitalised at the market rate; comparable sales" />
        </Form.Item>
        <Form.Item name="evidence" label="Evidence" rules={[{ required: true, whitespace: true }, { max: 2000 }]}>
          <Input placeholder="e.g. document or file references" />
        </Form.Item>
        <Typography.Paragraph strong style={{ marginBottom: 8 }}>Inputs used (optional)</Typography.Paragraph>
        <Form.List name="inputs">
          {(fields, { add, remove }) => (
            <>
              {fields.map((field) => (
                <Space key={field.key} align="baseline" wrap style={{ display: 'flex' }}>
                  <Form.Item name={[field.name, 'name']} rules={[{ required: true, message: 'Name' }]}>
                    <Input aria-label="Input name" placeholder="e.g. Net income" maxLength={100} style={{ width: 220 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'value']} rules={[{ required: true, message: 'Value' }]}>
                    <InputNumber<number> aria-label="Input value" placeholder="value" style={{ width: 150 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'unit']}><Input aria-label="Input unit" placeholder="unit" maxLength={30} style={{ width: 100 }} /></Form.Item>
                  <MinusCircleOutlined aria-label="Remove input" onClick={() => remove(field.name)} />
                </Space>
              ))}
              <Button type="dashed" icon={<PlusOutlined />} onClick={() => add()} style={{ margin: '4px 0 12px' }}>Add input</Button>
            </>
          )}
        </Form.List>
        <Button type="primary" htmlType="submit" loading={create.isPending}>Record</Button>
      </Form>
    </Modal>
  );
}

function WithdrawModal({ appraisal, onClose }: { appraisal: IndependentAppraisalDto; onClose: () => void }) {
  const withdraw = useWithdrawIndependentAppraisal(appraisal.rpuId);
  return (
    <Modal open title="Withdraw the appraisal" footer={null} onCancel={onClose} destroyOnHidden>
      {withdraw.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not withdrawn" description={errorText(withdraw.error)} />}
      <Typography.Paragraph>
        {appraisal.subjectLabel}: {formatMoney(appraisal.value)}. After withdrawal the next valuation uses the SMV again; valuations already made keep it.
      </Typography.Paragraph>
      <Form layout="vertical" onFinish={(v) => withdraw.mutate({ id: appraisal.id, reason: v.reason.trim() }, { onSuccess: onClose })}>
        <Form.Item name="reason" label="Reason" rules={[{ required: true, whitespace: true }, { max: 500 }]}><Input /></Form.Item>
        <Button danger htmlType="submit" loading={withdraw.isPending}>Withdraw</Button>
      </Form>
    </Modal>
  );
}
