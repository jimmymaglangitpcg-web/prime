import { useState } from 'react';
import { Alert, Button, Form, Input, Radio } from 'antd';
import { LocationSelect } from './LocationSelect';
import { useCreateTaxpayer } from '../api/taxpayers';
import type { CreateTaxpayerRequest, TaxpayerDto } from '../lib/types';
import { ApiRequestError } from '../lib/apiClient';

export function TaxpayerForm({ onSuccess }: { onSuccess: (taxpayer: TaxpayerDto) => void }) {
  const [form] = Form.useForm<CreateTaxpayerRequest>();
  const [taxpayerType, setTaxpayerType] = useState<CreateTaxpayerRequest['taxpayerType']>('Individual');
  const [provinceId, setProvinceId] = useState<string>();
  const [municipalityId, setMunicipalityId] = useState<string>();
  const createTaxpayer = useCreateTaxpayer();

  return (
    <>
      {createTaxpayer.isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title="Could not register taxpayer"
          description={
            createTaxpayer.error instanceof ApiRequestError
              ? createTaxpayer.error.apiError.message
              : (createTaxpayer.error as Error).message
          }
        />
      )}

      <Form
        form={form}
        layout="vertical"
        initialValues={{ taxpayerType: 'Individual' }}
        onFinish={(values) => createTaxpayer.mutate(values, { onSuccess })}
      >
        <Form.Item name="taxpayerType" label="Taxpayer Type" rules={[{ required: true }]}>
          <Radio.Group
            onChange={(e) => setTaxpayerType(e.target.value)}
            options={[
              { value: 'Individual', label: 'Individual' },
              { value: 'Corporation', label: 'Corporation' },
              { value: 'Partnership', label: 'Partnership' },
              { value: 'Government', label: 'Government' },
              { value: 'Estate', label: 'Estate' },
              { value: 'Association', label: 'Association' },
              { value: 'Other', label: 'Other' },
            ]}
          />
        </Form.Item>

        {taxpayerType === 'Individual' ? (
          <>
            <Form.Item name="lastName" label="Last Name" rules={[{ required: true, message: 'Last name is required' }]}>
              <Input />
            </Form.Item>
            <Form.Item name="firstName" label="First Name" rules={[{ required: true, message: 'First name is required' }]}>
              <Input />
            </Form.Item>
            <Form.Item name="middleName" label="Middle Name">
              <Input />
            </Form.Item>
            <Form.Item name="suffix" label="Suffix">
              <Input placeholder="e.g. Jr., III" style={{ maxWidth: 150 }} />
            </Form.Item>
          </>
        ) : (
          <Form.Item name="corporateName" label="Corporate/Entity Name" rules={[{ required: true, message: 'Name is required' }]}>
            <Input />
          </Form.Item>
        )}

        <Form.Item name="tin" label="TIN">
          <Input />
        </Form.Item>

        <Form.Item name="address" label="Address">
          <Input />
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
          required={false}
        />

        <Form.Item name="contactNumber" label="Contact Number">
          <Input />
        </Form.Item>
        <Form.Item name="email" label="Email" rules={[{ type: 'email', message: 'Enter a valid email' }]}>
          <Input />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={createTaxpayer.isPending}>
            Register Taxpayer
          </Button>
        </Form.Item>
      </Form>
    </>
  );
}
