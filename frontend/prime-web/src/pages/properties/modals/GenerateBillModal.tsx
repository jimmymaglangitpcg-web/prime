import { Alert, Button, DatePicker, Form, InputNumber, Modal, Select } from 'antd';
import dayjs from 'dayjs';
import { useGenerateBill } from '../../../api/bills';
import { ApiRequestError } from '../../../lib/apiClient';
import type { RpuSummaryDto } from '../../../lib/types';

interface Values {
  rpuId: string;
  taxYear: number;
  asOfDate: dayjs.Dayjs;
}

/**
 * Generates a Draft bill (docs/BILLING.md §6.1). Rules and the posted
 * assessment are taken as in force on 1 January of the tax year; the as-of
 * date decides discounts, penalty and interest.
 */
export function GenerateBillModal({ propertyId, rpus, open, onClose }: {
  propertyId: string;
  rpus: RpuSummaryDto[];
  open: boolean;
  onClose: () => void;
}) {
  const [form] = Form.useForm<Values>();
  const generate = useGenerateBill(propertyId);

  function handleClose() {
    form.resetFields();
    generate.reset();
    onClose();
  }

  return (
    <Modal title="Generate Tax Bill" open={open} onCancel={handleClose} footer={null} destroyOnHidden>
      {generate.isError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title="Could not generate bill"
          description={generate.error instanceof ApiRequestError ? generate.error.apiError.message : (generate.error as Error).message}
        />
      )}

      <Form<Values>
        form={form}
        layout="vertical"
        initialValues={{ rpuId: rpus.length === 1 ? rpus[0].id : undefined, taxYear: dayjs().year(), asOfDate: dayjs() }}
        onFinish={(values) =>
          generate.mutate(
            { rpuId: values.rpuId, taxYear: values.taxYear, asOfDate: values.asOfDate.format('YYYY-MM-DD') },
            { onSuccess: handleClose },
          )
        }
      >
        <Form.Item name="rpuId" label="RPU" rules={[{ required: true, message: 'Select an RPU' }]}>
          <Select options={rpus.map((r) => ({ value: r.id, label: `${r.rpuNumber} (${r.rpuType})` }))} />
        </Form.Item>

        <Form.Item name="taxYear" label="Tax Year" rules={[{ required: true }]}>
          <InputNumber min={1900} max={9999} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item
          name="asOfDate"
          label="Amount due as of"
          extra="The bill assumes full payment on this date: it decides discounts, penalty and interest."
          rules={[{ required: true }]}
        >
          <DatePicker style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={generate.isPending}>
            Generate Draft Bill
          </Button>
        </Form.Item>
      </Form>
    </Modal>
  );
}
