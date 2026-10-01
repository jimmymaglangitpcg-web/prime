import { useState } from 'react';
import { Alert, Button, Card, Col, DatePicker, Form, Input, InputNumber, Modal, Row, Space, Table, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import {
  useApproveExchangeRate, useApprovePriceIndex, useCreateExchangeRate, useCreatePriceIndex, useExchangeRates, usePriceIndices,
} from '../../api/valuation';
import type { ExchangeRateDto, PriceIndexDto } from '../../lib/types';
import { day, errorText, statusTag, useToast } from './ruleHelpers';
import { ApproveButton } from './ApproveButton';

const number = new Intl.NumberFormat('en-PH', { maximumFractionDigits: 6 });

/**
 * Exchange rates and price indices for machinery (docs/analysis/valuation-foundation.md §4.6): dated
 * observations from their source (BSP, the index publisher), each approved by a second user and never edited.
 */
export function MachineryIndicesTab() {
  const [creating, setCreating] = useState<'rate' | 'index' | null>(null);
  const rates = useExchangeRates();
  const indices = usePriceIndices();
  const approveRate = useApproveExchangeRate();
  const approveIndex = useApprovePriceIndex();
  const { context, fail } = useToast();
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      {context}
      <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
        Machinery that is not brand-new and names a price index series is valued from its acquisition cost: converted at the rates on or before
        its acquisition and valuation dates (imported), and trended by the index of the valuation year over that of the acquisition year.
      </Typography.Paragraph>
      <Card title="Exchange rates" extra={<Button icon={<PlusOutlined />} onClick={() => setCreating('rate')}>New rate</Button>}>
        <Table<ExchangeRateDto> rowKey="id" size="small" loading={rates.isLoading} dataSource={rates.data ?? []} pagination={{ pageSize: 20 }} scroll={{ x: true }}
          columns={[
            { title: 'Currency', dataIndex: 'currency' },
            { title: 'Date', dataIndex: 'rateDate' },
            { title: 'Pesos per unit', dataIndex: 'pesosPerUnit', align: 'right', render: (v: number) => number.format(v) },
            { title: 'Source', dataIndex: 'source' },
            { title: 'Status', dataIndex: 'status', render: statusTag },
            { title: '', render: (_, r) => <ApproveButton status={r.status} pending={approveRate.isPending} onApprove={() => approveRate.mutate(r.id, { onError: fail })} /> },
          ]} />
      </Card>
      <Card title="Price indices" extra={<Button icon={<PlusOutlined />} onClick={() => setCreating('index')}>New index</Button>}>
        <Table<PriceIndexDto> rowKey="id" size="small" loading={indices.isLoading} dataSource={indices.data ?? []} pagination={{ pageSize: 20 }} scroll={{ x: true }}
          columns={[
            { title: 'Series', dataIndex: 'series' },
            { title: 'Year', dataIndex: 'year' },
            { title: 'Index', dataIndex: 'value', align: 'right', render: (v: number) => number.format(v) },
            { title: 'Source', dataIndex: 'source' },
            { title: 'Status', dataIndex: 'status', render: statusTag },
            { title: '', render: (_, r) => <ApproveButton status={r.status} pending={approveIndex.isPending} onApprove={() => approveIndex.mutate(r.id, { onError: fail })} /> },
          ]} />
      </Card>
      {creating === 'rate' && <CreateRateModal onClose={() => setCreating(null)} />}
      {creating === 'index' && <CreateIndexModal onClose={() => setCreating(null)} />}
    </Space>
  );
}

function CreateRateModal({ onClose }: { onClose: () => void }) {
  const create = useCreateExchangeRate();
  const [form] = Form.useForm();
  return (
    <Modal open title="New exchange rate" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" onFinish={(v) => create.mutate({
        currency: (v.currency as string).trim().toUpperCase(), rateDate: day(v.rateDate)!, pesosPerUnit: v.pesosPerUnit, source: v.source, remarks: null,
      }, { onSuccess: onClose })}>
        <Row gutter={12}>
          <Col xs={12} md={8}><Form.Item name="currency" label="Currency" rules={[{ required: true, len: 3, message: 'ISO code, e.g. USD' }]}><Input /></Form.Item></Col>
          <Col xs={12} md={8}><Form.Item name="rateDate" label="Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={24} md={8}><Form.Item name="pesosPerUnit" label="Pesos per unit" rules={[{ required: true }]}>
            <InputNumber<number> min={0.000001} style={{ width: '100%' }} />
          </Form.Item></Col>
        </Row>
        <Form.Item name="source" label="Source" rules={[{ required: true }, { max: 300 }]}><Input placeholder="e.g. BSP reference rate" /></Form.Item>
      </Form>
    </Modal>
  );
}

function CreateIndexModal({ onClose }: { onClose: () => void }) {
  const create = useCreatePriceIndex();
  const [form] = Form.useForm();
  return (
    <Modal open title="New price index" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" onFinish={(v) => create.mutate({ series: v.series.trim(), year: v.year, value: v.value, source: v.source, remarks: null },
        { onSuccess: onClose })}>
        <Row gutter={12}>
          <Col xs={24} md={10}><Form.Item name="series" label="Series" rules={[{ required: true }, { max: 30 }]}><Input placeholder="e.g. an origin country's" /></Form.Item></Col>
          <Col xs={12} md={7}><Form.Item name="year" label="Year" rules={[{ required: true }]}><InputNumber<number> min={1900} max={2200} precision={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={12} md={7}><Form.Item name="value" label="Index" rules={[{ required: true }]}><InputNumber<number> min={0.000001} style={{ width: '100%' }} /></Form.Item></Col>
        </Row>
        <Form.Item name="source" label="Source" rules={[{ required: true }, { max: 300 }]}><Input /></Form.Item>
      </Form>
    </Modal>
  );
}
