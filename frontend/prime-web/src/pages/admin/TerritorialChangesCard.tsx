import { useState } from 'react';
import { Alert, Button, Card, DatePicker, Descriptions, Form, Input, Modal, Progress, Select, Space, Table, Tag, Typography, message } from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import {
  useApproveTerritorialChange, useCreateTerritorialChange, useResumeTerritorialChange, useTerritorialChange, useTerritorialChanges,
  type TerritorialChangeDto,
} from '../../api/territorialChanges';
import { useAllMunicipalities, useBarangays } from '../../api/referenceData';
import { ApiRequestError } from '../../lib/apiClient';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const kindLabel = { CreatedLgu: 'New LGU created', TransferredTerritory: 'Territory transferred' } as const;
const modeLabel = { KeepParcelNumbers: 'Keep section and parcel numbers', TemporaryPins: 'Temporary PINs until re-tax-mapping' } as const;

/**
 * Territorial changes (LAM Book II p.40; docs/analysis/identification-numbering.md §4.3): the properties of the
 * barangays a law or court order moves get new PINs under the receiving barangays' index numbers. Created as a Draft,
 * approved by a second user, then run in the background with every old and new PIN recorded.
 */
export function TerritorialChangesCard() {
  const { data = [], isLoading } = useTerritorialChanges();
  const approve = useApproveTerritorialChange();
  const [creating, setCreating] = useState(false);
  const [viewing, setViewing] = useState<string>();
  const [toast, ctx] = message.useMessage();
  return (
    <Card title="Territorial changes" extra={<Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>New territorial change</Button>}>
      {ctx}
      <Typography.Paragraph type="secondary">
        When an LGU is created or territory moves to another LGU, every PIN of the moved barangays is retired and a new one assigned under the
        receiving barangay&apos;s index numbers. Set the receiving barangays and their index numbers first.
      </Typography.Paragraph>
      <Table<TerritorialChangeDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
        columns={[
          { title: 'Kind', dataIndex: 'kind', render: (v: TerritorialChangeDto['kind']) => kindLabel[v] },
          { title: 'Legal basis', dataIndex: 'legalBasis' },
          { title: 'Effective', dataIndex: 'effectiveDate' },
          { title: 'Barangays', render: (_, j) => j.mappings.map((m) => `${m.sourceBarangayName} → ${m.targetBarangayName}`).join('; ') },
          { title: 'New PINs', dataIndex: 'pinMode', render: (v: TerritorialChangeDto['pinMode']) => modeLabel[v] },
          { title: 'Status', render: (_, j) => <Space size={4}><Tag>{j.status}</Tag>{j.runStatus && <Tag color={j.runStatus === 'Completed' ? 'green' : j.runStatus === 'Failed' ? 'red' : 'blue'}>{j.runStatus}</Tag>}</Space> },
          { title: 'Properties', render: (_, j) => (j.status === 'Approved' ? `${j.processedCount} / ${j.totalCount}${j.failedCount ? `, ${j.failedCount} failed` : ''}` : '—') },
          {
            title: '', render: (_, j) => (
              <Space size={4}>
                {j.status === 'Draft' && <Button size="small" loading={approve.isPending} onClick={() => approve.mutate(j.id, { onError: (e) => toast.error(errorText(e)) })}>Approve and run</Button>}
                <Button size="small" onClick={() => setViewing(j.id)}>Details</Button>
              </Space>
            ),
          },
        ]} />
      {creating && <CreateModal onClose={() => setCreating(false)} />}
      {viewing && <DetailModal id={viewing} onClose={() => setViewing(undefined)} />}
    </Card>
  );
}

function BarangayPicker({ label, value, onChange }: { label: string; value?: string; onChange?: (v: string) => void }) {
  const { data: municipalities = [] } = useAllMunicipalities();
  const [municipalityId, setMunicipalityId] = useState<string>();
  const { data: barangays = [] } = useBarangays(municipalityId);
  return (
    <Space.Compact style={{ width: '100%' }}>
      <Select aria-label={`${label} municipality`} placeholder="Municipality" showSearch optionFilterProp="label" style={{ width: '50%' }} value={municipalityId}
        onChange={setMunicipalityId} options={municipalities.map((m) => ({ value: m.id, label: m.name }))} />
      <Select aria-label={`${label} barangay`} placeholder="Barangay" showSearch optionFilterProp="label" style={{ width: '50%' }} value={value}
        onChange={onChange} options={barangays.map((b) => ({ value: b.id, label: b.name }))} disabled={!municipalityId} />
    </Space.Compact>
  );
}

