import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, Button, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  useBuildingPermits, useLinkBuildingPermit, useLinkMachineryRegistration, useMachineryRegistrations, useSaveBuildingPermit,
  useSaveMachineryRegistration,
} from '../../api/marketData';
import {
  useAllMunicipalities, useBarangays, useBuildingTypes, useClassifications, useMachineryTypes, useStructuralTypes,
} from '../../api/referenceData';
import { usePropertySearch } from '../../api/properties';
import { usePropertyRpus } from '../../api/rpus';
import { useBuildingByRpu } from '../../api/buildings';
import { useMachineryUnitsByRpu } from '../../api/machinery';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import {
  buildingPermitScopeLabel, type BuildingPermitDto, type BuildingPermitScope, type MachineryRegistrationDto,
} from '../../lib/marketDataTypes';
import { CancelModal } from './MarketTransactionsTab';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const day = (v?: dayjs.Dayjs | null) => (v ? v.format('YYYY-MM-DD') : null);
const toDay = (v: string | null) => (v ? dayjs(v) : undefined);

function Declared({ pin, propertyId }: { pin: string | null; propertyId: string | null }) {
  return propertyId ? <Link to={`/properties/${propertyId}`}>{pin}</Link> : <Tag color="gold">not yet declared</Tag>;
}

