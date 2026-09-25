import { Alert, Button, DatePicker, Form, Input, Modal, Select } from 'antd';
import dayjs from 'dayjs';
import { useCreateRpu } from '../../../api/rpus';
import type { CreateRpuRequest, RpuSummaryDto } from '../../../lib/types';
import { ApiRequestError } from '../../../lib/apiClient';

const rpuTypeOptions = [
  { value: 'Land', label: 'Land' },
  { value: 'Building', label: 'Building' },
  { value: 'Machinery', label: 'Machinery' },
  { value: 'OtherImprovement', label: 'Other Improvement' },
];

/**
 * A building, machinery or other improvement names the land unit it stands
 * on, and machinery the building it is installed in — the FAAS "Land
 * Reference" and "Building Owner + PIN" blocks. PIN postscripts are assigned
 * by the server.
 */
export function AddRpuModal({ propertyId, rpus, open, onClose }: { propertyId: string; rpus: RpuSummaryDto[]; open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<{
    rpuNumber: string; rpuType: CreateRpuRequest['rpuType']; effectivityDate: dayjs.Dayjs; landRpuId?: string; hostRpuId?: string;
  }>();
  const rpuType = Form.useWatch('rpuType', form);
  const createRpu = useCreateRpu(propertyId);
  const lands = rpus.filter((r) => r.rpuType === 'Land');
  const buildings = rpus.filter((r) => r.rpuType === 'Building');

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
            landRpuId: values.rpuType !== 'Land' ? values.landRpuId ?? null : null,
            hostRpuId: values.rpuType === 'Machinery' ? values.hostRpuId ?? null : null,
          };
          createRpu.mutate(request, { onSuccess: handleClose });
        }}
      >
        <Form.Item name="rpuNumber" label="RPU Number" rules={[{ required: true, message: 'RPU number is required' }]}>
          <Input />
        </Form.Item>

        <Form.Item name="rpuType" label="RPU Type" rules={[{ required: true, message: 'Select an RPU type' }]}>
          <Select options={rpuTypeOptions} onChange={() => form.setFieldsValue({ landRpuId: lands.length === 1 ? lands[0].id : undefined, hostRpuId: undefined })} />
        </Form.Item>

        {rpuType && rpuType !== 'Land' && (
          <Form.Item name="landRpuId" label="Stands on land unit" extra="The FAAS land reference: the land's owner, title, lot and TD.">
            <Select allowClear placeholder="None" options={lands.map((r) => ({ value: r.id, label: `RPU ${r.rpuNumber}` }))} />
          </Form.Item>
        )}

        {rpuType === 'Machinery' && (
          <Form.Item name="hostRpuId" label="Installed in building" extra="The FAAS building reference: the building's owner and PIN.">
            <Select allowClear placeholder="None" options={buildings.map((r) => ({ value: r.id, label: `RPU ${r.rpuNumber}` }))} />
          </Form.Item>
        )}

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
