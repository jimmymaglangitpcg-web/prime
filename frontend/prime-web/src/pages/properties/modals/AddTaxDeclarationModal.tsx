import { Alert, Button, DatePicker, Form, Input, InputNumber, Modal, Radio, Select } from 'antd';
import dayjs from 'dayjs';
import { useCreateTaxDeclaration, useTaxDeclarationsByRpu } from '../../../api/taxDeclarations';
import { useActualUses, useClassifications, useSubClassifications } from '../../../api/referenceData';
import type { CreateTaxDeclarationRequest } from '../../../lib/types';
import { ApiRequestError } from '../../../lib/apiClient';

export function AddTaxDeclarationModal({
  propertyId,
  rpuId,
  open,
  onClose,
}: {
  propertyId: string;
  rpuId: string;
  open: boolean;
  onClose: () => void;
}) {
  const [form] = Form.useForm();
  const { data: classifications } = useClassifications();
  const { data: actualUses } = useActualUses();
  const { data: subClassifications } = useSubClassifications();
  const createTaxDeclaration = useCreateTaxDeclaration(propertyId);
  const { data: existing = [] } = useTaxDeclarationsByRpu(rpuId);
  const current = existing.find((td) => td.status === 'Approved');
  const replaceable = existing.filter((td) => !['Cancelled', 'Voided', 'Rejected'].includes(td.status));

  function handleClose() {
    form.resetFields();
    onClose();
  }

  return (
    <Modal title="Add Tax Declaration" open={open} onCancel={handleClose} footer={null} destroyOnHidden width={600}>
      {createTaxDeclaration.isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title="Could not add Tax Declaration"
          description={
            createTaxDeclaration.error instanceof ApiRequestError
              ? createTaxDeclaration.error.apiError.message
              : (createTaxDeclaration.error as Error).message
          }
        />
      )}

      <Form
        form={form}
        layout="vertical"
        initialValues={{ taxability: 'Taxable', effectivityDate: dayjs(), assessmentYear: dayjs().year(), previousTaxDeclarationId: current?.id }}
        onFinish={(values) => {
          const request: CreateTaxDeclarationRequest = {
            rpuId,
            taxDeclarationNumber: values.taxDeclarationNumber?.trim() || undefined,
            effectivityDate: values.effectivityDate.format('YYYY-MM-DD'),
            taxability: values.taxability,
            classificationId: values.classificationId,
            actualUseId: values.actualUseId,
            subClassificationId: values.subClassificationId,
            assessmentYear: values.assessmentYear,
            remarks: values.remarks,
            previousTaxDeclarationId: values.previousTaxDeclarationId,
          };
          createTaxDeclaration.mutate(request, { onSuccess: handleClose });
        }}
      >
        <Form.Item
          name="taxDeclarationNumber"
          label="Tax Declaration Number"
          extra="Leave blank to generate it when a TD numbering scheme is in force."
        >
          <Input />
        </Form.Item>

        <Form.Item
          name="previousTaxDeclarationId"
          label="Replaces TD"
          extra="When this TD is approved, the TD it replaces is cancelled (&quot;this declaration cancels …&quot;)."
        >
          <Select allowClear placeholder="None — first declaration for this RPU"
            options={replaceable.map((td) => ({ value: td.id, label: `${td.taxDeclarationNumber} (${td.status})` }))} />
        </Form.Item>

        <Form.Item name="taxability" label="Taxability" rules={[{ required: true }]}>
          <Radio.Group options={[{ value: 'Taxable', label: 'Taxable' }, { value: 'Exempt', label: 'Exempt' }]} />
        </Form.Item>

        <Form.Item name="classificationId" label="Classification" rules={[{ required: true, message: 'Select a classification' }]}>
          <Select options={classifications?.map((c) => ({ value: c.id, label: c.name }))} />
        </Form.Item>

        <Form.Item name="actualUseId" label="Actual Use" rules={[{ required: true, message: 'Select an actual use' }]}>
          <Select options={actualUses?.map((a) => ({ value: a.id, label: a.name }))} />
        </Form.Item>

        <Form.Item name="subClassificationId" label="Sub-Classification (optional)">
          <Select allowClear options={subClassifications?.map((s) => ({ value: s.id, label: s.name }))} />
        </Form.Item>

        <Form.Item name="assessmentYear" label="Assessment Year" rules={[{ required: true }]}>
          <InputNumber min={1900} max={2200} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="effectivityDate" label="Effectivity Date" rules={[{ required: true }]}>
          <DatePicker style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="remarks" label="Remarks">
          <Input.TextArea rows={2} />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={createTaxDeclaration.isPending}>
            Add Tax Declaration
          </Button>
        </Form.Item>
      </Form>
    </Modal>
  );
}
