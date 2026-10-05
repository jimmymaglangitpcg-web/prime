import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Button, Card, Checkbox, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useCreateImpactStudy, useImpactStudies, type StudySummaryDto } from '../../api/impactStudies';
import { useSmvSimulations } from '../../api/smvTesting';
import { useCurrentUser } from '../../api/offices';
import { ApiRequestError } from '../../lib/apiClient';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);

/**
 * Revenue compliance and tax impact studies (RA 12001 §17; LAM 2025 Book IV pp.116–118; docs/analysis/smv-preparation-general-revision.md
 * §4.5): an SMV's simulation taxed now, at the new values, and under up to three options, with the Treasurer's rates and collection.
 */
export function ImpactStudiesPage() {
  const navigate = useNavigate();
  const { data = [], isLoading } = useImpactStudies();
  const me = useCurrentUser();
  const [creating, setCreating] = useState(false);
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Typography.Title level={3} style={{ margin: 0 }}>Revenue &amp; Tax Impact Study</Typography.Title>
        {!me.data?.municipalityIds && <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>New study</Button>}
      </Space>
      <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
        With the Treasurer, after the SMV is transmitted to the LCE and Sanggunian. Rates and collections are entered here with their source;
        PRIME does not read them from the treasury records and computes no bill. Options are study figures, not configuration.
      </Typography.Paragraph>
      <Card>
        <Table<StudySummaryDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }}
          locale={{ emptyText: 'No study yet' }} onRow={(r) => ({ onClick: () => navigate(`/smv-impact/${r.id}`), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Title', dataIndex: 'title' },
            { title: 'Year', dataIndex: 'year' },
            { title: 'SMV', dataIndex: 'smvReference' },
            { title: 'Values as of', dataIndex: 'simulationAsOf' },
            { title: 'Options', dataIndex: 'optionCount', align: 'right' },
          ]} />
      </Card>
      {creating && <CreateModal onClose={(id) => { setCreating(false); if (id) navigate(`/smv-impact/${id}`); }} />}
    </Space>
  );
}

function CreateModal({ onClose }: { onClose: (id?: string) => void }) {
  const create = useCreateImpactStudy();
  const { data: runs = [] } = useSmvSimulations();
  const completed = runs.filter((r) => r.status === 'Completed');
  return (
    <Modal open title="New revenue and tax impact study" footer={null} width={640} destroyOnHidden onCancel={() => onClose()}>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not create" description={errorText(create.error)} />}
      <Form layout="vertical" initialValues={{ year: dayjs().year() - 1, includeAllTaxableUnits: false }}
        onFinish={(v) => create.mutate({
          title: v.title, smvSimulationRunId: v.runId, year: v.year, referenceDate: v.referenceDate?.format('YYYY-MM-DD') ?? null,
          actualCollection: null, discounts: null, collectionSource: null, includeAllTaxableUnits: !!v.includeAllTaxableUnits, notes: null, rates: [], options: [],
        }, { onSuccess: (r) => onClose(r.id) })}>
        <Form.Item name="title" label="Title" rules={[{ required: true }, { max: 300 }]}><Input /></Form.Item>
        <Form.Item name="runId" label="Simulation of the SMV" rules={[{ required: true }]}
          extra="A completed simulation (SMV Testing) gives the new values of every unit.">
          <Select options={completed.map((r) => ({ value: r.id, label: `${r.smvReference} (${r.smvRevisionYear}) as of ${r.asOf} — ${r.municipalities.map((m) => m.name).join(', ')}` }))}
            notFoundContent="No completed simulation" />
        </Form.Item>
        <Space wrap>
          <Form.Item name="year" label="Revenue compliance year" rules={[{ required: true }]}><InputNumber<number> min={1990} max={2200} precision={0} /></Form.Item>
          <Form.Item name="referenceDate" label="Taxable values as of (default 1 January)"><DatePicker /></Form.Item>
        </Space>
        <Form.Item name="includeAllTaxableUnits" valuePropName="checked"><Checkbox>Every taxable unit, not only land parcels</Checkbox></Form.Item>
        <Button type="primary" htmlType="submit" loading={create.isPending}>Create</Button>
      </Form>
    </Modal>
  );
}
