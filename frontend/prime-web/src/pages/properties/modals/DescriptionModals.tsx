import { Alert, Button, DatePicker, Form, Input, InputNumber, Modal, Select, Space } from 'antd';
import dayjs from 'dayjs';
import {
  useSetTaxClearance, useUpdateBuildingDescription, useUpdateMachineryDescription, useUpdatePropertyDescription,
} from '../../../api/descriptions';
import { useTitleTypes } from '../../../api/referenceData';
import { ApiRequestError } from '../../../lib/apiClient';
import type { BuildingDto, MachineryDto, PropertyDto, TransferTaxClearanceDto } from '../../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const toDay = (v: string | null | undefined) => (v ? dayjs(v) : undefined);
const fromDay = (v: dayjs.Dayjs | null | undefined) => (v ? v.format('YYYY-MM-DD') : null);

const ReasonItem = () => (
  <Form.Item name="reason" label="Reason for the correction" rules={[{ required: true, message: 'A reason is required' }, { max: 1000 }]}
    extra="Kept in the audit log with the old and new values. Issued forms keep what they printed.">
    <Input.TextArea rows={2} />
  </Form.Item>
);

/** Property descriptive fields (MRPAAO Att. 1, 4): location details, title, boundaries. */
export function PropertyDescriptionModal({ property, onClose }: { property: PropertyDto; onClose: () => void }) {
  const save = useUpdatePropertyDescription(property.id);
  const { data: titleTypes = [] } = useTitleTypes();
  return (
    <Modal open title="Edit property description" footer={null} onCancel={onClose} width={720} destroyOnHidden>
      {save.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not save" description={errorText(save.error)} />}
      <Form layout="vertical"
        initialValues={{ ...property, titleDate: toDay(property.titleDate) }}
        onFinish={(v) => save.mutate({
          street: v.street ?? null, sitio: v.sitio ?? null, lotNumber: v.lotNumber ?? null, blockNumber: v.blockNumber ?? null,
          surveyNumber: v.surveyNumber ?? null, titleNumber: v.titleNumber ?? null, titleTypeId: v.titleTypeId ?? null, titleDate: fromDay(v.titleDate),
          taxMapNumber: v.taxMapNumber ?? null, boundaryNorth: v.boundaryNorth ?? null, boundaryEast: v.boundaryEast ?? null,
          boundarySouth: v.boundarySouth ?? null, boundaryWest: v.boundaryWest ?? null, reason: v.reason,
        }, { onSuccess: onClose })}>
        <Space wrap>
          <Form.Item name="street" label="Street"><Input maxLength={100} /></Form.Item>
          <Form.Item name="sitio" label="Sitio"><Input maxLength={100} /></Form.Item>
          <Form.Item name="lotNumber" label="Lot No."><Input maxLength={100} /></Form.Item>
          <Form.Item name="blockNumber" label="Block No."><Input maxLength={100} /></Form.Item>
          <Form.Item name="surveyNumber" label="Survey No."><Input maxLength={100} /></Form.Item>
          <Form.Item name="taxMapNumber" label="Tax Map No."><Input maxLength={100} /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="titleTypeId" label="Title kind">
            <Select allowClear style={{ width: 260 }} options={titleTypes.map((t) => ({ value: t.id, label: `${t.code} — ${t.name}` }))} />
          </Form.Item>
          <Form.Item name="titleNumber" label="Title No."><Input maxLength={100} /></Form.Item>
          <Form.Item name="titleDate" label="Title dated"><DatePicker /></Form.Item>
        </Space>
        {(['North', 'East', 'South', 'West'] as const).map((side) => (
          <Form.Item key={side} name={`boundary${side}`} label={`Boundary — ${side}`}
            extra={side === 'North' ? 'As the FAAS states it: adjoining lot numbers, owners, streets or rivers.' : undefined}>
            <Input maxLength={500} />
          </Form.Item>
        ))}
        <ReasonItem />
        <Button type="primary" htmlType="submit" loading={save.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}

/** Building general description (MRPAAO Att. 2). */
export function BuildingDescriptionModal({ building, rpuId, onClose }: { building: BuildingDto; rpuId: string; onClose: () => void }) {
  const save = useUpdateBuildingDescription(building.id, rpuId);
  return (
    <Modal open title="Edit building description" footer={null} onCancel={onClose} width={720} destroyOnHidden>
      {save.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not save" description={errorText(save.error)} />}
      <Form layout="vertical"
        initialValues={{
          ...building, buildingPermitDate: toDay(building.buildingPermitDate), certificateOfCompletionDate: toDay(building.certificateOfCompletionDate),
          certificateOfOccupancyDate: toDay(building.certificateOfOccupancyDate), dateConstructed: toDay(building.dateConstructed),
          dateOccupied: toDay(building.dateOccupied),
        }}
        onFinish={(v) => save.mutate({
          numberOfStoreys: v.numberOfStoreys ?? null, yearConstructed: v.yearConstructed ?? null, yearCompleted: v.yearCompleted ?? null,
          buildingPermitNumber: v.buildingPermitNumber ?? null, buildingPermitDate: fromDay(v.buildingPermitDate),
          condominiumCertificateNumber: v.condominiumCertificateNumber ?? null, certificateOfCompletionDate: fromDay(v.certificateOfCompletionDate),
          certificateOfOccupancyDate: fromDay(v.certificateOfOccupancyDate), dateConstructed: fromDay(v.dateConstructed),
          dateOccupied: fromDay(v.dateOccupied), reason: v.reason,
        }, { onSuccess: onClose })}>
        <Space wrap>
          <Form.Item name="numberOfStoreys" label="Storeys"><InputNumber<number> min={1} /></Form.Item>
          <Form.Item name="yearConstructed" label="Year constructed"><InputNumber<number> min={1800} max={2200} /></Form.Item>
          <Form.Item name="yearCompleted" label="Year completed"><InputNumber<number> min={1800} max={2200} /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="buildingPermitNumber" label="Building permit No."><Input maxLength={100} /></Form.Item>
          <Form.Item name="buildingPermitDate" label="Permit issued"><DatePicker /></Form.Item>
          <Form.Item name="condominiumCertificateNumber" label="CCT No."><Input maxLength={100} /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="certificateOfCompletionDate" label="Certificate of completion"><DatePicker /></Form.Item>
          <Form.Item name="certificateOfOccupancyDate" label="Certificate of occupancy"><DatePicker /></Form.Item>
          <Form.Item name="dateConstructed" label="Date constructed / completed"><DatePicker /></Form.Item>
          <Form.Item name="dateOccupied" label="Date occupied"><DatePicker /></Form.Item>
        </Space>
        <ReasonItem />
        <Button type="primary" htmlType="submit" loading={save.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}

/** A machine's descriptive fields (MRPAAO Att. 3). Costs and lives are valued inputs and are not edited here. */
export function MachineryDescriptionModal({ machine, rpuId, onClose }: { machine: MachineryDto; rpuId: string; onClose: () => void }) {
  const save = useUpdateMachineryDescription(rpuId);
  return (
    <Modal open title="Edit machine description" footer={null} onCancel={onClose} width={640} destroyOnHidden>
      {save.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not save" description={errorText(save.error)} />}
      <Form layout="vertical" initialValues={machine}
        onFinish={(v) => save.mutate({
          id: machine.id, description: v.description ?? null, brand: v.brand ?? null, model: v.model ?? null, serialNumber: v.serialNumber ?? null,
          capacity: v.capacity ?? null, capacityUnit: v.capacityUnit ?? null, yearInstalled: v.yearInstalled ?? null,
          yearOfInitialOperation: v.yearOfInitialOperation ?? null, conversionFactor: v.conversionFactor ?? null, reason: v.reason,
        }, { onSuccess: onClose })}>
        <Space wrap>
          <Form.Item name="brand" label="Brand"><Input maxLength={200} /></Form.Item>
          <Form.Item name="model" label="Model"><Input maxLength={200} /></Form.Item>
          <Form.Item name="serialNumber" label="Serial No."><Input maxLength={200} /></Form.Item>
        </Space>
        <Form.Item name="description" label="Description"><Input maxLength={500} /></Form.Item>
        <Space wrap>
          <Form.Item name="capacity" label="Capacity"><InputNumber<number> min={0} /></Form.Item>
          <Form.Item name="capacityUnit" label="Unit"><Input maxLength={200} style={{ width: 100 }} /></Form.Item>
          <Form.Item name="yearInstalled" label="Year installed"><InputNumber<number> min={1800} max={2200} /></Form.Item>
          <Form.Item name="yearOfInitialOperation" label="Year of initial operation"><InputNumber<number> min={1800} max={2200} /></Form.Item>
          <Form.Item name="conversionFactor" label="Conversion factor" extra="Printed on the FAAS; the replacement cost stays as entered.">
            <InputNumber<number> min={0.000001} />
          </Form.Item>
        </Space>
        <ReasonItem />
        <Button type="primary" htmlType="submit" loading={save.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}

/** The BIR clearance and taxes paid on a transfer (MRPAAO Annex A), printed on the back of its TD. */
export function TaxClearanceModal({ propertyId, transactionId, value, onClose }: {
  propertyId: string; transactionId: string; value: TransferTaxClearanceDto | null; onClose: () => void;
}) {
  const save = useSetTaxClearance(propertyId, transactionId);
  const tax = (key: 'capitalGainsTax' | 'documentaryStampTax' | 'transferTax', label: string) => (
    <Space wrap>
      <Form.Item name={key} label={label}><InputNumber<number> min={0} style={{ width: 160 }} /></Form.Item>
      <Form.Item name={`${key}Receipt`} label="OR No."><Input maxLength={100} /></Form.Item>
      <Form.Item name={`${key}Date`} label="Paid on"><DatePicker /></Form.Item>
    </Space>
  );
  const v0 = value ?? ({} as Partial<TransferTaxClearanceDto>);
  return (
    <Modal open title="BIR tax clearance (CAR)" footer={null} onCancel={onClose} width={720} destroyOnHidden>
      {save.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not save" description={errorText(save.error)} />}
      <Form layout="vertical"
        initialValues={{
          ...v0, carDate: toDay(v0.carDate), capitalGainsTaxDate: toDay(v0.capitalGainsTaxDate),
          documentaryStampTaxDate: toDay(v0.documentaryStampTaxDate), transferTaxDate: toDay(v0.transferTaxDate),
        }}
        onFinish={(v) => save.mutate({
          carNumber: v.carNumber ?? null, carDate: fromDay(v.carDate), transferorName: v.transferorName ?? null, transferorTin: v.transferorTin ?? null,
          transfereeTin: v.transfereeTin ?? null, capitalGainsTax: v.capitalGainsTax ?? null, capitalGainsTaxReceipt: v.capitalGainsTaxReceipt ?? null,
          capitalGainsTaxDate: fromDay(v.capitalGainsTaxDate), documentaryStampTax: v.documentaryStampTax ?? null,
          documentaryStampTaxReceipt: v.documentaryStampTaxReceipt ?? null, documentaryStampTaxDate: fromDay(v.documentaryStampTaxDate),
          transferTax: v.transferTax ?? null, transferTaxReceipt: v.transferTaxReceipt ?? null, transferTaxDate: fromDay(v.transferTaxDate),
          remarks: v.remarks ?? null,
        }, { onSuccess: onClose })}>
        <Space wrap>
          <Form.Item name="carNumber" label="CAR No."><Input maxLength={100} /></Form.Item>
          <Form.Item name="carDate" label="CAR date"><DatePicker /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="transferorName" label="Transferor"><Input maxLength={300} /></Form.Item>
          <Form.Item name="transferorTin" label="Transferor TIN"><Input maxLength={20} /></Form.Item>
          <Form.Item name="transfereeTin" label="Transferee TIN"><Input maxLength={20} /></Form.Item>
        </Space>
        {tax('capitalGainsTax', 'Capital gains tax')}
        {tax('documentaryStampTax', 'Documentary stamp tax')}
        {tax('transferTax', 'Transfer tax')}
        <Form.Item name="remarks" label="Remarks"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={save.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}