function CreateModal({ onClose }: { onClose: () => void }) {
  const create = useCreateTerritorialChange();
  const [form] = Form.useForm();
  return (
    <Modal open title="New territorial change (Draft)" okText="Create draft" onCancel={onClose} okButtonProps={{ loading: create.isPending }}
      onOk={() => form.submit()} width={760} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" initialValues={{ kind: 'TransferredTerritory', pinMode: 'KeepParcelNumbers', mappings: [{}] }}
        onFinish={(v) => create.mutate({
          kind: v.kind, legalBasis: v.legalBasis.trim(), effectiveDate: (v.effectiveDate as Dayjs).format('YYYY-MM-DD'), pinMode: v.pinMode,
          mappings: v.mappings.map((m: { sourceBarangayId: string; targetBarangayId: string }) => ({ sourceBarangayId: m.sourceBarangayId, targetBarangayId: m.targetBarangayId })),
          remarks: v.remarks?.trim() || null,
        }, { onSuccess: onClose })}>
        <Space wrap>
          <Form.Item name="kind" label="Kind" rules={[{ required: true }]}>
            <Select style={{ width: 220 }} options={Object.entries(kindLabel).map(([value, label]) => ({ value, label }))} />
          </Form.Item>
          <Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker /></Form.Item>
          <Form.Item name="pinMode" label="New PINs" rules={[{ required: true }]}>
            <Select style={{ width: 280 }} options={Object.entries(modeLabel).map(([value, label]) => ({ value, label }))} />
          </Form.Item>
        </Space>
        <Form.Item name="legalBasis" label="Legal basis (the law or court order)" rules={[{ required: true, whitespace: true }, { max: 500 }]}><Input /></Form.Item>
        <Typography.Paragraph strong style={{ marginBottom: 8 }}>Barangays moved (from → to)</Typography.Paragraph>
        <Form.List name="mappings">
          {(fields, { add, remove }) => (
            <>
              {fields.map((field) => (
                <Space key={field.key} align="baseline" style={{ display: 'flex', width: '100%' }} wrap>
                  <Form.Item name={[field.name, 'sourceBarangayId']} rules={[{ required: true, message: 'From' }]} style={{ minWidth: 300 }}>
                    <BarangayPicker label="From" />
                  </Form.Item>
                  <span>→</span>
                  <Form.Item name={[field.name, 'targetBarangayId']} rules={[{ required: true, message: 'To' }]} style={{ minWidth: 300 }}>
                    <BarangayPicker label="To" />
                  </Form.Item>
                  {fields.length > 1 && <MinusCircleOutlined aria-label="Remove barangay" onClick={() => remove(field.name)} />}
                </Space>
              ))}
              <Button type="dashed" icon={<PlusOutlined />} onClick={() => add()} style={{ margin: '4px 0 12px' }}>Add barangay</Button>
            </>
          )}
        </Form.List>
        <Form.Item name="remarks" label="Remarks"><Input maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
  );
}

function DetailModal({ id, onClose }: { id: string; onClose: () => void }) {
  const { data } = useTerritorialChange(id);
  const resume = useResumeTerritorialChange();
  return (
    <Modal open title="Territorial change" footer={null} onCancel={onClose} width={820} destroyOnHidden>
      {resume.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not resumed" description={errorText(resume.error)} />}
      {data && (
        <Space orientation="vertical" style={{ width: '100%' }}>
          <Descriptions size="small" column={{ xs: 1, md: 2 }}>
            <Descriptions.Item label="Kind">{kindLabel[data.kind]}</Descriptions.Item>
            <Descriptions.Item label="Legal basis">{data.legalBasis}</Descriptions.Item>
            <Descriptions.Item label="Effective">{data.effectiveDate}</Descriptions.Item>
            <Descriptions.Item label="New PINs">{modeLabel[data.pinMode]}</Descriptions.Item>
          </Descriptions>
          {data.status === 'Approved' && (
            <Progress percent={data.totalCount ? Math.round(((data.processedCount + data.failedCount) / data.totalCount) * 100) : 100}
              status={data.failedCount ? 'exception' : data.runStatus === 'Completed' ? 'success' : 'active'} />
          )}
          {data.failedCount > 0 && data.runStatus !== 'Running' && (
            <Button onClick={() => resume.mutate(id)} loading={resume.isPending}>Resume the failed properties</Button>
          )}
          <Table size="small" rowKey="propertyId" dataSource={data.items} pagination={{ pageSize: 20 }} scroll={{ x: true }}
            columns={[
              { title: 'Old PIN', dataIndex: 'oldPin' },
              { title: 'New PIN', dataIndex: 'newPin', render: (v: string | null) => v ?? '—' },
              { title: 'Status', dataIndex: 'status', render: (v: string) => <Tag color={v === 'Done' ? 'green' : v === 'Failed' ? 'red' : 'default'}>{v}</Tag> },
              { title: 'Error', dataIndex: 'error', render: (v: string | null) => v ?? '' },
            ]} />
        </Space>
      )}
    </Modal>
  );
}
