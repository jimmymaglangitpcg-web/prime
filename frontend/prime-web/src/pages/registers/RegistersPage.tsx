import { useState } from 'react';
import { Alert, Button, Card, DatePicker, Form, Input, Select, Space, Table, Tag, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useCreateRegisterRun, useRegisterRuns } from '../../api/registers';
import { useClassifications } from '../../api/referenceData';
import { useTaxpayerSearch } from '../../api/taxpayers';
import { LocationSelect } from '../../components/LocationSelect';
import { PrintFormButton } from '../../components/PrintFormButton';
import { ApiRequestError } from '../../lib/apiClient';
import { registerKindLabel, type RegisterKind, type RegisterRunDto } from '../../lib/types';

interface RunForm {
  kind: RegisterKind;
  provinceId?: string;
  municipalityId?: string;
  barangayId?: string;
  classificationId?: string;
  taxpayerId?: string;
  period?: [Dayjs, Dayjs];
  fromDate?: Dayjs;
  asOf?: Dayjs;
  remarks?: string;
}

/**
 * The MRPAAO registers (Att. 5–9): a run fixes a register's kind, scope and
 * date; its rows come from the FAAS in force, and printing the run issues it
 * as a frozen form.
 */
export function RegistersPage() {
  const [form] = Form.useForm<RunForm>();
  const kind = Form.useWatch('kind', form) ?? 'TaxMapControlRoll';
  const [provinceId, setProvinceId] = useState<string>();
  const [municipalityId, setMunicipalityId] = useState<string>();
  const [taxpayerTerm, setTaxpayerTerm] = useState('');
  const { data: runs = [], isLoading } = useRegisterRuns();
  const { data: classifications = [] } = useClassifications();
  const { data: taxpayers, isFetching: searching } = useTaxpayerSearch({ searchTerm: taxpayerTerm, pageSize: 10 }, { enabled: taxpayerTerm.length >= 2 });
  const create = useCreateRegisterRun();

  const byOwner = kind === 'OwnershipRecordCard';
  const periodic = kind === 'RecordOfAssessment';
  const supplement = kind === 'AssessmentRollTaxable' || kind === 'AssessmentRollExempt';

  function submit(values: RunForm) {
    const asOf = periodic ? values.period?.[1] : values.asOf;
    const from = periodic ? values.period?.[0] : supplement ? values.fromDate : null;
    create.mutate(
      {
        kind: values.kind, asOf: asOf!.format('YYYY-MM-DD'), fromDate: from ? from.format('YYYY-MM-DD') : null,
        barangayId: byOwner ? null : values.barangayId ?? null, classificationId: periodic ? values.classificationId ?? null : null,
        taxpayerId: byOwner ? values.taxpayerId ?? null : null, remarks: values.remarks || null,
      },
      { onSuccess: () => form.resetFields(['remarks']) },
    );
  }

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Title level={3} style={{ margin: 0 }}>Assessment Registers</Typography.Title>
      <Alert type="info" showIcon title="MRPAAO registers (reference layouts)"
        description="Each run lists the FAAS in force on its date. Printing a run issues it: the rows are frozen, and a later run is a new run." />
      <Card title="New register run">
        {create.isError && (
          <Alert type="error" showIcon style={{ marginBottom: 16 }} title="No run created"
            description={create.error instanceof ApiRequestError ? create.error.apiError.message : (create.error as Error).message} />
        )}
        <Form form={form} layout="vertical" initialValues={{ kind: 'TaxMapControlRoll', asOf: dayjs() }} onFinish={submit}>
          <Form.Item name="kind" label="Register" rules={[{ required: true }]} style={{ maxWidth: 420 }}>
            <Select options={(Object.keys(registerKindLabel) as RegisterKind[]).map((k) => ({ value: k, label: registerKindLabel[k] }))} />
          </Form.Item>
          {byOwner ? (
            <Form.Item name="taxpayerId" label="Owner" rules={[{ required: true, message: 'Choose the owner' }]} style={{ maxWidth: 420 }}>
              <Select showSearch filterOption={false} onSearch={setTaxpayerTerm} loading={searching} placeholder="Type at least 2 characters"
                options={(taxpayers?.items ?? []).map((t) => ({ value: t.id, label: t.displayName }))} />
            </Form.Item>
          ) : (
            <LocationSelect provinceFieldName="provinceId" municipalityFieldName="municipalityId" barangayFieldName="barangayId"
              provinceId={provinceId} municipalityId={municipalityId}
              onProvinceChange={(id) => { setProvinceId(id); setMunicipalityId(undefined); form.setFieldsValue({ municipalityId: undefined, barangayId: undefined }); }}
              onMunicipalityChange={(id) => { setMunicipalityId(id); form.setFieldsValue({ barangayId: undefined }); }} />
          )}
          {periodic && (
            <Form.Item name="classificationId" label="Classification" rules={[{ required: true, message: 'Choose the classification' }]} style={{ maxWidth: 420 }}>
              <Select options={classifications.map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` }))} />
            </Form.Item>
          )}
          {periodic ? (
            <Form.Item name="period" label="Period" rules={[{ required: true, message: 'Choose the period' }]}>
              <DatePicker.RangePicker />
            </Form.Item>
          ) : (
            <Space wrap align="start">
              <Form.Item name="asOf" label="As of" rules={[{ required: true }]}><DatePicker /></Form.Item>
              {supplement && (
                <Form.Item name="fromDate" label="Supplement: FAAS entered from (optional)"><DatePicker /></Form.Item>
              )}
            </Space>
          )}
          <Form.Item name="remarks" label="Remarks" style={{ maxWidth: 620 }}><Input maxLength={1000} /></Form.Item>
          <Button type="primary" htmlType="submit" loading={create.isPending}>Create run</Button>
        </Form>
      </Card>
      <Card title="Runs">
        <Table<RegisterRunDto> rowKey="id" size="small" loading={isLoading} dataSource={runs} scroll={{ x: true }}
          columns={[
            { title: 'Register', dataIndex: 'kind', render: (k: RegisterKind) => registerKindLabel[k] },
            { title: 'Scope', render: (_, r) => r.taxpayerName ?? [r.barangayName, r.classificationName].filter(Boolean).join(' · ') },
            { title: 'Period', render: (_, r) => (r.fromDate ? <>{r.fromDate} – {r.asOf} {r.kind !== 'RecordOfAssessment' && <Tag>supplement</Tag>}</> : <>as of {r.asOf}</>) },
            { title: 'Remarks', dataIndex: 'remarks' },
            { title: 'Created', dataIndex: 'createdAt', render: (d: string) => dayjs(d).format('YYYY-MM-DD HH:mm') },
            {
              title: '', render: (_, r) => (
                <Space>
                  <PrintFormButton formCode={r.formCode} subjectId={r.id} issuable={false} />
                  <PrintFormButton formCode={r.formCode} subjectId={r.id} issuable label="Issue" />
                </Space>
              ),
            },
          ]} />
      </Card>
    </Space>
  );
}
