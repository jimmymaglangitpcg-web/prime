import { useState } from 'react';
import { Alert, Button, Card, Col, DatePicker, Form, Input, InputNumber, Modal, Row, Select, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import {
  useApproveReportRowMap, useApproveSystemParameter, useCreateReportRowMap, useCreateSystemParameter, useReportRowMaps, useSystemParameterCatalog,
  useSystemParameters, type ReportRowMapDto, type ReportRowSpec, type SystemParameterDto,
} from '../../api/reportConfiguration';
import { formatMoney } from '../../lib/format';
import { ApproveButton } from './ApproveButton';
import { day, errorText, period, statusTag, useToast } from './ruleHelpers';

const sectionLabel: Record<ReportRowSpec['section'], string> = {
  Taxable: 'Taxable', Exempt: 'Exempt', Restricted: 'With restrictions', IdleLand: 'Idle lands',
};

const codes = (list?: string[] | null) => (list?.length ? list.join(', ') : '');

/** The rows of one map version, as configured. */
function RowsTable({ map }: { map: ReportRowMapDto }) {
  const groups = new Map((map.definition.restrictions ?? []).map((g) => [g.code, g]));
  return (
    <Table<ReportRowSpec> rowKey="code" size="small" pagination={false} dataSource={map.definition.rows} scroll={{ x: true }}
      columns={[
        { title: 'Section', dataIndex: 'section', render: (s: ReportRowSpec['section'], r) => r.restriction ? `${sectionLabel[s]}: ${groups.get(r.restriction)?.label ?? r.restriction}` : sectionLabel[s] },
        { title: 'Code', dataIndex: 'code' },
        { title: 'Row', dataIndex: 'label' },
        {
          title: 'Takes', render: (_, r) => r.others ? <Tag>everything else in its section</Tag>
            : [codes(r.classifications) && `classes ${codes(r.classifications)}`, codes(r.actualUses) && `uses ${codes(r.actualUses)}`,
              codes(r.exemptionTypes) && `exemptions ${codes(r.exemptionTypes)}`].filter(Boolean).join('; ') || (r.section === 'IdleLand' ? '—' : 'any'),
        },
        { title: 'Building split', dataIndex: 'splitsBuildings', render: (v?: boolean) => (v ? 'Yes' : '') },
      ]}
      footer={() => (map.definition.restrictions?.length
        ? `Restriction groups: ${(map.definition.restrictions ?? []).map((g) => `${g.label} (annotation types ${g.annotationTypes.join(', ')})`).join('; ')}`
        : 'No restriction groups')} />
  );
}

/**
 * The QRRPA's rows (docs/analysis/reporting.md §10, Q15): which classifications, exemption types and restriction
 * annotations each row of the quarterly report gathers. Loaded from the content pack (report-row-maps) or entered here as
 * JSON; a second user approves; a new version takes over from its effective date. The LAM's rows are content: PRIME
 * ships a DEMO map only.
 */
export function ReportRowMapsTab() {
  const { data = [], isLoading } = useReportRowMaps();
  const approve = useApproveReportRowMap();
  const { context, fail } = useToast();
  const [creating, setCreating] = useState(false);
  return (
    <Card title="QRRPA rows" extra={<Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>New version</Button>}>
      {context}
      <Typography.Paragraph type="secondary">
        The rows of the quarterly report and what each gathers: taxable parts by classification, exempt parts by exemption type, units under a
        restriction (a Tax Declaration annotation) by group, and idle lands. Without an approved map in force, the report lists PRIME&apos;s
        classifications and exemption types. The official rows come with the LAM content pack.
      </Typography.Paragraph>
      <Table<ReportRowMapDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
        locale={{ emptyText: 'No row map: the report uses PRIME\'s classifications and exemption types' }}
        expandable={{ expandedRowRender: (map) => <RowsTable map={map} /> }}
        columns={[
          { title: 'Report', dataIndex: 'code' },
          { title: 'Name', dataIndex: 'name' },
          { title: 'Rows', render: (_, r) => r.definition.rows.length, align: 'right' },
          { title: 'Legal basis', dataIndex: 'legalBasis' },
          { title: 'Period', render: (_, r) => period(r.effectiveDate, r.endDate) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          { title: '', render: (_, r) => <ApproveButton status={r.status} pending={approve.isPending} onApprove={() => approve.mutate(r.id, { onError: fail })} /> },
        ]} />
      {creating && <CreateRowMapModal onClose={() => setCreating(false)} />}
    </Card>
  );
}

const example = JSON.stringify({
  restrictions: [{ code: 'RA', label: 'Restriction group', annotationTypes: ['ANNOTATION-CODE'] }],
  rows: [
    { section: 'Taxable', code: 'T1', label: 'Row label', classifications: ['CLASS-CODE'], splitsBuildings: true },
    { section: 'Taxable', code: 'T9', label: 'Others', others: true },
    { section: 'Exempt', code: 'E9', label: 'Others', others: true },
    { section: 'Restricted', restriction: 'RA', code: 'R9', label: 'All classes', others: true },
  ],
}, null, 2);

function CreateRowMapModal({ onClose }: { onClose: () => void }) {
  const create = useCreateReportRowMap();
  const [form] = Form.useForm();
  const [jsonError, setJsonError] = useState<string>();
  function submit(v: { name: string; definition: string; legalBasis: string; effectiveDate: Parameters<typeof day>[0]; remarks?: string }) {
    let definition: unknown;
    try {
      definition = JSON.parse(v.definition);
    } catch (e) {
      setJsonError(`The definition is not valid JSON: ${(e as Error).message}`);
      return;
    }
    setJsonError(undefined);
    create.mutate({ code: 'QRRPA', name: v.name, definition, legalBasis: v.legalBasis, effectiveDate: day(v.effectiveDate)!, remarks: v.remarks || null },
      { onSuccess: onClose });
  }
  return (
    <Modal open title="New QRRPA row map (Draft)" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()}
      width={820} destroyOnHidden>
      {(jsonError || create.isError) && (
        <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={jsonError ?? errorText(create.error)} />
      )}
      <Form form={form} layout="vertical" onFinish={submit} initialValues={{ definition: example }}>
        <Row gutter={12}>
          <Col span={12}><Form.Item name="name" label="Name" rules={[{ required: true, max: 200 }]}><Input /></Form.Item></Col>
          <Col span={12}><Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={24}><Form.Item name="legalBasis" label="Legal basis" rules={[{ required: true, max: 500 }]}><Input placeholder="The issuance and annex the rows follow" /></Form.Item></Col>
          <Col span={24}>
            <Form.Item name="definition" label="Rows (JSON)" rules={[{ required: true }]}
              extra="Sections: Taxable, Exempt, Restricted (with a restriction group), IdleLand. Codes are PRIME's classification, actual-use, exemption-type and annotation-type codes.">
              <Input.TextArea rows={14} spellCheck={false} style={{ fontFamily: 'monospace', fontSize: 12 }} />
            </Form.Item>
          </Col>
          <Col span={24}><Form.Item name="remarks" label="Remarks"><Input maxLength={1000} /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  );
}

/**
 * Dated system parameters (docs/analysis/reporting.md §10, Q16): figures an issuance or ordinance sets that PRIME reads,
 * such as the QRRPA's residential building threshold. None has a built-in value.
 */
export function SystemParametersTab() {
  const { data = [], isLoading } = useSystemParameters();
  const { data: catalog = [] } = useSystemParameterCatalog();
  const approve = useApproveSystemParameter();
  const { context, fail } = useToast();
  const [creating, setCreating] = useState(false);
  return (
    <Card title="Parameters" extra={<Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>New value</Button>}>
      {context}
      <Typography.Paragraph type="secondary">
        Figures an issuance or ordinance sets and PRIME reads. Each value has its legal basis and takes effect on its date once a second user approves it.
      </Typography.Paragraph>
      <ul style={{ marginTop: 0 }}>
        {catalog.map((p) => <li key={p.code}><Typography.Text strong>{p.name}</Typography.Text> ({p.unit}): {p.description}</li>)}
      </ul>
      <Table<SystemParameterDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: true }}
        locale={{ emptyText: 'No values set' }}
        columns={[
          { title: 'Parameter', dataIndex: 'name' },
          { title: 'Value', dataIndex: 'value', align: 'right', render: (v: number, r) => (r.unit === 'PHP' ? formatMoney(v) : v) },
          { title: 'Legal basis', dataIndex: 'legalBasis' },
          { title: 'Period', render: (_, r) => period(r.effectiveDate, r.endDate) },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          { title: '', render: (_, r) => <ApproveButton status={r.status} pending={approve.isPending} onApprove={() => approve.mutate(r.id, { onError: fail })} /> },
        ]} />
      {creating && <CreateParameterModal onClose={() => setCreating(false)} />}
    </Card>
  );
}

