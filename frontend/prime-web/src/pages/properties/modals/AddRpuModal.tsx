import { Alert, Button, DatePicker, Form, Input, Modal, Select } from 'antd';
import dayjs from 'dayjs';
import { useCreateRpu } from '../../../api/rpus';
import type { CreateRpuRequest } from '../../../lib/types';
import { ApiRequestError } from '../../../lib/apiClient';

const rpuTypeOptions = [
  { value: 'Land', label: 'Land' },
  { value: 'Building', label: 'Building' },
  { value: 'Machinery', label: 'Machinery' },
  { value: 'OtherImprovement', label: 'Other Improvement' },
];

export function AddRpuModal({ propertyId, open, onClose }: { propertyId: string; open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<{ rpuNumber: string; rpuType: CreateRpuRequest['rpuType']; effectivityDate: dayjs.Dayjs }>();
  const createRpu = useCreateRpu(propertyId);

  function handleClose() {
    form.resetFields();
    onClose();
  }

  return (
    <Modal title="Add Real Property Unit (RPU)" open={open} onCancel={handleClose} footer={null} destroyOnHidden>
      {createRpu.isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title="Could not add RPU"
          description={createRpu.error instanceof ApiRequestError ? createRpu.error.apiError.message : (createRpu.error as Error).message}
        />
      )}

      <Form
        form={form}
        layout="vertical"
        initialValues={{ effectivityDate: dayjs() }}
        onFinish={(values) => {
          const request: CreateRpuRequest = {
            propertyId,
            rpuNumber: values.rpuNumber,
            rpuType: values.rpuType,
            effectivityDate: values.effectivityDate.format('YYYY-MM-DD'),
          };
          createRpu.mutate(request, { onSuccess: handleClose });
        }}
      >
        <Form.Item name="rpuNumber" label="RPU Number" rules={[{ required: true, message: 'RPU number is required' }]}>
          <Input />
        </Form.Item>

        <Form.Item name="rpuType" label="RPU Type" rules={[{ required: true, message: 'Select an RPU type' }]}>
          <Select options={rpuTypeOptions} />
        </Form.Item>

        <Form.Item name="effectivityDate" label="Effectivity Date" rules={[{ required: true }]}>
          <DatePicker style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={createRpu.isPending}>
            Add RPU
          </Button>
        </Form.Item>
      </Form>
    </Modal>
  );
}
