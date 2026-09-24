import { Alert, Button, Checkbox, Form, Input, InputNumber, Modal, Select } from 'antd';
import { useCreateLand } from '../../../api/land';
import { useActualUses, useClassifications, useRoadTypes, useSubClassifications, useZones } from '../../../api/referenceData';
import type { CreateLandRequest } from '../../../lib/types';
import { ApiRequestError } from '../../../lib/apiClient';

export function AddLandModal({
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
  const { data: zones } = useZones();
  const { data: roadTypes } = useRoadTypes();
  const createLand = useCreateLand(propertyId);

  function handleClose() {
    form.resetFields();
    onClose();
  }

  return (
    <Modal title="Add Land" open={open} onCancel={handleClose} footer={null} destroyOnHidden width={600}>
      {createLand.isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title="Could not add Land"
          description={createLand.error instanceof ApiRequestError ? createLand.error.apiError.message : (createLand.error as Error).message}
        />
      )}

      <Form
        form={form}
        layout="vertical"
        initialValues={{ areaUnit: 'sqm', isCornerLot: false }}
        onFinish={(values) => {
          const request: CreateLandRequest = {
            rpuId,
            area: values.area,
            areaUnit: values.areaUnit,
            classificationId: values.classificationId,
            actualUseId: values.actualUseId,
            subClassificationId: values.subClassificationId,
            zoneId: values.zoneId,
            locationFactor: values.locationFactor,
            roadFrontage: values.roadFrontage,
            roadTypeId: values.roadTypeId,
            isCornerLot: values.isCornerLot ?? false,
            zoning: values.zoning,
          };
          createLand.mutate(request, { onSuccess: handleClose });
        }}
      >
        <Form.Item name="area" label="Area (sqm)" rules={[{ required: true, message: 'Required' }]}>
          <InputNumber min={0} style={{ width: '100%' }} />
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

        <Form.Item name="zoneId" label="Zone (optional)">
          <Select allowClear options={zones?.map((z) => ({ value: z.id, label: z.name }))} />
        </Form.Item>

        <Form.Item name="locationFactor" label="Location Factor (optional)">
          <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="roadFrontage" label="Road Frontage (optional)">
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="roadTypeId" label="Road Type (optional)">
          <Select allowClear options={roadTypes?.map((r) => ({ value: r.id, label: r.name }))} />
        </Form.Item>

        <Form.Item name="isCornerLot" valuePropName="checked">
          <Checkbox>Corner Lot</Checkbox>
        </Form.Item>

        <Form.Item name="zoning" label="Zoning (optional)">
          <Input />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={createLand.isPending}>
            Add Land
          </Button>
        </Form.Item>
      </Form>
    </Modal>
  );
}