/** Building permits received from the building official (LGC §290; LAM 2025 Book I pp.22–23). */
export function BuildingPermitsTab({ municipalityId, unlinkedOnly = false }: { municipalityId?: string; unlinkedOnly?: boolean }) {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const { data, isFetching } = useBuildingPermits({ municipalityId, unlinkedOnly, search: search || undefined, page, pageSize: 20 });
  const link = useLinkBuildingPermit();
  const [editing, setEditing] = useState<BuildingPermitDto | 'new' | null>(null);
  const [linking, setLinking] = useState<BuildingPermitDto | null>(null);
  const [cancelling, setCancelling] = useState<BuildingPermitDto | null>(null);

  return (
    <>
      <Space wrap style={{ marginBottom: 12, justifyContent: 'space-between', width: '100%' }}>
        <Input.Search allowClear placeholder="Permit no., permittee or TD" style={{ width: 280 }} onSearch={(v) => { setSearch(v); setPage(1); }} />
        {!unlinkedOnly && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New permit</Button>}
      </Space>
      {link.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not link" description={errorText(link.error)} />}
      <Table<BuildingPermitDto> rowKey="id" size="small" loading={isFetching} dataSource={data?.items ?? []} scroll={{ x: 'max-content' }}
        pagination={{ current: page, pageSize: 20, total: data?.totalCount ?? 0, onChange: setPage, showSizeChanger: false }}
        locale={{ emptyText: unlinkedOnly ? 'No permit awaits a declaration' : 'No permits recorded' }}
        columns={[
          { title: 'Permit no.', dataIndex: 'permitNumber' },
          { title: 'Issued', dataIndex: 'issuedOn' },
          { title: 'Permittee', dataIndex: 'permitteeName' },
          { title: 'Location', render: (_, p) => [p.blockLotNumber, p.street, p.barangayName, p.municipalityName].filter(Boolean).join(', ') },
          { title: 'Scope', dataIndex: 'scope', render: (s: BuildingPermitScope) => buildingPermitScopeLabel[s] },
          { title: 'Floor area', dataIndex: 'totalFloorArea', align: 'right' },
          { title: 'Est. cost', dataIndex: 'estimatedCost', align: 'right', render: (v: number | null) => (v === null ? '—' : formatMoney(v)) },
          { title: 'Declared', render: (_, p) => <Declared pin={p.buildingPin} propertyId={p.buildingPropertyId} /> },
          {
            title: 'Actions', render: (_, p) => (
              <Space size={4} wrap>
                {!unlinkedOnly && <Button size="small" onClick={() => setEditing(p)}>Edit</Button>}
                {p.suggestedBuildingId && (
                  <Button size="small" type="primary" loading={link.isPending} onClick={() => link.mutate({ id: p.id, targetId: p.suggestedBuildingId })}
                    title="A building on record carries this permit number">Link suggested building</Button>
                )}
                {!p.buildingId && <Button size="small" onClick={() => { link.reset(); setLinking(p); }}>Link…</Button>}
                {p.buildingId && <Button size="small" onClick={() => link.mutate({ id: p.id, targetId: null })}>Unlink</Button>}
                {!unlinkedOnly && <Button size="small" danger onClick={() => setCancelling(p)}>Cancel</Button>}
              </Space>
            ),
          },
        ]} />
      {editing && <PermitModal value={editing === 'new' ? null : editing} defaultMunicipalityId={municipalityId} onClose={() => setEditing(null)} />}
      {linking && (
        <LinkModal kind="Building" title={`Link permit ${linking.permitNumber}`} onClose={() => setLinking(null)} pending={link.isPending}
          onLink={(targetId) => link.mutate({ id: linking.id, targetId }, { onSuccess: () => setLinking(null) })} error={link.isError ? errorText(link.error) : null} />
      )}
      {cancelling && <CancelModal path="building-permits" id={cancelling.id} title={`Cancel permit ${cancelling.permitNumber}`} onClose={() => setCancelling(null)} />}
    </>
  );
}

/** Certificates of registration of installation of machinery (LGC §210; LAM 2025 Book I p.23). */
export function MachineryRegistrationsTab({ municipalityId, unlinkedOnly = false }: { municipalityId?: string; unlinkedOnly?: boolean }) {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const { data, isFetching } = useMachineryRegistrations({ municipalityId, unlinkedOnly, search: search || undefined, page, pageSize: 20 });
  const link = useLinkMachineryRegistration();
  const [editing, setEditing] = useState<MachineryRegistrationDto | 'new' | null>(null);
  const [linking, setLinking] = useState<MachineryRegistrationDto | null>(null);
  const [cancelling, setCancelling] = useState<MachineryRegistrationDto | null>(null);

  return (
    <>
      <Space wrap style={{ marginBottom: 12, justifyContent: 'space-between', width: '100%' }}>
        <Input.Search allowClear placeholder="Certificate no., owner or TD" style={{ width: 280 }} onSearch={(v) => { setSearch(v); setPage(1); }} />
        {!unlinkedOnly && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New certificate</Button>}
      </Space>
      {link.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not link" description={errorText(link.error)} />}
      <Table<MachineryRegistrationDto> rowKey="id" size="small" loading={isFetching} dataSource={data?.items ?? []} scroll={{ x: 'max-content' }}
        pagination={{ current: page, pageSize: 20, total: data?.totalCount ?? 0, onChange: setPage, showSizeChanger: false }}
        locale={{ emptyText: unlinkedOnly ? 'No certificate awaits a declaration' : 'No certificates recorded' }}
        columns={[
          { title: 'Certificate no.', dataIndex: 'certificateNumber' },
          { title: 'Issued', dataIndex: 'issuedOn' },
          { title: 'Owner', dataIndex: 'ownerName' },
          { title: 'Location', render: (_, m) => [m.location, m.barangayName, m.municipalityName].filter(Boolean).join(', ') },
          { title: 'Machinery', render: (_, m) => [m.machineryTypeName, m.description, m.brandModel].filter(Boolean).join(' · ') },
          { title: 'Cost', dataIndex: 'cost', align: 'right', render: (v: number | null) => (v === null ? '—' : formatMoney(v)) },
          { title: 'Declared', render: (_, m) => <Declared pin={m.machineryPin} propertyId={m.machineryPropertyId} /> },
          {
            title: 'Actions', render: (_, m) => (
              <Space size={4} wrap>
                {!unlinkedOnly && <Button size="small" onClick={() => setEditing(m)}>Edit</Button>}
                {m.suggestedMachineryId && (
                  <Button size="small" type="primary" loading={link.isPending} onClick={() => link.mutate({ id: m.id, targetId: m.suggestedMachineryId })}>
                    Link suggested machinery
                  </Button>
                )}
                {!m.machineryId && <Button size="small" onClick={() => { link.reset(); setLinking(m); }}>Link…</Button>}
                {m.machineryId && <Button size="small" onClick={() => link.mutate({ id: m.id, targetId: null })}>Unlink</Button>}
                {!unlinkedOnly && <Button size="small" danger onClick={() => setCancelling(m)}>Cancel</Button>}
              </Space>
            ),
          },
        ]} />
      {editing && <RegistrationModal value={editing === 'new' ? null : editing} defaultMunicipalityId={municipalityId} onClose={() => setEditing(null)} />}
      {linking && (
        <LinkModal kind="Machinery" title={`Link certificate ${linking.certificateNumber}`} onClose={() => setLinking(null)} pending={link.isPending}
          onLink={(targetId) => link.mutate({ id: linking.id, targetId }, { onSuccess: () => setLinking(null) })} error={link.isError ? errorText(link.error) : null} />
      )}
      {cancelling && <CancelModal path="machinery-registrations" id={cancelling.id} title={`Cancel certificate ${cancelling.certificateNumber}`} onClose={() => setCancelling(null)} />}
    </>
  );
}

/** Permits and certificates with nothing declared yet: properties to look for (discovery, LGC §204). */
export function DiscoveryLeadsTab({ municipalityId }: { municipalityId?: string }) {
  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
        Permits (other than demolitions) and machinery certificates not yet linked to a declared building or machinery. A building or machinery on
        record with the same number is suggested; link it, or open a discovery transaction on the property.
      </Typography.Paragraph>
      <Typography.Title level={5}>Building permits</Typography.Title>
      <BuildingPermitsTab municipalityId={municipalityId} unlinkedOnly />
      <Typography.Title level={5}>Machinery certificates</Typography.Title>
      <MachineryRegistrationsTab municipalityId={municipalityId} unlinkedOnly />
    </Space>
  );
}

/** Pick a declared building or machinery: a property by PIN, one of its units, then (machinery) the machine. */
function LinkModal({ kind, title, onLink, onClose, pending, error }: {
  kind: 'Building' | 'Machinery'; title: string; onLink: (targetId: string) => void; onClose: () => void; pending: boolean; error: string | null;
}) {
  const [term, setTerm] = useState('');
  const [propertyId, setPropertyId] = useState<string>();
  const [rpuId, setRpuId] = useState<string>();
  const [machineryId, setMachineryId] = useState<string>();
  const { data: properties, isFetching } = usePropertySearch({ searchTerm: term || undefined, pageSize: 20 });
  const { data: rpus = [] } = usePropertyRpus(propertyId ?? '');
  const { data: building } = useBuildingByRpu(kind === 'Building' ? rpuId : undefined);
  const { data: machines = [] } = useMachineryUnitsByRpu(kind === 'Machinery' ? rpuId : undefined);
  const targetId = kind === 'Building' ? building?.id : machineryId;
  return (
    <Modal open title={title} okText="Link" okButtonProps={{ disabled: !targetId, loading: pending }} onOk={() => targetId && onLink(targetId)} onCancel={onClose} destroyOnHidden>
      {error && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not link" description={error} />}
      <Space orientation="vertical" style={{ width: '100%' }}>
        <Select showSearch filterOption={false} onSearch={setTerm} loading={isFetching} value={propertyId} aria-label="Property"
          onChange={(v) => { setPropertyId(v); setRpuId(undefined); setMachineryId(undefined); }} style={{ width: '100%' }} placeholder="Property (PIN, owner, lot)"
          options={properties?.items.map((p) => ({ value: p.id, label: `${p.propertyIdentificationNumber} — ${p.barangayName}` }))} />
        <Select value={rpuId} onChange={(v) => { setRpuId(v); setMachineryId(undefined); }} disabled={!propertyId} style={{ width: '100%' }} aria-label="Unit"
          placeholder={`${kind} unit`} notFoundContent={`No ${kind.toLowerCase()} unit on this property`}
          options={rpus.filter((r) => r.rpuType === kind && r.status === 'Active').map((r) => ({ value: r.id, label: `RPU ${r.rpuNumber}` }))} />
        {kind === 'Machinery' && (
          <Select value={machineryId} onChange={setMachineryId} disabled={!rpuId} style={{ width: '100%' }} aria-label="Machine" placeholder="Machine"
            options={machines.map((m) => ({ value: m.id, label: [m.machineryTypeName, m.description, m.brand, m.model].filter(Boolean).join(' · ') }))} />
        )}
        {kind === 'Building' && rpuId && !building && <Typography.Text type="secondary">This unit has no building description yet.</Typography.Text>}
      </Space>
    </Modal>
  );
}

function PlaceFields({ form }: { form: ReturnType<typeof Form.useForm>[0] }) {
  const municipalityId = Form.useWatch('municipalityId', form) as string | undefined;
  const { data: municipalities = [] } = useAllMunicipalities();
  const { data: barangays = [] } = useBarangays(municipalityId);
  return (
    <>
      <Form.Item name="municipalityId" label="City/municipality" rules={[{ required: true }]}>
        <Select showSearch optionFilterProp="label" style={{ width: 220 }} onChange={() => form.setFieldValue('barangayId', undefined)}
          options={municipalities.map((m) => ({ value: m.id, label: m.name }))} />
      </Form.Item>
      <Form.Item name="barangayId" label="Barangay">
        <Select allowClear showSearch optionFilterProp="label" style={{ width: 200 }} disabled={!municipalityId}
          options={barangays.map((b) => ({ value: b.id, label: b.name }))} />
      </Form.Item>
    </>
  );
}

function PermitModal({ value, defaultMunicipalityId, onClose }: { value: BuildingPermitDto | null; defaultMunicipalityId?: string; onClose: () => void }) {
  const save = useSaveBuildingPermit();
  const [form] = Form.useForm();
  const { data: buildingTypes = [] } = useBuildingTypes();
  const { data: structuralTypes = [] } = useStructuralTypes();
  const { data: classes = [] } = useClassifications();
  const opts = (rows: { id: string; name: string }[]) => rows.map((r) => ({ value: r.id, label: r.name }));
  return (
    <Modal open title={value ? `Edit permit ${value.permitNumber}` : 'New building permit'} footer={null} width={820} destroyOnHidden onCancel={onClose}>
      {save.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not save" description={errorText(save.error)} />}
      <Form form={form} layout="vertical"
        initialValues={value
          ? { ...value, issuedOn: toDay(value.issuedOn), proposedConstructionDate: toDay(value.proposedConstructionDate),
              expectedCompletionDate: toDay(value.expectedCompletionDate), receivedOn: toDay(value.receivedOn) }
          : { municipalityId: defaultMunicipalityId, scope: 'NewConstruction', receivedOn: dayjs() }}
        onFinish={(v) => save.mutate({
          id: value?.id, permitNumber: v.permitNumber, issuedOn: day(v.issuedOn)!, proposedConstructionDate: day(v.proposedConstructionDate),
          expectedCompletionDate: day(v.expectedCompletionDate), permitteeName: v.permitteeName, permitteeAddress: v.permitteeAddress ?? null,
          taxDeclarationNumber: v.taxDeclarationNumber ?? null, municipalityId: v.municipalityId, barangayId: v.barangayId ?? null,
          blockLotNumber: v.blockLotNumber ?? null, street: v.street ?? null, scope: v.scope, buildingTypeId: v.buildingTypeId ?? null,
          structuralTypeId: v.structuralTypeId ?? null, storeys: v.storeys ?? null, totalFloorArea: v.totalFloorArea ?? null,
          estimatedCost: v.estimatedCost ?? null, classificationId: v.classificationId ?? null, receivedOn: day(v.receivedOn), remarks: v.remarks ?? null,
        }, { onSuccess: onClose })}>
        <Space wrap>
          <Form.Item name="permitNumber" label="Permit no." rules={[{ required: true }, { max: 100 }]}><Input style={{ width: 160 }} /></Form.Item>
          <Form.Item name="issuedOn" label="Date issued" rules={[{ required: true }]}><DatePicker /></Form.Item>
          <Form.Item name="proposedConstructionDate" label="Construction starts"><DatePicker /></Form.Item>
          <Form.Item name="expectedCompletionDate" label="Expected completion"><DatePicker /></Form.Item>
          <Form.Item name="receivedOn" label="Received"><DatePicker /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="permitteeName" label="Permittee / owner" rules={[{ required: true }, { max: 300 }]}><Input style={{ width: 260 }} /></Form.Item>
          <Form.Item name="permitteeAddress" label="Address"><Input maxLength={1000} style={{ width: 280 }} /></Form.Item>
          <Form.Item name="taxDeclarationNumber" label="TD no. (as stated)"><Input maxLength={100} style={{ width: 160 }} /></Form.Item>
        </Space>
        <Space wrap>
          <PlaceFields form={form} />
          <Form.Item name="blockLotNumber" label="Block / lot"><Input maxLength={100} style={{ width: 120 }} /></Form.Item>
          <Form.Item name="street" label="Street"><Input maxLength={300} style={{ width: 180 }} /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="scope" label="Scope of work" rules={[{ required: true }]}>
            <Select style={{ width: 170 }} options={(Object.keys(buildingPermitScopeLabel) as BuildingPermitScope[]).map((s) => ({ value: s, label: buildingPermitScopeLabel[s] }))} />
          </Form.Item>
          <Form.Item name="buildingTypeId" label="Kind of building"><Select allowClear style={{ width: 180 }} options={opts(buildingTypes)} /></Form.Item>
          <Form.Item name="structuralTypeId" label="Structural type"><Select allowClear style={{ width: 150 }} options={opts(structuralTypes)} /></Form.Item>
          <Form.Item name="classificationId" label="Classification"><Select allowClear style={{ width: 160 }} options={opts(classes)} /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="storeys" label="Storeys"><InputNumber<number> min={1} precision={0} style={{ width: 90 }} /></Form.Item>
          <Form.Item name="totalFloorArea" label="Total floor area (sqm)"><InputNumber<number> min={0} style={{ width: 160 }} /></Form.Item>
          <Form.Item name="estimatedCost" label="Estimated cost"><InputNumber<number> min={0} style={{ width: 180 }} /></Form.Item>
        </Space>
        <Form.Item name="remarks" label="Remarks"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={save.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}

function RegistrationModal({ value, defaultMunicipalityId, onClose }: { value: MachineryRegistrationDto | null; defaultMunicipalityId?: string; onClose: () => void }) {
  const save = useSaveMachineryRegistration();
  const [form] = Form.useForm();
  const { data: types = [] } = useMachineryTypes();
  return (
    <Modal open title={value ? `Edit certificate ${value.certificateNumber}` : 'New machinery certificate'} footer={null} width={820} destroyOnHidden onCancel={onClose}>
      {save.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not save" description={errorText(save.error)} />}
      <Form form={form} layout="vertical"
        initialValues={value
          ? { ...value, issuedOn: toDay(value.issuedOn), installationDate: toDay(value.installationDate), receivedOn: toDay(value.receivedOn) }
          : { municipalityId: defaultMunicipalityId, receivedOn: dayjs() }}
        onFinish={(v) => save.mutate({
          id: value?.id, certificateNumber: v.certificateNumber, issuedOn: day(v.issuedOn)!, ownerName: v.ownerName, ownerAddress: v.ownerAddress ?? null,
          taxDeclarationNumber: v.taxDeclarationNumber ?? null, municipalityId: v.municipalityId, barangayId: v.barangayId ?? null,
          location: v.location ?? null, machineryTypeId: v.machineryTypeId ?? null, description: v.description ?? null, brandModel: v.brandModel ?? null,
          yearAcquired: v.yearAcquired ?? null, manufacturer: v.manufacturer ?? null, cost: v.cost ?? null, currentCondition: v.currentCondition ?? null,
          installationDate: day(v.installationDate), receivedOn: day(v.receivedOn), remarks: v.remarks ?? null,
        }, { onSuccess: onClose })}>
        <Space wrap>
          <Form.Item name="certificateNumber" label="Certificate no." rules={[{ required: true }, { max: 100 }]}><Input style={{ width: 160 }} /></Form.Item>
          <Form.Item name="issuedOn" label="Date issued" rules={[{ required: true }]}><DatePicker /></Form.Item>
          <Form.Item name="installationDate" label="Installed on"><DatePicker /></Form.Item>
          <Form.Item name="receivedOn" label="Received"><DatePicker /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="ownerName" label="Owner" rules={[{ required: true }, { max: 300 }]}><Input style={{ width: 260 }} /></Form.Item>
          <Form.Item name="ownerAddress" label="Address of owner"><Input maxLength={1000} style={{ width: 280 }} /></Form.Item>
          <Form.Item name="taxDeclarationNumber" label="TD no. (as stated)"><Input maxLength={100} style={{ width: 160 }} /></Form.Item>
        </Space>
        <Space wrap>
          <PlaceFields form={form} />
          <Form.Item name="location" label="Location"><Input maxLength={300} style={{ width: 200 }} /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="machineryTypeId" label="Type of machinery"><Select allowClear style={{ width: 200 }} options={types.map((t) => ({ value: t.id, label: t.name }))} /></Form.Item>
          <Form.Item name="description" label="Description"><Input maxLength={500} style={{ width: 220 }} /></Form.Item>
          <Form.Item name="brandModel" label="Brand / model"><Input maxLength={200} style={{ width: 180 }} /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="yearAcquired" label="Year acquired"><InputNumber<number> min={1800} precision={0} style={{ width: 120 }} /></Form.Item>
          <Form.Item name="manufacturer" label="Manufacturer"><Input maxLength={200} style={{ width: 180 }} /></Form.Item>
          <Form.Item name="cost" label="Cost"><InputNumber<number> min={0} style={{ width: 180 }} /></Form.Item>
          <Form.Item name="currentCondition" label="Condition"><Input maxLength={200} style={{ width: 150 }} /></Form.Item>
        </Space>
        <Form.Item name="remarks" label="Remarks"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={save.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}
