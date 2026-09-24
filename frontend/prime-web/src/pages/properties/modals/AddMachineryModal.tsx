import { Alert, Button, DatePicker, Form, Input, InputNumber, Modal, Select } from 'antd';
import { useCreateMachinery } from '../../../api/machinery';
import { useMachineryTypes } from '../../../api/referenceData';
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
  const createMachinery = useCreateMachinery(propertyId);

  function handleClose() {
    form.resetFields();
    onClose();
  }

  return (
    <Modal title="Add Machinery" open={open} onCancel={handleClose} footer={null} destroyOnHidden width={600}>
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
            economicLifeYears: values.economicLifeYears,
            remainingLifeYears: values.remainingLifeYears,
          };
          createMachinery.mutate(request, { onSuccess: handleClose });
        }}
      >
        <Form.Item name="machineryTypeId" label="Machinery Type" rules={[{ required: true, message: 'Select a machinery type' }]}>
          <Select options={machineryTypes?.map((m) => ({ value: m.id, label: m.name }))} />
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

        <Form.Item name="economicLifeYears" label="Economic Life (years, optional)">
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="remainingLifeYears" label="Remaining Life (years, optional)">
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={createMachinery.isPending}>
            Add Machinery
          </Button>
        </Form.Item>
      </Form>
    </Modal>
  );
}
