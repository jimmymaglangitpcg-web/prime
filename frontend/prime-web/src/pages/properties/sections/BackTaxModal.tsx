import { useState } from 'react';
import { Alert, Button, Descriptions, Form, Input, InputNumber, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import { useBackTaxRuns, useCreateBackTaxRun, usePreviewBackTaxes } from '../../../api/backTaxes';
import { useTransactionTypes } from '../../../api/transactions';
import { ApiRequestError } from '../../../lib/apiClient';
import { formatMoney } from '../../../lib/format';
import type { BackTaxPeriodDto, BackTaxRequest, BackTaxRunDto } from '../../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);

function Periods({ run }: { run: BackTaxRunDto }) {
  return (
    <Table<BackTaxPeriodDto> size="small" rowKey="sequence" dataSource={run.periods} pagination={false} scroll={{ x: 'max-content' }}
      columns={[
        { title: '#', dataIndex: 'sequence', width: 40 },
        { title: 'From', dataIndex: 'startDate' },
        { title: 'To', dataIndex: 'endDate', render: (v: string | null) => v ?? <Tag color="blue">current</Tag> },
        { title: 'SMV', dataIndex: 'smvReference', render: (v: string | null) => v ?? '—' },
        { title: 'Market value', dataIndex: 'marketValue', align: 'right', render: (v: number | null) => (v === null || v === undefined ? '—' : formatMoney(v)) },
        { title: 'Assessed value', dataIndex: 'assessedValue', align: 'right', render: (v: number | null) => (v === null || v === undefined ? '—' : formatMoney(v)) },
        { title: 'Assessment', dataIndex: 'assessmentStatus', render: (v: string | null) => v ?? '—' },
      ]} />
  );
}

/**
 * Back taxes of a unit declared for the first time or discovered (docs/analysis/valuation-foundation.md §4.8):
 * from the year it should have been declared, at most the configured years before its initial assessment, one
 * valuation and Draft assessment per SMV period. They are approved as usual and posted in period order; each
 * posting prepares that period's FAAS/TD.
 */
export function BackTaxModal({ rpuId, onClose }: { rpuId: string; onClose: () => void }) {
  const { data: runs = [] } = useBackTaxRuns(rpuId);
  const preview = usePreviewBackTaxes();
  const create = useCreateBackTaxRun(rpuId);
  const { data: types = [] } = useTransactionTypes(true);
  const [form] = Form.useForm();
  const [shown, setShown] = useState<BackTaxRunDto | null>(null);
  const request = (v: { declaredFromYear: number; basis: string; initialAssessmentYear?: number; transactionTypeId?: string }): BackTaxRequest => ({
    rpuId, declaredFromYear: v.declaredFromYear, basis: v.basis.trim(), initialAssessmentYear: v.initialAssessmentYear ?? null,
    transactionTypeId: v.transactionTypeId ?? null,
  });
  const error = preview.error ?? create.error;
  return (
    <Modal open title="Back taxes" footer={null} onCancel={onClose} width={860} destroyOnHidden>
      <Typography.Paragraph type="secondary">
        For a unit declared for the first time or discovered: each SMV period from the year it should have been declared is valued and assessed
        under its own SMV and levels. The assessments are created as drafts, approved as usual and posted in order; each posting prepares that
        period&apos;s FAAS/TD. The tax itself is computed by the treasury.
      </Typography.Paragraph>
      {error && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not done" description={errorText(error)} />}
      <Form form={form} layout="vertical" initialValues={{ initialAssessmentYear: new Date().getFullYear() }}
        onValuesChange={() => setShown(null)}
        onFinish={(v) => create.mutate(request(v), { onSuccess: (run) => { setShown(run); form.resetFields(); } })}>
        <Space wrap align="start">
          <Form.Item name="declaredFromYear" label="Should have been declared from (year)" rules={[{ required: true }]}>
            <InputNumber<number> min={1900} max={2200} precision={0} style={{ width: 200 }} />
          </Form.Item>
          <Form.Item name="initialAssessmentYear" label="Year of initial assessment"><InputNumber<number> min={1900} max={2200} precision={0} /></Form.Item>
          <Form.Item name="transactionTypeId" label="Transaction" extra="Optional; its rule must be Periods.">
            <Select allowClear style={{ width: 240 }} options={types.map((t) => ({ value: t.id, label: `${t.code} — ${t.name}` }))} />
          </Form.Item>
        </Space>
        <Form.Item name="basis" label="Basis of that year" rules={[{ required: true, whitespace: true }, { max: 1000 }]}>
          <Input placeholder="e.g. certificate of completion, deed of sale, occupancy" />
        </Form.Item>
        <Space>
          <Button loading={preview.isPending}
            onClick={() => form.validateFields().then((v) => preview.mutate(request(v), { onSuccess: setShown, onError: () => setShown(null) }))}>Preview periods</Button>
          <Button type="primary" htmlType="submit" loading={create.isPending}>Create the period assessments</Button>
        </Space>
      </Form>
      {shown && (
        <div style={{ marginTop: 16 }}>
          <Descriptions size="small" column={{ xs: 1, md: 2 }} style={{ marginBottom: 8 }}>
            <Descriptions.Item label="Limit">{shown.yearsLimit} years before the initial assessment ({shown.yearsLimitLegalBasis})</Descriptions.Item>
            <Descriptions.Item label="Buildings / machinery">
              {shown.buildingRules === 'Current' ? 'current rules' : 'each period’s rules'} / {shown.machineryRules === 'Current' ? 'current rules' : 'each period’s rules'}
            </Descriptions.Item>
          </Descriptions>
          <Typography.Text strong>{shown.id ? 'Created' : 'Preview'}: {shown.periods.length} period(s)</Typography.Text>
          <Periods run={shown} />
        </div>
      )}
      {runs.length > 0 && !shown?.id && (
        <div style={{ marginTop: 16 }}>
          <Typography.Text strong>Back-tax runs of this unit</Typography.Text>
          {runs.map((r) => (
            <div key={r.id} style={{ marginTop: 8 }}>
              <Typography.Text type="secondary">From {r.declaredFromYear} (initial {r.initialAssessmentYear}): {r.basis}</Typography.Text>
              <Periods run={r} />
            </div>
          ))}
        </div>
      )}
    </Modal>
  );
}
