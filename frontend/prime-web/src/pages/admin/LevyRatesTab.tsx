import { useState } from 'react';
import { Alert, Button, Card, Col, DatePicker, Form, Input, InputNumber, Modal, Row, Select, Table, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { levyKindLabel, useApproveLevyRate, useCreateLevyRate, useLevyRates, type LevyKind, type LevyRateDto } from '../../api/levyRates';
import { useAllMunicipalities, useClassifications } from '../../api/referenceData';
import { ApproveButton } from './ApproveButton';
import { day, errorText, lookup, pct, period, statusTag, useToast } from './ruleHelpers';

/**
 * Levy rates (docs/analysis/reporting.md §10, Q5, Q18): the basic tax, SEF and idle-land rates the ordinances set, per
 * municipality or province-wide, optionally per classification. They give the collectibles of the quarterly report only;
 * PRIME bills nothing. A new rate for the same levy, place and class takes over from its effective date.
 */
export function LevyRatesTab() {
  const { data = [], isLoading } = useLevyRates();
  const approve = useApproveLevyRate();
  const { context, fail } = useToast();
  const [creating, setCreating] = useState(false);
  return (
    <Card title="Levy rates (report figures)" extra={<Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>New rate</Button>}>
      {context}
      <Typography.Paragraph type="secondary">
        The rates of the basic real property tax, the Special Education Fund and the idle-land tax, as the ordinances set them. The quarterly report
        multiplies the taxable assessed values by the rates in force on the quarter&apos;s last day. A municipality&apos;s rate takes precedence over the
        province-wide one, and a rate for a classification over the rate for every class. None is built in.
      </Typography.Paragraph>
      <Table<LevyRateDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
        locale={{ emptyText: 'No levy rates: the report leaves the collectibles blank' }}
        columns={[
          { title: 'Levy', dataIndex: 'kind', render: (k: LevyKind) => levyKindLabel[k] },
          { title: 'Municipality', dataIndex: 'municipalityName', render: (v: string | null) => v ?? 'Whole province' },
          { title: 'Classification', dataIndex: 'classificationName', render: (v: string | null) => v ?? 'Every class' },
          { title: 'Rate', dataIndex: 'ratePercent', align: 'right', render: pct },
          { title: 'Legal basis', dataIndex: 'legalBasis' },
          { title: 'Period', render: (_, r) => period(r.effectiveDate, r.endDate) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          { title: '', render: (_, r) => <ApproveButton status={r.status} pending={approve.isPending} onApprove={() => approve.mutate(r.id, { onError: fail })} /> },
        ]} />
      {creating && <CreateLevyRateModal onClose={() => setCreating(false)} />}
    </Card>
  );
}

function CreateLevyRateModal({ onClose }: { onClose: () => void }) {
  const create = useCreateLevyRate();
  const [form] = Form.useForm();
  const { data: municipalities = [] } = useAllMunicipalities();
  const { data: classifications = [] } = useClassifications();
  return (
    <Modal open title="New levy rate (Draft)" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} width={720} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" onFinish={(v) => create.mutate({
        kind: v.kind, municipalityId: v.municipalityId ?? null, classificationId: v.classificationId ?? null, ratePercent: v.ratePercent,
        legalBasis: v.legalBasis, effectiveDate: day(v.effectiveDate)!, description: v.description || null, remarks: null,
      }, { onSuccess: onClose })}>
        <Row gutter={12}>
          <Col span={8}>
            <Form.Item name="kind" label="Levy" rules={[{ required: true }]}>
              <Select options={(Object.keys(levyKindLabel) as LevyKind[]).map((k) => ({ value: k, label: levyKindLabel[k] }))} />
            </Form.Item>
          </Col>
          <Col span={8}><Form.Item name="municipalityId" label="Municipality" extra="Blank: the whole province"><Select allowClear showSearch optionFilterProp="label" options={municipalities.map((m) => ({ value: m.id, label: m.name }))} /></Form.Item></Col>
          <Col span={8}><Form.Item name="classificationId" label="Classification" extra="Blank: every class"><Select allowClear showSearch optionFilterProp="label" options={lookup(classifications)} /></Form.Item></Col>
          <Col span={8}><Form.Item name="ratePercent" label="Rate % of assessed value" rules={[{ required: true }]}><InputNumber min={0.000001} max={100} precision={6} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={16}><Form.Item name="legalBasis" label="Ordinance (legal basis)" rules={[{ required: true }]}><Input maxLength={500} placeholder="The ordinance's number, date and section" /></Form.Item></Col>
          <Col span={8}><Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={16}><Form.Item name="description" label="Description"><Input maxLength={1000} /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  );
}