function CreateParameterModal({ onClose }: { onClose: () => void }) {
  const create = useCreateSystemParameter();
  const { data: catalog = [] } = useSystemParameterCatalog();
  const [form] = Form.useForm();
  return (
    <Modal open title="New parameter value (Draft)" okText="Create" onCancel={onClose} okButtonProps={{ loading: create.isPending }} onOk={() => form.submit()}
      width={640} destroyOnHidden>
      {create.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not created" description={errorText(create.error)} />}
      <Form form={form} layout="vertical" onFinish={(v) => create.mutate({
        code: v.code, value: v.value, legalBasis: v.legalBasis, effectiveDate: day(v.effectiveDate)!, description: v.description || null, remarks: null,
      }, { onSuccess: onClose })}>
        <Form.Item name="code" label="Parameter" rules={[{ required: true }]}>
          <Select options={catalog.map((p) => ({ value: p.code, label: p.name }))} />
        </Form.Item>
        <Row gutter={12}>
          <Col span={12}><Form.Item name="value" label="Value" rules={[{ required: true }]}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={12}><Form.Item name="effectiveDate" label="Effective" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
        </Row>
        <Form.Item name="legalBasis" label="Legal basis" rules={[{ required: true, max: 500 }]}><Input placeholder="The issuance that sets the figure" /></Form.Item>
        <Form.Item name="description" label="Description"><Input maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
  );
}
