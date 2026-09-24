import { Alert, Button, Form, InputNumber, Modal, Select } from 'antd';
import { useCreateBuilding } from '../../../api/buildings';
import { useActualUses, useBuildingTypes, useConditions, useStructuralTypes } from '../../../api/referenceData';
import type { CreateBuildingRequest } from '../../../lib/types';
import { ApiRequestError } from '../../../lib/apiClient';

export function AddBuildingModal({
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
  const { data: buildingTypes } = useBuildingTypes();
  const { data: structuralTypes } = useStructuralTypes();
  const { data: actualUses } = useActualUses();
  const { data: conditions } = useConditions();
  const createBuilding = useCreateBuilding(propertyId);

  function handleClose() {
    form.resetFields();
    onClose();
  }

  return (
    <Modal title="Add Building" open={open} onCancel={handleClose} footer={null} destroyOnHidden width={600}>
      {createBuilding.isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title="Could not add Building"
          description={
            createBuilding.error instanceof ApiRequestError ? createBuilding.error.apiError.message : (createBuilding.error as Error).message
          }
        />
      )}

      <Form
        form={form}
        layout="vertical"
        initialValues={{ numberOfStoreys: 1, completionPercentage: 100 }}
        onFinish={(values) => {
          const request: CreateBuildingRequest = {
            rpuId,
            buildingTypeId: values.buildingTypeId,
            structuralTypeId: values.structuralTypeId,
            actualUseId: values.actualUseId,
            numberOfStoreys: values.numberOfStoreys,
            floorArea: values.floorArea,
            totalFloorArea: values.totalFloorArea,
            yearConstructed: values.yearConstructed,
            yearCompleted: values.yearCompleted,
            conditionId: values.conditionId,
            completionPercentage: values.completionPercentage,
          };
          createBuilding.mutate(request, { onSuccess: handleClose });
        }}
      >
        <Form.Item name="buildingTypeId" label="Building Type" rules={[{ required: true, message: 'Select a building type' }]}>
          <Select options={buildingTypes?.map((b) => ({ value: b.id, label: b.name }))} />
        </Form.Item>

        <Form.Item name="structuralTypeId" label="Structural Type" rules={[{ required: true, message: 'Select a structural type' }]}>
          <Select options={structuralTypes?.map((s) => ({ value: s.id, label: s.name }))} />
        </Form.Item>

        <Form.Item name="actualUseId" label="Actual Use" rules={[{ required: true, message: 'Select an actual use' }]}>
          <Select options={actualUses?.map((a) => ({ value: a.id, label: a.name }))} />
        </Form.Item>

        <Form.Item name="conditionId" label="Condition" rules={[{ required: true, message: 'Select a condition' }]}>
          <Select options={conditions?.map((c) => ({ value: c.id, label: c.name }))} />
        </Form.Item>

        <Form.Item name="numberOfStoreys" label="Number of Storeys">
          <InputNumber min={1} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="floorArea" label="Floor Area (sqm)" rules={[{ required: true, message: 'Required' }]}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="totalFloorArea" label="Total Floor Area (sqm)" rules={[{ required: true, message: 'Required' }]}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="yearConstructed" label="Year Constructed (optional)">
          <InputNumber min={1800} max={2200} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="yearCompleted" label="Year Completed (optional)">
          <InputNumber min={1800} max={2200} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="completionPercentage" label="Completion Percentage">
          <InputNumber min={0} max={100} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={createBuilding.isPending}>
            Add Building
          </Button>
        </Form.Item>
      </Form>
    </Modal>
  );
}
