import { Alert, Button, Checkbox, DatePicker, Form, Input, InputNumber, Modal, Select } from 'antd';
import { useCreateMachinery } from '../../../api/machinery';
import { useActualUses, useClassifications, useMachineryTypes } from '../../../api/referenceData';
import type { CreateMachineryRequest } from '../../../lib/types';
import { ApiRequestError } from '../../../lib/apiClient';

export function AddMachineryModal({
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
  const { data: machineryTypes } = useMachineryTypes();
  const { data: classifications = [] } = useClassifications();
  const { data: uses = [] } = useActualUses();
  const createMachinery = useCreateMachinery(propertyId);
  const isBrandNew: boolean = Form.useWatch('isBrandNew', form) ?? false;

  function handleClose() {
    form.resetFields();
    onClose();
  }

  return (
    <Modal title="Add machine" open={open} onCancel={handleClose} footer={null} destroyOnHidden width={600}>
      {createMachinery.isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title="Could not add Machinery"
          description={
            createMachinery.error instanceof ApiRequestError ? createMachinery.error.apiError.message : (createMachinery.error as Error).message
          }
        />
      )}

      <Form
        form={form}
        layout="vertical"
        onFinish={(values) => {
          const request: CreateMachineryRequest = {
            rpuId,
            machineryTypeId: values.machineryTypeId,
            description: values.description,
            brand: values.brand,
            model: values.model,
            serialNumber: values.serialNumber,
            capacity: values.capacity,
            capacityUnit: values.capacityUnit,
            dateAcquired: values.dateAcquired ? values.dateAcquired.format('YYYY-MM-DD') : null,
            acquisitionCost: values.acquisitionCost,
            installationCost: values.installationCost,
            otherCost: values.otherCost,
            isBrandNew: values.isBrandNew ?? false,
            replacementCost: values.isBrandNew ? null : values.replacementCost,
            economicLifeYears: values.economicLifeYears,
            remainingLifeYears: values.remainingLifeYears,
            classificationId: values.classificationId ?? null,
            actualUseId: values.actualUseId ?? null,
          };
          createMachinery.mutate(request, { onSuccess: handleClose });
        }}
      >
        <Form.Item name="machineryTypeId" label="Machinery Type" rules={[{ required: true, message: 'Select a machinery type' }]}>
          <Select options={machineryTypes?.map((m) => ({ value: m.id, label: m.name }))} />
        </Form.Item>

        <Form.Item name="classificationId" label="Classification (optional)" extra="Leave both empty to assess under the unit's Tax Declaration.">
          <Select allowClear options={classifications.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Form.Item name="actualUseId" label="Actual use (optional)">
          <Select allowClear options={uses.map((x) => ({ value: x.id, label: x.name }))} />
        </Form.Item>

        <Form.Item name="description" label="Description (optional)">
          <Input.TextArea rows={2} />
        </Form.Item>

        <Form.Item name="brand" label="Brand (optional)">
          <Input />
        </Form.Item>

        <Form.Item name="model" label="Model (optional)">
          <Input />
        </Form.Item>

        <Form.Item name="serialNumber" label="Serial Number (optional)">
          <Input />
        </Form.Item>

        <Form.Item name="capacity" label="Capacity (optional)">
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="capacityUnit" label="Capacity Unit (optional)">
          <Input />
        </Form.Item>

        <Form.Item name="dateAcquired" label="Date Acquired (optional)">
          <DatePicker style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="acquisitionCost" label="Acquisition Cost" rules={[{ required: true, message: 'Required' }]}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="installationCost" label="Installation Cost (optional)">
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="otherCost" label="Other Cost (optional)">
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item
          name="isBrandNew"
          valuePropName="checked"
          extra="Brand-new machinery is valued at acquisition cost; all other machinery from its replacement cost and remaining life (LGC §224)."
        >
          <Checkbox>Brand-new</Checkbox>
        </Form.Item>

        {!isBrandNew && (
          <>
            <Form.Item
              name="replacementCost"
              label="Replacement / Reproduction Cost"
              extra="Needed before this machinery can be valued."
            >
              <InputNumber min={0} style={{ width: '100%' }} />
            </Form.Item>

            <Form.Item name="economicLifeYears" label="Estimated Economic Life (years)" extra="Needed before this machinery can be valued.">
              <InputNumber min={0} style={{ width: '100%' }} />
            </Form.Item>

            <Form.Item name="remainingLifeYears" label="Remaining Economic Life (years)" extra="Needed before this machinery can be valued.">
              <InputNumber min={0} style={{ width: '100%' }} />
            </Form.Item>
          </>
        )}

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={createMachinery.isPending}>
            Add Machinery
          </Button>
        </Form.Item>
      </Form>
    </Modal>
  );
}
