import { Alert, Button, Form, Input, InputNumber, Modal, Select } from 'antd';
import { useCreateParcel } from '../../../api/parcels';
import { useZones } from '../../../api/referenceData';
import { LocationSelect } from '../../../components/LocationSelect';
import { useState } from 'react';
import type { CreateParcelRequest } from '../../../lib/types';
import { ApiRequestError } from '../../../lib/apiClient';

export function AddParcelModal({ propertyId, open, onClose }: { propertyId: string; open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<Omit<CreateParcelRequest, 'propertyId'> & { provinceId?: string; municipalityId?: string }>();
  const [provinceId, setProvinceId] = useState<string>();
  const [municipalityId, setMunicipalityId] = useState<string>();
  const { data: zones } = useZones();
  const createParcel = useCreateParcel(propertyId);

  function handleClose() {
    form.resetFields();
    onClose();
  }

  return (
    <Modal title="Add Parcel" open={open} onCancel={handleClose} footer={null} destroyOnHidden>
      {createParcel.isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title="Could not add parcel"
          description={createParcel.error instanceof ApiRequestError ? createParcel.error.apiError.message : (createParcel.error as Error).message}
        />
      )}

      <Form
        form={form}
        layout="vertical"
        onFinish={(values) => {
          const request: CreateParcelRequest = {
            propertyId,
            barangayId: values.barangayId,
            zoneId: values.zoneId,
            geometryWkt: values.geometryWkt || undefined,
            area: values.area,
            surveyNumber: values.surveyNumber,
            lotNumber: values.lotNumber,
            blockNumber: values.blockNumber,
          };
          createParcel.mutate(request, { onSuccess: handleClose });
        }}
      >
        <LocationSelect
          provinceFieldName="provinceId"
          municipalityFieldName="municipalityId"
          barangayFieldName="barangayId"
          provinceId={provinceId}
          municipalityId={municipalityId}
          onProvinceChange={(id) => {
            setProvinceId(id);
            setMunicipalityId(undefined);
            form.setFieldsValue({ municipalityId: undefined, barangayId: undefined });
          }}
          onMunicipalityChange={(id) => {
            setMunicipalityId(id);
            form.setFieldsValue({ barangayId: undefined });
          }}
        />

        <Form.Item name="zoneId" label="Zone (optional)">
          <Select placeholder="Select zone" allowClear options={zones?.map((z) => ({ value: z.id, label: z.name }))} />
        </Form.Item>

        <Form.Item name="area" label="Area (sqm)">
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="lotNumber" label="Lot Number">
          <Input />
        </Form.Item>
        <Form.Item name="blockNumber" label="Block Number">
          <Input />
        </Form.Item>
        <Form.Item name="surveyNumber" label="Survey Number">
          <Input />
        </Form.Item>

        <Form.Item
          name="geometryWkt"
          label="Geometry (WKT, optional)"
          extra="Well-Known Text, e.g. POLYGON((121.0 14.5, 121.001 14.5, 121.001 14.501, 121.0 14.501, 121.0 14.5)). SRID is pending confirmation — see docs/DATABASE.md §9."
        >
          <Input.TextArea rows={3} placeholder="POLYGON((...))" />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={createParcel.isPending}>
            Add Parcel
          </Button>
        </Form.Item>
      </Form>
    </Modal>
  );
}
