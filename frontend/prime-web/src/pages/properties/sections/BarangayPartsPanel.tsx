import { useState } from 'react';
import { Alert, Button, Form, Input, InputNumber, Modal, Select, Space, Table, Typography } from 'antd';
import { EditOutlined, MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import { useBarangayParts, useSetBarangayParts, type BarangayPartDto } from '../../../api/territorialChanges';
import { useBarangays } from '../../../api/referenceData';
import { ApiRequestError } from '../../../lib/apiClient';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const plain = new Intl.NumberFormat('en-PH', { maximumFractionDigits: 4 });

/**
 * A parcel crossed by a barangay line (LAM Book II p.59; docs/analysis/identification-numbering.md §4.3): each
 * barangay's part, area and share of the assessed value, printed as the TD annotation. The property carries the
 * barangay of its larger part.
 */
export function BarangayPartsPanel({ propertyId, municipalityId }: { propertyId: string; municipalityId?: string }) {
  const { data = [] } = useBarangayParts(propertyId);
  const [editing, setEditing] = useState(false);
  return (
    <div style={{ marginTop: 16 }}>
      <Space style={{ justifyContent: 'space-between', width: '100%' }}>
        <Typography.Text strong>Crossed by a barangay line</Typography.Text>
        <Button size="small" icon={<EditOutlined />} onClick={() => setEditing(true)} disabled={!municipalityId}>Edit</Button>
      </Space>
      {data.length === 0
        ? <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0' }}>No: the parcel lies in one barangay.</Typography.Paragraph>
        : (
          <Table<BarangayPartDto> scroll={{ x: true }} size="small" rowKey="sequence" dataSource={data} pagination={false} style={{ marginTop: 8 }}
            columns={[
              { title: 'Barangay', render: (_, p) => `${p.barangayName}${p.barangayIndex ? ` (${p.barangayIndex})` : ''}` },
              { title: 'Area', dataIndex: 'area', align: 'right', render: (v: number) => `${plain.format(v)} sqm` },
              { title: 'Share of assessed value', dataIndex: 'assessedValueShare', align: 'right', render: (v: number) => `${plain.format(v)}%` },
            ]} />
        )}
      {editing && municipalityId && <EditModal propertyId={propertyId} municipalityId={municipalityId} parts={data} onClose={() => setEditing(false)} />}
    </div>
  );
}

function EditModal({ propertyId, municipalityId, parts, onClose }: { propertyId: string; municipalityId: string; parts: BarangayPartDto[]; onClose: () => void }) {
  const save = useSetBarangayParts(propertyId);
  const { data: barangays = [] } = useBarangays(municipalityId);
  return (
    <Modal open title="Barangay parts" footer={null} onCancel={onClose} width={640} destroyOnHidden>
      {save.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not saved" description={errorText(save.error)} />}
      <Typography.Paragraph type="secondary">Leave no rows if the parcel lies in one barangay. The shares total 100%.</Typography.Paragraph>
      <Form layout="vertical" initialValues={{ parts: parts.map((p) => ({ barangayId: p.barangayId, area: p.area, assessedValueShare: p.assessedValueShare })) }}
        onFinish={(v) => save.mutate({ parts: v.parts ?? [], reason: v.reason.trim() }, { onSuccess: onClose })}>
        <Form.List name="parts">
          {(fields, { add, remove }) => (
            <>
              {fields.map((field) => (
                <Space key={field.key} align="baseline" wrap style={{ display: 'flex' }}>
                  <Form.Item name={[field.name, 'barangayId']} rules={[{ required: true, message: 'Barangay' }]}>
                    <Select aria-label="Part barangay" placeholder="Barangay" style={{ width: 220 }} options={barangays.map((b) => ({ value: b.id, label: b.name }))} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'area']} rules={[{ required: true, message: 'Area' }]}>
                    <InputNumber<number> aria-label="Part area" placeholder="area (sqm)" min={0.0001} style={{ width: 140 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'assessedValueShare']} rules={[{ required: true, message: 'Share' }]}>
                    <InputNumber<number> aria-label="Part share" placeholder="share %" min={0} max={100} style={{ width: 110 }} />
                  </Form.Item>
                  <MinusCircleOutlined aria-label="Remove part" onClick={() => remove(field.name)} />
                </Space>
              ))}
              <Button type="dashed" icon={<PlusOutlined />} onClick={() => add()} style={{ margin: '4px 0 12px' }}>Add part</Button>
            </>
          )}
        </Form.List>
        <Form.Item name="reason" label="Reason" rules={[{ required: true, whitespace: true }, { max: 500 }]}><Input placeholder="e.g. the approved survey plan" /></Form.Item>
        <Button type="primary" htmlType="submit" loading={save.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}
