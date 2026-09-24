import { useState } from 'react';
import { Alert, Button, DatePicker, Form, Input, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import dayjs from 'dayjs';
import { useAddAnnotation, useLiftAnnotation, useTdAnnotations } from '../../../api/taxDeclarations';
import { useAnnotationTypes } from '../../../api/referenceData';
import { ApiRequestError } from '../../../lib/apiClient';
import type { TaxDeclarationAnnotationDto, TaxDeclarationDto } from '../../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);

/**
 * Annotations on a Tax Declaration (e.g. a levy — LTOM §150). Never deleted:
 * lifting records who, when and why (docs/FORMS-REVISION-PLAN.md A4).
 */
export function TdAnnotationsModal({ td, propertyId, onClose }: { td: TaxDeclarationDto | null; propertyId: string; onClose: () => void }) {
  const { data = [], isLoading } = useTdAnnotations(td?.id);
  const { data: types = [] } = useAnnotationTypes();
  const add = useAddAnnotation(propertyId, td?.id, td?.rpuId);
  const lift = useLiftAnnotation(propertyId, td?.id, td?.rpuId);
  const [lifting, setLifting] = useState<TaxDeclarationAnnotationDto | null>(null);
  const [liftReason, setLiftReason] = useState('');
  const [liftRef, setLiftRef] = useState('');
  const [form] = Form.useForm();
  const canAnnotate = td !== null && !['Cancelled', 'Voided', 'Rejected'].includes(td.status);

  return (
    <Modal title={td ? `Annotations — TD ${td.taxDeclarationNumber}` : ''} open={td !== null} onCancel={onClose} footer={null} width={860} destroyOnHidden>
      <Table<TaxDeclarationAnnotationDto>
        rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No annotations' }}
        columns={[
          { title: 'Type', dataIndex: 'annotationTypeName' },
          { title: 'Effective', dataIndex: 'effectiveDate' },
          { title: 'Annotation', dataIndex: 'text' },
          { title: 'Reference', render: (_, a) => [a.referenceNumber, a.referenceDate].filter(Boolean).join(' · ') || '—' },
          {
            title: 'Status', render: (_, a) => a.liftedAt
              ? <span><Tag>Lifted</Tag>{new Date(a.liftedAt).toLocaleDateString('en-PH')} — {a.liftReason}</span>
              : <Tag color="orange">Active</Tag>,
          },
          {
            title: '', render: (_, a) => !a.liftedAt && (
              <Button size="small" onClick={() => { setLifting(a); setLiftReason(''); setLiftRef(''); lift.reset(); }}>Lift</Button>
            ),
          },
        ]}
      />

      {canAnnotate && (
        <>
          <Typography.Title level={5} style={{ marginTop: 16 }}>Add annotation</Typography.Title>
          {types.length === 0 && (
            <Alert type="info" showIcon style={{ marginBottom: 12 }} title="No annotation types are configured"
              description="Annotation kinds come from the LAM; add them to the AnnotationTypes reference table." />
          )}
          {add.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not add" description={errorText(add.error)} />}
          <Form form={form} layout="vertical" initialValues={{ effectiveDate: dayjs() }}
            onFinish={(v) => add.mutate({
              annotationTypeId: v.annotationTypeId, text: v.text, referenceNumber: v.referenceNumber,
              referenceDate: v.referenceDate?.format('YYYY-MM-DD'), effectiveDate: v.effectiveDate.format('YYYY-MM-DD'),
            }, { onSuccess: () => form.resetFields() })}>
            <Space wrap align="start">
              <Form.Item name="annotationTypeId" label="Type" rules={[{ required: true }]}>
                <Select style={{ width: 200 }} options={types.map((t) => ({ value: t.id, label: t.name }))} />
              </Form.Item>
              <Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker /></Form.Item>
              <Form.Item name="referenceNumber" label="Reference no."><Input style={{ width: 160 }} /></Form.Item>
              <Form.Item name="referenceDate" label="Reference date"><DatePicker /></Form.Item>
            </Space>
            <Form.Item name="text" label="Annotation" rules={[{ required: true }, { max: 2000 }]}><Input.TextArea rows={2} /></Form.Item>
            <Button type="primary" htmlType="submit" loading={add.isPending} disabled={types.length === 0}>Add annotation</Button>
          </Form>
        </>
      )}

      <Modal title="Lift annotation" open={lifting !== null} okText="Lift" onCancel={() => setLifting(null)}
        okButtonProps={{ disabled: liftReason.trim() === '', loading: lift.isPending }}
        onOk={() => lifting && lift.mutate({ id: lifting.id, reason: liftReason.trim(), reference: liftRef.trim() || undefined },
          { onSuccess: () => setLifting(null) })}
        destroyOnHidden>
        {lift.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not lift" description={errorText(lift.error)} />}
        <Typography.Paragraph>The annotation stays on record, marked as lifted.</Typography.Paragraph>
        <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={2} value={liftReason} onChange={(e) => setLiftReason(e.target.value)} />
        <Input aria-label="Reference" placeholder="Reference (e.g. official receipt no.)" style={{ marginTop: 8 }} value={liftRef} onChange={(e) => setLiftRef(e.target.value)} />
      </Modal>
    </Modal>
  );
}
