import { useState } from 'react';
import { Alert, Button, Card, Form, Input, Select, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { LocationSelect } from '../../components/LocationSelect';
import { useZones } from '../../api/referenceData';
import { useCreateProperty } from '../../api/properties';
import type { CreatePropertyRequest } from '../../lib/types';
import { ApiRequestError } from '../../lib/apiClient';

export function PropertyRegisterPage() {
  const navigate = useNavigate();
  const [form] = Form.useForm<CreatePropertyRequest>();
  const [provinceId, setProvinceId] = useState<string>();
  const [municipalityId, setMunicipalityId] = useState<string>();
  const { data: zones } = useZones();
  const createProperty = useCreateProperty();

  return (
    <Card style={{ maxWidth: 900 }}>
      <Typography.Title level={3}>Register Property</Typography.Title>
      <Typography.Paragraph type="secondary">
        Creates the permanent physical-property record (CLAUDE.md §19). Land,
        buildings, machinery, RPUs, and Tax Declarations are added afterward
        from the Property Profile.
      </Typography.Paragraph>

      {createProperty.isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title="Could not register property"
          description={
            createProperty.error instanceof ApiRequestError
              ? createProperty.error.apiError.message
              : (createProperty.error as Error).message
          }
        />
      )}

      <Form
        form={form}
        layout="vertical"
        onFinish={(values) => {
          createProperty.mutate(values, {
            onSuccess: (property) => navigate(`/properties/${property.id}`),
          });
        }}
      >
        <Form.Item
          name="propertyIdentificationNumber"
          label="Property Identification Number (PIN)"
          extra="Leave blank to generate it when a PIN numbering scheme is in force."
        >
          <Input placeholder="e.g. 012-34-567-890" />
        </Form.Item>

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

        <Form.Item name="street" label="Street">
          <Input />
        </Form.Item>
        <Form.Item name="sitio" label="Sitio">
          <Input />
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
        <Form.Item name="titleNumber" label="Title Number">
          <Input />
        </Form.Item>
        <Form.Item name="taxMapNumber" label="Tax Map Number">
          <Input />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={createProperty.isPending}>
            Register Property
          </Button>
        </Form.Item>
      </Form>
    </Card>
  );
}
