import { Alert, Button, Checkbox, DatePicker, Form, Input, InputNumber, Modal, Select, Space } from 'antd';
import dayjs from 'dayjs';
import { useCreateRpu, usePropertyRpus } from '../../../api/rpus';
import type { CreateRpuRequest, RpuSummaryDto } from '../../../lib/types';
import { ApiRequestError } from '../../../lib/apiClient';

const rpuTypeOptions = [
  { value: 'Land', label: 'Land' },
  { value: 'Building', label: 'Building' },
  { value: 'Machinery', label: 'Machinery' },
  { value: 'OtherImprovement', label: 'Other Improvement' },
  { value: 'MineralRight', label: 'Mineral right (apart from the surface)' },
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
    isLeasingProperty?: boolean; leasingHostId?: string; floorPrefix?: string; floorNumber?: number; unitNumber?: number;
  }>();
  const rpuType = Form.useWatch('rpuType', form);
  const leasingHostId = Form.useWatch('leasingHostId', form);
  const createRpu = useCreateRpu(propertyId);
  const { data: units = [] } = usePropertyRpus(propertyId);
  const lands = rpus.filter((r) => r.rpuType === 'Land');
  const buildings = rpus.filter((r) => r.rpuType === 'Building');
  // A leasing property (condominium) and its units (LAM Book II p.38; identification-numbering.md §4.2).
  const leasingProperties = units.filter((u) => u.rpuType === 'Building' && u.isLeasingProperty);

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
            hostRpuId: values.rpuType === 'Machinery' ? values.hostRpuId ?? null : values.rpuType === 'Building' ? values.leasingHostId ?? null : null,
            isLeasingProperty: values.rpuType === 'Building' && !values.leasingHostId && !!values.isLeasingProperty,
            floorPrefix: values.rpuType === 'Building' && values.leasingHostId ? values.floorPrefix ?? 'F' : null,
            floorNumber: values.rpuType === 'Building' && values.leasingHostId ? values.floorNumber ?? null : null,
            unitNumber: values.rpuType === 'Building' && values.leasingHostId ? values.unitNumber ?? null : null,
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
          <Form.Item name="hostRpuId" label="Installed in building or condominium unit" extra="The FAAS building reference: the building's owner and PIN.">
            <Select allowClear placeholder="None" options={buildings.map((r) => ({ value: r.id, label: `RPU ${r.rpuNumber}` }))} />
          </Form.Item>
        )}

        {rpuType === 'Building' && (
          <>
            <Form.Item name="leasingHostId" label="Unit of a leasing property (condominium)" extra="Leave empty for a building of its own.">
              <Select allowClear placeholder="None" options={leasingProperties.map((r) => ({ value: r.id, label: `${r.rpuNumber} (${r.unitPin})` }))} />
            </Form.Item>
            {leasingHostId ? (
              <Space wrap>
                <Form.Item name="floorPrefix" label="Floor" initialValue="F">
                  <Select style={{ width: 150 }} options={[{ value: 'F', label: 'F — floor' }, { value: 'B', label: 'B — basement' },
                    { value: 'P', label: 'P — parking' }, { value: 'M', label: 'M — mezzanine' }]} />
                </Form.Item>
                <Form.Item name="floorNumber" label="Floor number" rules={[{ required: true }]}><InputNumber min={1} max={99} precision={0} /></Form.Item>
                <Form.Item name="unitNumber" label="Unit number" extra="Empty: the next on the floor."><InputNumber min={1} max={999} precision={0} /></Form.Item>
              </Space>
            ) : (
              <Form.Item name="isLeasingProperty" valuePropName="checked">
                <Checkbox>Leasing property (condominium): its units are recorded under it, by floor</Checkbox>
              </Form.Item>
            )}
          </>
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
