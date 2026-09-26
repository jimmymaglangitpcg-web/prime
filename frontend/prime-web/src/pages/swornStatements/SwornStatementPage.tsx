import { useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import {
  Alert, Button, Card, Checkbox, Col, DatePicker, Descriptions, Form, Input, InputNumber, Modal, Popconfirm, Radio, Row, Select, Space, Spin, Table, Tag, Typography,
} from 'antd';
import { ArrowLeftOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import {
  useAddSwornStatementItem, useCancelSwornStatement, useCreateSwornStatement, useFileSwornStatement, useLinkSwornStatementItem,
  useRemoveSwornStatementItem, useSwornStatement, useUpdateSwornStatement,
} from '../../api/swornStatements';
import { useActualUses, useClassifications, useImprovementKinds, useMunicipalities, useProvinces } from '../../api/referenceData';
import { PrintFormButton } from '../../components/PrintFormButton';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import {
  declarantCapacityLabel, filingBasisLabel, swornItemKindLabel, swornStatusColor,
  type AddSwornStatementItemRequest, type DeclarantCapacity, type SaveSwornStatementRequest, type SwornStatementDto,
  type SwornStatementFilingBasis, type SwornStatementItemDto, type SwornStatementItemKind,
} from '../../lib/types';
import { UnitPicker, type PickedUnit } from './UnitPicker';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const day = (d: Dayjs | null | undefined) => (d ? d.format('YYYY-MM-DD') : null);
const text = (v: string | null | undefined) => (v && v.trim() ? v.trim() : null);
const dash = (v: unknown) => (v === null || v === undefined || v === '' ? '—' : String(v));

interface HeaderForm {
  declarantName: string; citizenship?: string; civilStatus?: string; postalAddress?: string; declarantTin?: string;
  capacity: DeclarantCapacity; ownerNames?: string; provinceId?: string; municipalityId: string; filingBasis: SwornStatementFilingBasis;
  signedOn?: Dayjs; signedAt?: string; thumbmarked?: boolean; witness1?: string; witness2?: string;
  swornOn?: Dayjs; swornAt?: string; administeringOfficer?: string; officerTin?: string;
  identityDocument?: string; identityDocumentIssuedOn?: Dayjs; identityDocumentIssuedAt?: string; receivedOn?: Dayjs; remarks?: string;
}

/**
 * One sworn statement (MRPAAO Att. 11; docs/analysis/mrpaao-forms-model.md
 * §16): intake in the order of the paper form, then filing once it is sworn
 * and received. Route /sworn-statements/new (optionally ?supersedes=) or /:id.
 */
export function SwornStatementPage() {
  const { id } = useParams();
  const isNew = !id || id === 'new';
  const [params] = useSearchParams();
  const supersedesId = isNew ? params.get('supersedes') : null;
  const { data: statement, isLoading } = useSwornStatement(isNew ? undefined : id);
  const { data: corrected } = useSwornStatement(supersedesId ?? undefined);
  const navigate = useNavigate();

  if (!isNew && (isLoading || !statement)) {
    return <Spin />;
  }
  if (supersedesId && !corrected) {
    return <Spin />;
  }
  const source = statement ?? corrected;
  const editable = isNew || statement?.status === 'Draft';

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/sworn-statements')}>Sworn statements</Button>
        <Typography.Title level={3} style={{ margin: 0 }}>
          {isNew ? (supersedesId ? 'Correct a sworn statement' : 'New sworn statement') : `Sworn statement ${statement!.number ?? '(unnumbered)'}`}
        </Typography.Title>
        {statement && <Tag color={swornStatusColor[statement.status]}>{statement.status}</Tag>}
      </Space>
      {statement && <StatusNotes statement={statement} />}
      {editable
        ? <HeaderEditor key={statement?.id ?? 'new'} statement={statement} initial={source} supersedesId={statement?.supersedesId ?? supersedesId} />
        : <HeaderView statement={statement!} />}
      {statement && <ItemsCard statement={statement} />}
      {statement && <ActionsCard statement={statement} />}
    </Space>
  );
}

function StatusNotes({ statement: s }: { statement: SwornStatementDto }) {
  const navigate = useNavigate();
  return (
    <>
      {s.supersedesId && (
        <Alert type="info" showIcon title={<>Corrects statement <Button type="link" style={{ padding: 0 }} onClick={() => navigate(`/sworn-statements/${s.supersedesId}`)}>
          {s.supersedesNumber ?? '(unnumbered)'}</Button></>} />
      )}
      {s.supersededById && (
        <Alert type="warning" showIcon title={<>{s.status === 'Superseded' ? 'Superseded by' : 'A correction is being prepared:'}{' '}
          <Button type="link" style={{ padding: 0 }} onClick={() => navigate(`/sworn-statements/${s.supersededById}`)}>the later statement</Button></>} />
      )}
      {s.status === 'Cancelled' && <Alert type="error" showIcon title="Cancelled" description={s.cancellationReason} />}
    </>
  );
}

function HeaderEditor({ statement, initial, supersedesId }: { statement?: SwornStatementDto; initial?: SwornStatementDto; supersedesId: string | null }) {
  const [form] = Form.useForm<HeaderForm>();
  const navigate = useNavigate();
  const create = useCreateSwornStatement();
  const update = useUpdateSwornStatement(statement?.id ?? '');
  const save = statement ? update : create;
  const [provinceId, setProvinceId] = useState<string>();
  const { data: provinces = [] } = useProvinces();
  const { data: municipalities = [] } = useMunicipalities(provinceId);
  const capacity = Form.useWatch('capacity', form) ?? 'Owner';
  const thumbmarked = Form.useWatch('thumbmarked', form) ?? false;
  const d = (v: string | null | undefined) => (v ? dayjs(v) : undefined);
  const lockedMunicipality = !!supersedesId || (statement?.items.some((i) => i.propertyId) ?? false);

  const initialValues: Partial<HeaderForm> = initial ? {
    declarantName: initial.declarantName, citizenship: initial.citizenship ?? undefined, civilStatus: initial.civilStatus ?? undefined,
    postalAddress: initial.postalAddress ?? undefined, declarantTin: initial.declarantTin ?? undefined, capacity: initial.capacity,
    ownerNames: initial.ownerNames ?? undefined, municipalityId: initial.municipalityId, filingBasis: initial.filingBasis,
    ...(statement ? {
      signedOn: d(statement.signedOn), signedAt: statement.signedAt ?? undefined, thumbmarked: statement.thumbmarked,
      witness1: statement.witness1 ?? undefined, witness2: statement.witness2 ?? undefined, swornOn: d(statement.swornOn),
      swornAt: statement.swornAt ?? undefined, administeringOfficer: statement.administeringOfficer ?? undefined,
      officerTin: statement.officerTin ?? undefined, identityDocument: statement.identityDocument ?? undefined,
      identityDocumentIssuedOn: d(statement.identityDocumentIssuedOn), identityDocumentIssuedAt: statement.identityDocumentIssuedAt ?? undefined,
      receivedOn: d(statement.receivedOn), remarks: statement.remarks ?? undefined,
    } : {}),
  } : { capacity: 'Owner', filingBasis: 'Section202', thumbmarked: false };

  function submit(v: HeaderForm) {
    const request: SaveSwornStatementRequest = {
      declarantName: v.declarantName, declarantTaxpayerId: statement?.declarantTaxpayerId ?? null, citizenship: text(v.citizenship),
      civilStatus: text(v.civilStatus), postalAddress: text(v.postalAddress), declarantTin: text(v.declarantTin), capacity: v.capacity,
      ownerNames: text(v.ownerNames), municipalityId: v.municipalityId ?? initial?.municipalityId, filingBasis: v.filingBasis,
      signedOn: day(v.signedOn), signedAt: text(v.signedAt), thumbmarked: !!v.thumbmarked, witness1: text(v.witness1), witness2: text(v.witness2),
      swornOn: day(v.swornOn), swornAt: text(v.swornAt), administeringOfficer: text(v.administeringOfficer), officerTin: text(v.officerTin),
      identityDocument: text(v.identityDocument), identityDocumentIssuedOn: day(v.identityDocumentIssuedOn),
      identityDocumentIssuedAt: text(v.identityDocumentIssuedAt), receivedOn: day(v.receivedOn), supersedesId, remarks: text(v.remarks),
    };
    save.mutate(request, { onSuccess: (saved) => { if (!statement) navigate(`/sworn-statements/${saved.id}`, { replace: true }); } });
  }

  return (
    <Card title="Declarant, execution and jurat">
      {save.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not saved" description={errorText(save.error)} />}
      {save.isSuccess && statement && <Alert type="success" showIcon style={{ marginBottom: 12 }} title="Saved" />}
      <Form form={form} layout="vertical" initialValues={initialValues} onFinish={submit}>
        <Row gutter={16}>
          <Col xs={24} md={12}><Form.Item name="declarantName" label="Declarant (I, …)" rules={[{ required: true, message: 'The declarant is required' }]}><Input maxLength={300} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="citizenship" label="Citizenship"><Input maxLength={100} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="civilStatus" label="Civil status"><Input maxLength={50} /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="postalAddress" label="Postal address"><Input maxLength={1000} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="declarantTin" label="Declarant's TIN"><Input maxLength={50} /></Form.Item></Col>
          <Col xs={12} md={6}>
            <Form.Item name="capacity" label="Capacity" rules={[{ required: true }]}>
              <Select options={Object.entries(declarantCapacityLabel).map(([value, label]) => ({ value, label }))} />
            </Form.Item>
          </Col>
          {capacity !== 'Owner' && (
            <Col xs={24}><Form.Item name="ownerNames" label="Owned by (the owners' names)" rules={[{ required: true, message: 'State the owners (Att. 11, item 1)' }]}><Input maxLength={2000} /></Form.Item></Col>
          )}
          {lockedMunicipality ? (
            <Col xs={24} md={12}><Form.Item label="City/Municipality"><Input disabled value={`${initial?.municipalityName}, ${initial?.provinceName}`} /></Form.Item></Col>
          ) : (
            <>
              <Col xs={12} md={6}>
                <Form.Item name="provinceId" label="Province">
                  <Select showSearch optionFilterProp="label" onChange={(v) => { setProvinceId(v); form.setFieldsValue({ municipalityId: undefined }); }}
                    placeholder={initial?.provinceName} options={provinces.map((p) => ({ value: p.id, label: p.name }))} />
                </Form.Item>
              </Col>
              <Col xs={12} md={6}>
                <Form.Item name="municipalityId" label="City/Municipality" rules={[{ required: true, message: 'One statement covers one city/municipality' }]}>
                  <Select showSearch optionFilterProp="label"
                    options={municipalities.length ? municipalities.map((m) => ({ value: m.id, label: m.name }))
                      : initial ? [{ value: initial.municipalityId, label: initial.municipalityName }] : []} />
                </Form.Item>
              </Col>
            </>
          )}
          <Col xs={24} md={12}>
            <Form.Item name="filingBasis" label="Filed under" rules={[{ required: true }]}
              extra="Recorded only; PRIME enforces no filing period (DOMAIN VERIFICATION REQUIRED).">
              <Select options={Object.entries(filingBasisLabel).map(([value, label]) => ({ value, label }))} />
            </Form.Item>
          </Col>
        </Row>
        <Typography.Title level={5}>Execution</Typography.Title>
        <Row gutter={16}>
          <Col xs={12} md={6}><Form.Item name="signedOn" label="Signed on"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="signedAt" label="Signed at"><Input maxLength={300} /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="thumbmarked" valuePropName="checked" label=" "><Checkbox>Thumbmarked (two witnesses required)</Checkbox></Form.Item></Col>
          {thumbmarked && (
            <>
              <Col xs={12}><Form.Item name="witness1" label="Witness"><Input maxLength={300} /></Form.Item></Col>
              <Col xs={12}><Form.Item name="witness2" label="Witness"><Input maxLength={300} /></Form.Item></Col>
            </>
          )}
        </Row>
        <Typography.Title level={5}>Jurat</Typography.Title>
        <Row gutter={16}>
          <Col xs={12} md={6}><Form.Item name="swornOn" label="Subscribed and sworn on"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="swornAt" label="At"><Input maxLength={300} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="administeringOfficer" label="Administering officer"><Input maxLength={300} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="officerTin" label="Officer's TIN"><Input maxLength={50} /></Form.Item></Col>
          <Col xs={24} md={12}>
            <Form.Item name="identityDocument" label="Identity document exhibited" extra="Free text — which identity evidence a jurat requires is a notarial rule (DOMAIN VERIFICATION REQUIRED).">
              <Input maxLength={300} />
            </Form.Item>
          </Col>
          <Col xs={12} md={6}><Form.Item name="identityDocumentIssuedOn" label="Issued on"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="identityDocumentIssuedAt" label="Issued at"><Input maxLength={300} /></Form.Item></Col>
        </Row>
        <Row gutter={16}>
          <Col xs={12} md={6}><Form.Item name="receivedOn" label="Received by the Assessor's Office on"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={24} md={18}><Form.Item name="remarks" label="Remarks"><Input maxLength={1000} /></Form.Item></Col>
        </Row>
        <Button type="primary" htmlType="submit" loading={save.isPending}>{statement ? 'Save draft' : 'Create draft'}</Button>
      </Form>
    </Card>
  );
}

function HeaderView({ statement: s }: { statement: SwornStatementDto }) {
  return (
    <Card title="Declarant, execution and jurat">
      <Descriptions size="small" column={{ xs: 1, md: 3 }} bordered>
        <Descriptions.Item label="Declarant">{s.declarantName}</Descriptions.Item>
        <Descriptions.Item label="Citizenship / civil status">{dash(s.citizenship)} / {dash(s.civilStatus)}</Descriptions.Item>
        <Descriptions.Item label="TIN">{dash(s.declarantTin)}</Descriptions.Item>
        <Descriptions.Item label="Postal address">{dash(s.postalAddress)}</Descriptions.Item>
        <Descriptions.Item label="Capacity">{declarantCapacityLabel[s.capacity]}</Descriptions.Item>
        <Descriptions.Item label="Owned by">{dash(s.ownerNames ?? (s.capacity === 'Owner' ? s.declarantName : null))}</Descriptions.Item>
        <Descriptions.Item label="City/Municipality">{s.municipalityName}, {s.provinceName}</Descriptions.Item>
        <Descriptions.Item label="Filed under">{filingBasisLabel[s.filingBasis]}</Descriptions.Item>
        <Descriptions.Item label="Signed">{dash(s.signedOn)} at {dash(s.signedAt)}{s.thumbmarked ? ' (thumbmarked)' : ''}</Descriptions.Item>
        {s.thumbmarked && <Descriptions.Item label="Witnesses">{dash(s.witness1)}; {dash(s.witness2)}</Descriptions.Item>}
        <Descriptions.Item label="Sworn">{dash(s.swornOn)} at {dash(s.swornAt)} before {dash(s.administeringOfficer)}</Descriptions.Item>
        <Descriptions.Item label="Identity document">{dash(s.identityDocument)}{s.identityDocumentIssuedOn ? `, issued ${s.identityDocumentIssuedOn}` : ''}{s.identityDocumentIssuedAt ? ` at ${s.identityDocumentIssuedAt}` : ''}</Descriptions.Item>
        <Descriptions.Item label="Received on">{dash(s.receivedOn)}</Descriptions.Item>
        <Descriptions.Item label="Remarks">{dash(s.remarks)}</Descriptions.Item>
      </Descriptions>
    </Card>
  );
}

/** A short description of an item, per its kind's columns. */
function describe(i: SwornStatementItemDto): string {
  switch (i.kind) {
    case 'Land':
      return [i.lotNumber && `Lot ${i.lotNumber}`, i.blockNumber && `Blk ${i.blockNumber}`, i.cadastralNumber && `Cad. ${i.cadastralNumber}`,
        i.titleNumber && `Title ${i.titleNumber}`, `${i.area} ${i.areaUnit}`, i.classificationName].filter(Boolean).join(' · ');
    case 'Building':
      return [`${i.floorArea} sqm`, i.storeys && `${i.storeys} storey(s)`, i.description, i.yearCompleted && `completed ${i.yearCompleted}`,
        i.actualUseName, i.lotOwnerName && `lot of ${i.lotOwnerName}`].filter(Boolean).join(' · ');
    case 'Machinery':
      return [i.description, i.dateAcquired && `acquired ${i.dateAcquired}`, i.dateOperationCommenced && `operating ${i.dateOperationCommenced}`,
        i.acquisitionCost !== null && `cost ${formatMoney(i.acquisitionCost)}`, i.installationCost !== null && `installation ${formatMoney(i.installationCost)}`,
        i.depreciation !== null && `depreciation ${formatMoney(i.depreciation)}`].filter(Boolean).join(' · ');
    default:
      return [i.improvementKindName, i.productiveCount !== null && `${i.productiveCount} productive`, i.nonProductiveCount !== null && `${i.nonProductiveCount} non-productive`,
        i.annualProduct, i.ages && `ages ${i.ages}`].filter(Boolean).join(' · ');
  }
}

function ItemsCard({ statement: s }: { statement: SwornStatementDto }) {
  const [adding, setAdding] = useState(false);
  const [linking, setLinking] = useState<SwornStatementItemDto | null>(null);
  const remove = useRemoveSwornStatementItem(s.id);
  const draft = s.status === 'Draft';
  return (
    <Card title="Properties declared" extra={draft && <Button icon={<PlusOutlined />} onClick={() => setAdding(true)}>Add property</Button>}>
      {remove.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not removed" description={errorText(remove.error)} />}
      <Table<SwornStatementItemDto> rowKey="id" size="small" dataSource={s.items} pagination={false} scroll={{ x: true }}
        locale={{ emptyText: 'No property declared yet' }}
        summary={() => (
          <Table.Summary.Row>
            <Table.Summary.Cell index={0} colSpan={5} align="right"><b>Total declared</b></Table.Summary.Cell>
            <Table.Summary.Cell index={1} align="right"><b>{formatMoney(s.totalDeclaredValue)}</b></Table.Summary.Cell>
            <Table.Summary.Cell index={2} />
          </Table.Summary.Row>
        )}
        columns={[
          { title: '#', dataIndex: 'sequence', width: 40 },
          { title: 'Part', dataIndex: 'kind', render: (k: SwornStatementItemKind) => swornItemKindLabel[k] },
          { title: 'Existing Tax Dec. No.', render: (_, i) => (i.isNew ? <Tag color="blue">NEW</Tag> : i.tdNumber) },
          { title: 'Description', render: (_, i) => describe(i) },
          { title: 'Location', dataIndex: 'location', render: dash },
          { title: 'Declared value', dataIndex: 'declaredMarketValue', align: 'right', render: formatMoney },
          {
            title: 'Unit', render: (_, i) => (
              <Space>
                {i.rpuNumber ?? (i.isNew || !i.taxDeclarationId ? <Typography.Text type="secondary">not linked</Typography.Text> : '—')}
                {!draft && s.status !== 'Cancelled' && !i.rpuId && <Button size="small" onClick={() => setLinking(i)}>Link to unit</Button>}
                {draft && (
                  <Popconfirm title="Remove this property from the draft?" onConfirm={() => remove.mutate(i.id)}>
                    <Button size="small" danger>Remove</Button>
                  </Popconfirm>
                )}
              </Space>
            ),
          },
        ]} />
      {adding && <AddItemModal statement={s} onClose={() => setAdding(false)} />}
      {linking && <LinkItemModal statement={s} item={linking} onClose={() => setLinking(null)} />}
    </Card>
  );
}

type Existing = 'prime' | 'number' | 'new';

interface ItemForm {
  kind: SwornStatementItemKind; existing: Existing; existingTdNumber?: string; location?: string; declaredMarketValue: number;
  lotNumber?: string; blockNumber?: string; cadastralNumber?: string; titleNumber?: string; area?: number; areaUnit?: string; classificationId?: string;
  floorArea?: number; storeys?: number; description?: string; yearCompleted?: number; actualUseId?: string; lotOwnerName?: string;
  dateAcquired?: Dayjs; dateOperationCommenced?: Dayjs; acquisitionCost?: number; installationCost?: number; depreciation?: number;
  improvementKindId?: string; productiveCount?: number; nonProductiveCount?: number; annualProduct?: string; ages?: string;
}

function AddItemModal({ statement, onClose }: { statement: SwornStatementDto; onClose: () => void }) {
  const [form] = Form.useForm<ItemForm>();
  const kind = Form.useWatch('kind', form) ?? 'Land';
  const existing = Form.useWatch('existing', form) ?? 'new';
  const [unit, setUnit] = useState<PickedUnit | null>(null);
  const add = useAddSwornStatementItem(statement.id);
  const { data: classifications = [] } = useClassifications();
  const { data: actualUses = [] } = useActualUses();
  const { data: improvementKinds = [] } = useImprovementKinds();
  const lookup = (rows: { id: string; code: string; name: string }[]) => rows.map((r) => ({ value: r.id, label: `${r.code} — ${r.name}` }));

  function submit(v: ItemForm) {
    const request: AddSwornStatementItemRequest = {
      kind: v.kind, taxDeclarationId: v.existing === 'prime' ? unit?.taxDeclarationId ?? null : null,
      existingTdNumber: v.existing === 'number' ? text(v.existingTdNumber) : null, location: text(v.location), declaredMarketValue: v.declaredMarketValue,
      lotNumber: text(v.lotNumber), blockNumber: text(v.blockNumber), cadastralNumber: text(v.cadastralNumber), titleNumber: text(v.titleNumber),
      area: v.area ?? null, areaUnit: text(v.areaUnit), classificationId: v.classificationId ?? null,
      floorArea: v.floorArea ?? null, storeys: v.storeys ?? null, description: text(v.description), yearCompleted: v.yearCompleted ?? null,
      actualUseId: v.actualUseId ?? null, lotOwnerName: text(v.lotOwnerName),
      dateAcquired: day(v.dateAcquired), dateOperationCommenced: day(v.dateOperationCommenced), acquisitionCost: v.acquisitionCost ?? null,
      installationCost: v.installationCost ?? null, depreciation: v.depreciation ?? null,
      improvementKindId: v.improvementKindId ?? null, productiveCount: v.productiveCount ?? null, nonProductiveCount: v.nonProductiveCount ?? null,
      annualProduct: text(v.annualProduct), ages: text(v.ages),
    };
    add.mutate(request, { onSuccess: onClose });
  }

  const tdMissing = existing === 'prime' && !unit?.taxDeclarationId;
  return (
    <Modal open title="Add a declared property" onCancel={onClose} footer={null} width={760} destroyOnHidden>
      {add.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not added" description={errorText(add.error)} />}
      <Form form={form} layout="vertical" initialValues={{ kind: 'Land', existing: 'new', areaUnit: 'sqm' }} onFinish={submit}>
        <Form.Item name="kind" label="Part of the statement">
          <Radio.Group optionType="button" onChange={() => setUnit(null)}
            options={Object.entries(swornItemKindLabel).map(([value, label]) => ({ value, label }))} />
        </Form.Item>
        <Form.Item name="existing" label="Existing Tax Declaration">
          <Radio.Group onChange={() => setUnit(null)} options={[
            { value: 'prime', label: 'A Tax Declaration in PRIME' },
            { value: 'number', label: 'A TD number PRIME does not have' },
            { value: 'new', label: 'NEW — not previously declared' },
          ]} />
        </Form.Item>
        {existing === 'prime' && (
          <Form.Item label="RPU" required>
            <UnitPicker key={kind} municipalityId={statement.municipalityId} kind={kind} onChange={setUnit} />
          </Form.Item>
        )}
        {existing === 'number' && (
          <Form.Item name="existingTdNumber" label="TD number" rules={[{ required: true, message: 'Enter the TD number' }]}><Input maxLength={100} /></Form.Item>
        )}
        <Row gutter={12}>
          {kind === 'Land' && (
            <>
              <Col span={6}><Form.Item name="lotNumber" label="Lot No."><Input maxLength={100} /></Form.Item></Col>
              <Col span={6}><Form.Item name="blockNumber" label="Block No."><Input maxLength={100} /></Form.Item></Col>
              <Col span={6}><Form.Item name="cadastralNumber" label="Cadastral/PLS No."><Input maxLength={100} /></Form.Item></Col>
              <Col span={6}><Form.Item name="titleNumber" label="Title"><Input maxLength={100} /></Form.Item></Col>
              <Col span={8}><Form.Item name="area" label="Area" rules={[{ required: true, message: 'Enter the area' }]}><InputNumber min={0.0001} style={{ width: '100%' }} /></Form.Item></Col>
              <Col span={6}><Form.Item name="areaUnit" label="Area unit" rules={[{ required: true }]}><Select options={[{ value: 'sqm', label: 'sq.m.' }, { value: 'ha', label: 'ha' }]} /></Form.Item></Col>
              <Col span={10}><Form.Item name="classificationId" label="Classification"><Select allowClear showSearch optionFilterProp="label" options={lookup(classifications)} /></Form.Item></Col>
            </>
          )}
          {kind === 'Building' && (
            <>
              <Col span={8}><Form.Item name="floorArea" label="Total floor area (sq.m.)" rules={[{ required: true, message: 'Enter the floor area' }]}><InputNumber min={0.0001} style={{ width: '100%' }} /></Form.Item></Col>
              <Col span={6}><Form.Item name="storeys" label="No. of storeys"><InputNumber min={1} precision={0} style={{ width: '100%' }} /></Form.Item></Col>
              <Col span={10}><Form.Item name="yearCompleted" label="Year completed/occupied"><InputNumber min={1800} max={2200} precision={0} style={{ width: '100%' }} /></Form.Item></Col>
              <Col span={24}><Form.Item name="description" label="General description"><Input maxLength={500} /></Form.Item></Col>
              <Col span={12}><Form.Item name="actualUseId" label="Actual use"><Select allowClear showSearch optionFilterProp="label" options={lookup(actualUses)} /></Form.Item></Col>
              <Col span={12}><Form.Item name="lotOwnerName" label="Owner of the lot"><Input maxLength={300} /></Form.Item></Col>
            </>
          )}
          {kind === 'Machinery' && (
            <>
              <Col span={24}><Form.Item name="description" label="Description" rules={[{ required: true, message: 'Describe the machine' }]}><Input maxLength={500} /></Form.Item></Col>
              <Col span={12}><Form.Item name="dateAcquired" label="Date acquired"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
              <Col span={12}><Form.Item name="dateOperationCommenced" label="Date operation commenced"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
              <Col span={8}><Form.Item name="acquisitionCost" label="Original acquisition cost"><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
              <Col span={8}><Form.Item name="installationCost" label="Cost of installation"><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
              <Col span={8}><Form.Item name="depreciation" label="Value of depreciation"><InputNumber min={0} precision={2} style={{ width: '100%' }} /></Form.Item></Col>
            </>
          )}
          {kind === 'OtherImprovement' && (
            <>
              <Col span={12}><Form.Item name="improvementKindId" label="Kind of trees/plants" rules={[{ required: true, message: 'Choose the kind' }]}>
                <Select showSearch optionFilterProp="label" options={lookup(improvementKinds)}
                  notFoundContent="No improvement kinds are configured" />
              </Form.Item></Col>
              <Col span={6}><Form.Item name="productiveCount" label="Productive"><InputNumber min={0} precision={0} style={{ width: '100%' }} /></Form.Item></Col>
              <Col span={6}><Form.Item name="nonProductiveCount" label="Non-productive"><InputNumber min={0} precision={0} style={{ width: '100%' }} /></Form.Item></Col>
              <Col span={12}><Form.Item name="annualProduct" label="Annual product per tree/plant"><Input maxLength={200} /></Form.Item></Col>
              <Col span={12}><Form.Item name="ages" label="Ages"><Input maxLength={200} /></Form.Item></Col>
            </>
          )}
          <Col span={14}><Form.Item name="location" label="Location" extra={existing === 'prime' ? 'Blank: taken from the property' : undefined}><Input maxLength={500} /></Form.Item></Col>
          <Col span={10}>
            <Form.Item name="declaredMarketValue" label="True current and fair market value (declared)" rules={[{ required: true, message: 'Enter the declared value' }]}>
              <InputNumber min={0} precision={2} style={{ width: '100%' }} />
            </Form.Item>
          </Col>
        </Row>
        <Button type="primary" htmlType="submit" loading={add.isPending} disabled={tdMissing}>Add</Button>
        {tdMissing && <Typography.Text type="secondary" style={{ marginLeft: 12 }}>Choose a unit with an approved Tax Declaration.</Typography.Text>}
      </Form>
    </Modal>
  );
}

function LinkItemModal({ statement, item, onClose }: { statement: SwornStatementDto; item: SwornStatementItemDto; onClose: () => void }) {
  const [unit, setUnit] = useState<PickedUnit | null>(null);
  const link = useLinkSwornStatementItem(statement.id);
  return (
    <Modal open title={`Link item ${item.sequence} to its registered unit`} okText="Link" onCancel={onClose}
      okButtonProps={{ disabled: !unit, loading: link.isPending }}
      onOk={() => unit && link.mutate({ itemId: item.id, rpuId: unit.rpuId }, { onSuccess: onClose })} destroyOnHidden>
      {link.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not linked" description={errorText(link.error)} />}
      <Typography.Paragraph type="secondary">{swornItemKindLabel[item.kind]}: {describe(item)}</Typography.Paragraph>
      <UnitPicker municipalityId={statement.municipalityId} kind={item.kind} onChange={setUnit} />
    </Modal>
  );
}

function ActionsCard({ statement: s }: { statement: SwornStatementDto }) {
  const navigate = useNavigate();
  const [filing, setFiling] = useState(false);
  const [number, setNumber] = useState('');
  const [cancelling, setCancelling] = useState(false);
  const [reason, setReason] = useState('');
  const file = useFileSwornStatement(s.id);
  const cancel = useCancelSwornStatement(s.id);
  const live = s.status === 'Draft' || s.status === 'Filed';
  return (
    <Card title="Form and filing">
      {(file.isError || cancel.isError) && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not done" description={errorText(file.error ?? cancel.error)} />}
      <Space wrap>
        <PrintFormButton formCode="SWORN_STATEMENT" subjectId={s.id} issuable={false} />
        {s.status === 'Draft' && <Button type="primary" onClick={() => setFiling(true)}>File statement</Button>}
        {s.status !== 'Draft' && s.status !== 'Cancelled' && <PrintFormButton formCode="SWORN_STATEMENT" subjectId={s.id} issuable label="Issue" />}
        {s.status === 'Filed' && !s.supersededById && <Button onClick={() => navigate(`/sworn-statements/new?supersedes=${s.id}`)}>Correct (new statement)</Button>}
        {live && <Button danger onClick={() => setCancelling(true)}>Cancel statement</Button>}
      </Space>
      <Modal open={filing} title="File the sworn statement" okText="File" onCancel={() => setFiling(false)} okButtonProps={{ loading: file.isPending }}
        onOk={() => file.mutate(text(number), { onSuccess: () => setFiling(false) })}>
        <Typography.Paragraph>
          Filing locks the statement as received. It needs at least one property, the signing date, the jurat and the received date
          {s.thumbmarked ? ', and two witnesses' : ''}.{s.supersedesId ? ' The statement it corrects becomes Superseded.' : ''}
        </Typography.Paragraph>
        <Input placeholder="Index No. (blank: generated if a numbering scheme is in force)" value={number} onChange={(e) => setNumber(e.target.value)} maxLength={100} />
      </Modal>
      <Modal open={cancelling} title="Cancel the sworn statement" okText="Cancel statement" okButtonProps={{ danger: true, disabled: !reason.trim(), loading: cancel.isPending }}
        onCancel={() => setCancelling(false)} onOk={() => cancel.mutate(reason.trim(), { onSuccess: () => setCancelling(false) })}>
        <Typography.Paragraph>The statement is kept, marked Cancelled.{s.supersedesId && s.status === 'Filed' ? ' The statement it corrected is back in force.' : ''}</Typography.Paragraph>
        <Input.TextArea placeholder="Reason" value={reason} onChange={(e) => setReason(e.target.value)} maxLength={1000} rows={3} />
      </Modal>
    </Card>
  );
}
