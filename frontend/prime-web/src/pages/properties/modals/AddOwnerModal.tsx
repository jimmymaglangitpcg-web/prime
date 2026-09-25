import { useState } from 'react';
import { Alert, Button, DatePicker, Form, InputNumber, Modal, Select, Space, Typography } from 'antd';
import dayjs from 'dayjs';
import { useTaxpayerSearch, useAddOwner } from '../../../api/taxpayers';
import { useOwnershipTypes } from '../../../api/referenceData';
import { TaxpayerForm } from '../../../components/TaxpayerForm';
import { partyRoleLabel, type AddOwnerRequest, type PropertyPartyRole, type RpuSummaryDto, type TaxpayerDto } from '../../../lib/types';
import { ApiRequestError } from '../../../lib/apiClient';

export function AddOwnerModal({ propertyId, units, open, onClose }: { propertyId: string; units: RpuSummaryDto[]; open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<{
    role: PropertyPartyRole; taxpayerId?: string; ownershipTypeId?: string; ownershipPercentage?: number; startDate: dayjs.Dayjs; rpuId?: string;
  }>();
  const role = Form.useWatch('role', form) ?? 'Owner';
  const [taxpayerSearchTerm, setTaxpayerSearchTerm] = useState('');
  const [showCreateTaxpayer, setShowCreateTaxpayer] = useState(false);

  const { data: taxpayerResults, isFetching: searchingTaxpayers } = useTaxpayerSearch(
    { searchTerm: taxpayerSearchTerm, pageSize: 10 },
    { enabled: taxpayerSearchTerm.length >= 2 },
  );
  const { data: ownershipTypes } = useOwnershipTypes();
  const addOwner = useAddOwner(propertyId);

  function handleClose() {
    form.resetFields();
    setShowCreateTaxpayer(false);
    onClose();
  }

  function handleNewTaxpayerCreated(taxpayer: TaxpayerDto) {
    form.setFieldsValue({ taxpayerId: taxpayer.id });
    setShowCreateTaxpayer(false);
  }

  return (
    <Modal title="Add Party to Property" open={open} onCancel={handleClose} footer={null} destroyOnHidden>
      {addOwner.isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title="Could not add party"
          description={addOwner.error instanceof ApiRequestError ? addOwner.error.apiError.message : (addOwner.error as Error).message}
        />
      )}

      {showCreateTaxpayer ? (
        <>
          <Typography.Paragraph>
            <Button type="link" style={{ padding: 0 }} onClick={() => setShowCreateTaxpayer(false)}>
              ← Back to search
            </Button>
          </Typography.Paragraph>
          <TaxpayerForm onSuccess={handleNewTaxpayerCreated} />
        </>
      ) : (
        <Form
          form={form}
          layout="vertical"
          onFinish={(values) => {
            const request: AddOwnerRequest = {
              role: values.role,
              taxpayerId: values.role === 'UnknownOwner' ? undefined : values.taxpayerId,
              ownershipTypeId: values.role === 'Owner' ? values.ownershipTypeId : undefined,
              ownershipPercentage: values.role === 'UnknownOwner' ? 0 : values.ownershipPercentage ?? 0,
              startDate: values.startDate.format('YYYY-MM-DD'),
              rpuId: values.rpuId,
            };
            addOwner.mutate(request, { onSuccess: handleClose });
          }}
        >
          <Form.Item name="role" label="Capacity (LGC §§204–205)" initialValue="Owner" rules={[{ required: true }]}
            extra={role === 'UnknownOwner' ? 'Declared by the assessor against an unknown owner (§204). Ended automatically when an owner is added.' : undefined}>
            <Select options={(Object.keys(partyRoleLabel) as PropertyPartyRole[]).map((r) => ({ value: r, label: partyRoleLabel[r] }))} />
          </Form.Item>

          {units.length > 0 && (
            <Form.Item name="rpuId" label="Holds"
              extra="A building or machinery owned apart from the land takes its own parties; shares then total 100% within that unit.">
              <Select allowClear placeholder="Whole property" options={units.map((u) => ({ value: u.id, label: `RPU ${u.rpuNumber} (${u.rpuType}) only` }))} />
            </Form.Item>
          )}

          {role !== 'UnknownOwner' && (<>
          <Form.Item name="taxpayerId" label="Taxpayer" rules={[{ required: true, message: 'Select a taxpayer' }]}>
            <Select
              showSearch
              placeholder="Search taxpayer by name or TIN (min. 2 characters)"
              filterOption={false}
              loading={searchingTaxpayers}
              onSearch={setTaxpayerSearchTerm}
              notFoundContent={taxpayerSearchTerm.length >= 2 ? 'No taxpayers found' : 'Type to search'}
              options={taxpayerResults?.items.map((t) => ({ value: t.id, label: `${t.displayName}${t.tin ? ` (TIN ${t.tin})` : ''}` }))}
            />
          </Form.Item>
          <Space style={{ marginBottom: 16 }}>
            <Typography.Text type="secondary">Can't find the taxpayer?</Typography.Text>
            <Button type="link" style={{ padding: 0 }} onClick={() => setShowCreateTaxpayer(true)}>
              Register a new taxpayer
            </Button>
          </Space>

          </>)}

          {role === 'Owner' && (<>
          <Form.Item name="ownershipTypeId" label="Ownership Type" rules={[{ required: true, message: 'Select an ownership type' }]}>
            <Select options={ownershipTypes?.map((o) => ({ value: o.id, label: o.name }))} />
          </Form.Item>

          <Form.Item
            name="ownershipPercentage"
            label="Ownership Percentage"
            rules={[{ required: true, message: 'Enter the ownership percentage' }]}
          >
            <InputNumber<number>
              min={0.01}
              max={100}
              step={1}
              style={{ width: '100%' }}
              formatter={(value) => (value === undefined ? '' : `${value}%`)}
              parser={(value) => (value ? Number(value.replace('%', '')) : 0)}
            />
          </Form.Item>
          </>)}

          <Form.Item name="startDate" label="Effective Start Date" rules={[{ required: true, message: 'Select a start date' }]} initialValue={dayjs()}>
            <DatePicker style={{ width: '100%' }} />
          </Form.Item>

          <Form.Item>
            <Button type="primary" htmlType="submit" loading={addOwner.isPending}>
              Add
            </Button>
          </Form.Item>
        </Form>
      )}
    </Modal>
  );
}
