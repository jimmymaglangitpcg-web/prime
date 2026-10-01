import { Alert, Button, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Switch, Typography } from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useUpdateMachineryValuationInputs } from '../../../api/machinery';
import { ApiRequestError } from '../../../lib/apiClient';
import { machineryCostItemKinds, type MachineryCostItemKind, type MachineryDto } from '../../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);

interface ItemForm { kind?: MachineryCostItemKind; amount?: number; description?: string }

/**
 * What a machine's derived replacement cost reads (docs/analysis/valuation-foundation.md §4.6): imported or local,
 * its currency, the price index series, the date installed, whether it is in operation, and its acquisition cost
 * items. Changed with a reason (audited); valuations already made keep their figures.
 */
export function MachineryInputsModal({ machine, rpuId, onClose }: { machine: MachineryDto; rpuId: string; onClose: () => void }) {
  const update = useUpdateMachineryValuationInputs(machine.id, rpuId, machine.propertyId);
  const [form] = Form.useForm();
  const imported: boolean = Form.useWatch('isImported', form) ?? machine.isImported;
  return (
    <Modal open title="Valuation inputs" footer={null} onCancel={onClose} width={720} destroyOnHidden>
      {update.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not saved" description={errorText(update.error)} />}
      <Typography.Paragraph type="secondary">
        With a price index series, the replacement cost is derived: cost, insurance and freight × the exchange rates (imported machinery) × the
        index of the valuation year over that of the acquisition year, plus the other expenses; then depreciated at most 5% a year. Without one,
        the entered replacement cost is used.
      </Typography.Paragraph>
      <Form form={form} layout="vertical"
        initialValues={{
          isImported: machine.isImported, acquisitionCurrency: machine.acquisitionCurrency ?? undefined,
          foreignAcquisitionCost: machine.foreignAcquisitionCost ?? undefined, originCountry: machine.originCountry ?? undefined,
          priceIndexSeries: machine.priceIndexSeries ?? undefined, dateInstalled: machine.dateInstalled ? dayjs(machine.dateInstalled) : undefined,
          isInOperation: machine.isInOperation, costItems: machine.costItems.map((i) => ({ kind: i.kind, amount: i.amount, description: i.description ?? undefined })),
        }}
        onFinish={(v) => update.mutate({
          isImported: !!v.isImported, acquisitionCurrency: v.isImported ? (v.acquisitionCurrency ?? '').trim().toUpperCase() || null : null,
          foreignAcquisitionCost: v.isImported ? v.foreignAcquisitionCost ?? null : null, originCountry: v.originCountry?.trim() || null,
          priceIndexSeries: v.priceIndexSeries?.trim() || null, dateInstalled: (v.dateInstalled as Dayjs | undefined)?.format('YYYY-MM-DD') ?? null,
          isInOperation: !!v.isInOperation,
          costItems: (v.costItems ?? []).map((i: ItemForm) => ({ kind: i.kind!, amount: i.amount ?? 0, description: i.description?.trim() || null })),
          reason: v.reason.trim(),
        }, { onSuccess: onClose })}>
        <Space wrap size="large">
          <Form.Item name="isImported" label="Imported" valuePropName="checked"><Switch /></Form.Item>
          <Form.Item name="isInOperation" label="In operation" valuePropName="checked"
            extra="The 20% minimum remaining value holds only while in operation (LGC §225)."><Switch /></Form.Item>
        </Space>
        {imported && (
          <Space wrap>
            <Form.Item name="acquisitionCurrency" label="Currency" rules={[{ required: true, len: 3, message: 'ISO code, e.g. USD' }]}>
              <Input style={{ width: 100 }} placeholder="USD" />
            </Form.Item>
            <Form.Item name="foreignAcquisitionCost" label="Cost in that currency"><InputNumber<number> min={0} style={{ width: 180 }} /></Form.Item>
            <Form.Item name="originCountry" label="Origin country"><Input maxLength={100} style={{ width: 200 }} /></Form.Item>
          </Space>
        )}
        <Space wrap>
          <Form.Item name="priceIndexSeries" label="Price index series" extra="Blank: the entered replacement cost is used.">
            <Input maxLength={30} style={{ width: 220 }} />
          </Form.Item>
          <Form.Item name="dateInstalled" label="Date installed" extra="Years of use count from here, else from acquisition."><DatePicker /></Form.Item>
        </Space>
        <Typography.Paragraph strong style={{ marginBottom: 8 }}>Acquisition cost items (pesos)</Typography.Paragraph>
        <Form.List name="costItems">
          {(fields, { add, remove }) => (
            <>
              {fields.map((field) => (
                <Space key={field.key} align="baseline" wrap style={{ display: 'flex' }}>
                  <Form.Item name={[field.name, 'kind']} rules={[{ required: true, message: 'Kind' }]}>
                    <Select aria-label="Cost item" placeholder="Item" style={{ width: 170 }} options={machineryCostItemKinds} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'amount']} rules={[{ required: true, message: 'Amount' }]}>
                    <InputNumber<number> aria-label="Amount" placeholder="amount" min={0} style={{ width: 150 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'description']}><Input aria-label="Item description" placeholder="description" maxLength={200} /></Form.Item>
                  <MinusCircleOutlined aria-label="Remove item" onClick={() => remove(field.name)} />
                </Space>
              ))}
              <Button type="dashed" icon={<PlusOutlined />} onClick={() => add()} style={{ margin: '4px 0 12px' }}>Add item</Button>
            </>
          )}
        </Form.List>
        <Form.Item name="reason" label="Reason for the change" rules={[{ required: true, whitespace: true, message: 'Give the reason' }]}>
          <Input maxLength={500} />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={update.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}
