import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Button, Card, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useCreateSmvPreparation, useSmvPreparations } from '../../api/smvPreparations';
import { useAllMunicipalities } from '../../api/referenceData';
import { useCurrentUser } from '../../api/offices';
import { ApiRequestError } from '../../lib/apiClient';
import {
  preparationStatusColor, preparationStatusLabel, type SmvPreparationStatus, type SmvPreparationSummaryDto,
} from '../../lib/smvPreparationTypes';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);

/**
 * SMV preparation (RA 12001 §§15–17; LAM 2025 Book IV Chs. II–III; docs/analysis/smv-preparation-general-revision.md §4.2): one
 * work file per revision cycle, prepared by the Provincial Assessor's Office; municipal offices read it.
 */
export function SmvPreparationsPage() {
  const navigate = useNavigate();
  const { data = [], isLoading } = useSmvPreparations();
  const me = useCurrentUser();
  const provincial = !me.data?.municipalityIds;
  const [creating, setCreating] = useState(false);
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Typography.Title level={3} style={{ margin: 0 }}>SMV Preparation</Typography.Title>
        {provincial && <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>New preparation</Button>}
      </Space>
      <Card>
        <Table<SmvPreparationSummaryDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false}
          locale={{ emptyText: 'No SMV preparation yet' }} scroll={{ x: 'max-content' }}
          onRow={(r) => ({ onClick: () => navigate(`/smv-preparation/${r.id}`), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Revision year', dataIndex: 'revisionYear' },
            { title: 'Title', dataIndex: 'title' },
            { title: 'Effectivity', dataIndex: 'effectivityDate' },
            { title: 'Consultations', dataIndex: 'consultationCount', align: 'right' },
            { title: 'Status', dataIndex: 'status', render: (s: SmvPreparationStatus) => <Tag color={preparationStatusColor[s]}>{preparationStatusLabel[s]}</Tag> },
          ]} />
      </Card>
      {creating && <CreateModal onClose={(id) => { setCreating(false); if (id) navigate(`/smv-preparation/${id}`); }} />}
    </Space>
  );
}

function CreateModal({ onClose }: { onClose: (id?: string) => void }) {
  const create = useCreateSmvPreparation();
  const { data: municipalities = [] } = useAllMunicipalities();
  const nextYear = dayjs().year() + 1;
  return (
    <Modal open title="New SMV preparation" footer={null} width={720} destroyOnHidden onCancel={() => onClose()}>
      <Typography.Paragraph type="secondary">
        Opens the work file and its proposed SMV, a draft without a certification reference. Its rows are entered on the Valuation Rules page;
        it is approved in PRIME only after the certified SMV is published.
      </Typography.Paragraph>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not create" description={errorText(create.error)} />}
      <Form layout="vertical" initialValues={{ revisionYear: nextYear, dateOfValuation: dayjs(`${dayjs().year()}-01-01`), plannedEffectivityDate: dayjs(`${nextYear}-01-01`) }}
        onFinish={(v) => create.mutate({
          revisionYear: v.revisionYear, title: v.title, dateOfValuation: v.dateOfValuation?.format('YYYY-MM-DD') ?? null,
          baseValuationDate: v.baseValuationDate?.format('YYYY-MM-DD') ?? null, plannedEffectivityDate: v.plannedEffectivityDate.format('YYYY-MM-DD'),
          municipalityIds: v.municipalityIds ?? [], notes: v.notes ?? null,
        }, { onSuccess: (r) => onClose(r.id) })}>
        <Space wrap>
          <Form.Item name="revisionYear" label="Revision year" rules={[{ required: true }]}><InputNumber<number> min={1990} max={2200} precision={0} /></Form.Item>
          <Form.Item name="title" label="Title" rules={[{ required: true }, { max: 300 }]}><Input style={{ width: 360 }} /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="dateOfValuation" label="Date of valuation"><DatePicker /></Form.Item>
          <Form.Item name="baseValuationDate" label="Base valuation date (planned submission)"><DatePicker /></Form.Item>
          <Form.Item name="plannedEffectivityDate" label="Planned effectivity" rules={[{ required: true }]}><DatePicker /></Form.Item>
        </Space>
        <Form.Item name="municipalityIds" label="Coverage (empty: the whole province)">
          <Select mode="multiple" showSearch optionFilterProp="label" options={municipalities.map((m) => ({ value: m.id, label: m.name }))} />
        </Form.Item>
        <Form.Item name="notes" label="Team notes"><Input.TextArea rows={3} maxLength={4000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={create.isPending}>Create</Button>
      </Form>
    </Modal>
  );
}
