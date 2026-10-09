import { Alert, Button, Form, Input, Modal } from 'antd';
import { useUpdateTaxpayerDetails } from '../../api/taxpayers';
import { SexItem } from '../../components/SexItem';
import { ApiRequestError } from '../../lib/apiClient';
import type { TaxpayerDto, UpdateTaxpayerDetailsRequest } from '../../lib/types';

/** Corrects a taxpayer's TIN, address, contact, email and (individuals) sex, with a reason (records-and-forms.md §4.1). */
export function TaxpayerDetailsModal({ taxpayer, onClose }: { taxpayer: TaxpayerDto; onClose: () => void }) {
  const save = useUpdateTaxpayerDetails(taxpayer.id, taxpayer.rowVersion);
  const clean = (v: string | null | undefined) => (v && v.trim() ? v.trim() : null);
  return (
    <Modal open title={`Edit details — ${taxpayer.displayName}`} footer={null} onCancel={onClose} width={560} destroyOnHidden>
      {save.isError && (
        <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not save"
          description={save.error instanceof ApiRequestError ? save.error.apiError.message : (save.error as Error).message} />
      )}
      <Form<UpdateTaxpayerDetailsRequest> layout="vertical"
        initialValues={{
          tin: taxpayer.tin ?? undefined, address: taxpayer.address ?? undefined, contactNumber: taxpayer.contactNumber ?? undefined,
          email: taxpayer.email ?? undefined, sex: taxpayer.sex ?? null,
        }}
        onFinish={(v) => save.mutate({
          tin: clean(v.tin), address: clean(v.address), contactNumber: clean(v.contactNumber), email: clean(v.email),
          sex: taxpayer.taxpayerType === 'Individual' ? v.sex ?? null : null, reason: v.reason,
        }, { onSuccess: onClose })}>
        <Form.Item name="tin" label="TIN"><Input maxLength={20} /></Form.Item>
        <Form.Item name="address" label="Address"><Input maxLength={500} /></Form.Item>
        <Form.Item name="contactNumber" label="Contact Number"><Input maxLength={30} /></Form.Item>
        <Form.Item name="email" label="Email" rules={[{ type: 'email', message: 'Enter a valid email' }]}><Input maxLength={320} /></Form.Item>
        {taxpayer.taxpayerType === 'Individual' && <SexItem />}
        <Form.Item name="reason" label="Reason for the correction" rules={[{ required: true, message: 'A reason is required' }, { max: 1000 }]}
          extra="Kept in the audit log with the old and new values. Issued forms keep what they printed.">
          <Input.TextArea rows={2} />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={save.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}
