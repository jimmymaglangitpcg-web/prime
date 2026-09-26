import { useState } from 'react';
import { Alert, Button, Card, DatePicker, Descriptions, Empty, Input, Modal, Select, Space, Table, Tabs, Tag, Typography } from 'antd';
import { EditOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import {
  type BarangayIndexDto, type CityDistrictDto, type TaxMapSectionDto,
  useCreateDistrict, useCreateSection, useDistricts, useIndexBarangays, useIndexMunicipalities, useIndexProvinces, useRetireSection,
  useSections, useSetIndexNumber, useSplitBarangay,
} from '../../api/propertyIdentification';
import { ApiRequestError } from '../../lib/apiClient';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const index = (v: string | null) => v ?? <Typography.Text type="secondary">not set</Typography.Text>;

/**
 * The Real Property Identification System's numbers (MRPAAO Ch. II §1;
 * docs/analysis/property-identification.md, step 10a-1): the LGU's index
 * number, its municipalities' or city districts', its barangays', and the
 * tax map sections of each barangay. These make up the PIN
 * (province/city · municipality/district · barangay · section · parcel).
 * Numbers come from the LGU; none is shipped. Numbers are never reused.
 */
export function PropertyIdentificationPage() {
  const provinces = useIndexProvinces();
  const [provinceId, setProvinceId] = useState<string>();
  const municipalities = useIndexMunicipalities(provinceId);
  const [municipalityId, setMunicipalityId] = useState<string>();
  const [editing, setEditing] = useState<{ kind: 'provinces' | 'municipalities'; id: string; label: string; current: string | null; digits: string } | null>(null);
  const province = provinces.data?.find((p) => p.id === provinceId);
  const municipality = municipalities.data?.find((m) => m.id === municipalityId);

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Title level={3} style={{ margin: 0 }}>Property Identification</Typography.Title>
      <Alert type="info" showIcon title="PIN = province/city · municipality/district · barangay · section · parcel"
        description="Enter the index numbers the LGU uses (MRPAAO Ch. II; the LAM prevails where it differs). Numbers are never reused: a divided barangay and a retired section keep theirs." />

      <Card>
        <Space wrap>
          <Select aria-label="Province" placeholder="Province" style={{ width: 280 }} showSearch optionFilterProp="label" value={provinceId}
            options={(provinces.data ?? []).map((p) => ({ value: p.id, label: p.name }))}
            onChange={(v) => { setProvinceId(v); setMunicipalityId(undefined); }} />
          <Select aria-label="City or municipality" placeholder="City or municipality" style={{ width: 280 }} showSearch optionFilterProp="label" value={municipalityId}
            disabled={!provinceId} options={(municipalities.data ?? []).map((m) => ({ value: m.id, label: m.name }))} onChange={setMunicipalityId} />
        </Space>
        {province && (
          <Descriptions size="small" bordered column={1} style={{ marginTop: 16 }}>
            <Descriptions.Item label={`Province: ${province.name} (1st–3rd digits)`}>
              <Space>{index(province.pinIndexNumber)}
                <Button size="small" icon={<EditOutlined />} aria-label="Edit province index number"
                  onClick={() => setEditing({ kind: 'provinces', id: province.id, label: `Province ${province.name}`, current: province.pinIndexNumber, digits: '3 digits' })} />
              </Space>
            </Descriptions.Item>
            {municipality && (
              <Descriptions.Item label={`${municipality.isCity ? 'City' : 'Municipality'}: ${municipality.name}`}>
                <Space>{index(municipality.pinIndexNumber)}
                  <Typography.Text type="secondary">
                    {municipality.pinIndexNumber?.length === 3 ? '(own 1st–3rd digits; its districts give the 4th–5th)' : '(4th–5th digits)'}
                  </Typography.Text>
                  <Button size="small" icon={<EditOutlined />} aria-label="Edit city or municipality index number"
                    onClick={() => setEditing({ kind: 'municipalities', id: municipality.id, label: municipality.name, current: municipality.pinIndexNumber,
                      digits: '3 digits for a city or Metro Manila municipality with its own number, otherwise 2' })} />
                </Space>
              </Descriptions.Item>
            )}
          </Descriptions>
        )}
      </Card>

      {municipalityId && (
        <Card>
          <Tabs items={[
            { key: 'barangays', label: 'Barangays & sections', children: <BarangaysTab municipalityId={municipalityId} /> },
            { key: 'districts', label: `Districts (${municipality?.districtCount ?? 0})`, children: <DistrictsTab municipalityId={municipalityId} /> },
          ]} />
        </Card>
      )}

      {editing && <IndexNumberModal {...editing} onClose={() => setEditing(null)} />}
    </Space>
  );
}

function IndexNumberModal({ kind, id, label, current, digits, onClose, districts, districtId }: {
  kind: 'provinces' | 'municipalities' | 'barangays'; id: string; label: string; current: string | null; digits: string; onClose: () => void;
  districts?: CityDistrictDto[]; districtId?: string | null;
}) {
  const [value, setValue] = useState(current ?? '');
  const [district, setDistrict] = useState<string | null>(districtId ?? null);
  const [reason, setReason] = useState('');
  const save = useSetIndexNumber();
  const needsReason = current !== null && value.trim() !== current;
  return (
    <Modal open title={`Index number — ${label}`} okText="Save" onCancel={onClose} confirmLoading={save.isPending}
      okButtonProps={{ disabled: needsReason && reason.trim() === '' }}
      onOk={() => save.mutate({ kind, id, indexNumber: value.trim() || null, reason: reason.trim() || null, cityDistrictId: district }, { onSuccess: onClose })}>
      {save.isError && <Alert type="error" showIcon title="Not saved" description={errorText(save.error)} style={{ marginBottom: 12 }} />}
      <Space orientation="vertical" style={{ width: '100%' }}>
        <Typography.Text type="secondary">{digits}</Typography.Text>
        <Input aria-label="Index number" value={value} maxLength={4} onChange={(e) => setValue(e.target.value)} />
        {districts && districts.length > 0 && (
          <Select aria-label="District" placeholder="District" allowClear value={district ?? undefined} style={{ width: '100%' }}
            options={districts.map((d) => ({ value: d.id, label: `${d.indexNumber} — ${d.name}` }))} onChange={(v) => setDistrict(v ?? null)} />
        )}
        {needsReason && (
          <Input.TextArea aria-label="Reason for the change" placeholder="Reason for changing a number already set (required)" rows={2} maxLength={1000}
            value={reason} onChange={(e) => setReason(e.target.value)} />
        )}
      </Space>
    </Modal>
  );
}

function DistrictsTab({ municipalityId }: { municipalityId: string }) {
  const { data = [], isLoading } = useDistricts(municipalityId);
  const create = useCreateDistrict();
  const [name, setName] = useState('');
  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Typography.Text type="secondary">
        City districts are numbered from "01" at the upper-left district in an inverted "S". A district's number gives the PIN's 4th–5th digits.
      </Typography.Text>
      <Space wrap>
        <Input aria-label="New district name" placeholder="District name" value={name} onChange={(e) => setName(e.target.value)} style={{ width: 260 }} />
        <Button icon={<PlusOutlined />} disabled={name.trim() === ''} loading={create.isPending}
          onClick={() => create.mutate({ municipalityId, indexNumber: null, name: name.trim() }, { onSuccess: () => setName('') })}>
          Add district (next number)
        </Button>
      </Space>
      {create.isError && <Alert type="error" showIcon title="Not added" description={errorText(create.error)} />}
      <Table<CityDistrictDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false}
        locale={{ emptyText: <Empty description="No districts (a municipality in a province has none)" /> }}
        columns={[{ title: 'Index No.', dataIndex: 'indexNumber' }, { title: 'Name', dataIndex: 'name' }]} />
    </Space>
  );
}

function BarangaysTab({ municipalityId }: { municipalityId: string }) {
  const { data = [], isLoading } = useIndexBarangays(municipalityId);
  const districts = useDistricts(municipalityId);
  const [editing, setEditing] = useState<BarangayIndexDto | null>(null);
  const [splitting, setSplitting] = useState<BarangayIndexDto | null>(null);
  const [selected, setSelected] = useState<BarangayIndexDto | null>(null);

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Typography.Text type="secondary">
        Barangays are numbered from "0001" for the Poblacion, then alphabetically. Select a barangay to manage its tax map sections.
      </Typography.Text>
      <Table<BarangayIndexDto>
        rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={{ pageSize: 50, hideOnSinglePage: true }} scroll={{ x: 'max-content' }}
        rowClassName={(b) => (b.id === selected?.id ? 'ant-table-row-selected' : '')}
        columns={[
          { title: 'District', dataIndex: 'cityDistrictIndexNumber', render: (v: string | null) => v ?? '—' },
          { title: 'Index No.', dataIndex: 'pinIndexNumber', render: index },
          { title: 'Barangay', dataIndex: 'name' },
          { title: 'Sections', dataIndex: 'sectionCount', align: 'right' },
          { title: 'Status', key: 'status', render: (_, b) => (b.retiredOn ? <Tag color="orange">Retired {b.retiredOn}</Tag> : b.splitFromBarangayId ? <Tag color="blue">From a division</Tag> : null) },
          {
            title: '', key: 'actions', render: (_, b) => (
              <Space>
                <Button size="small" onClick={() => setSelected(b)}>Sections</Button>
                {!b.retiredOn && <Button size="small" icon={<EditOutlined />} onClick={() => setEditing(b)}>Number</Button>}
                {!b.retiredOn && b.pinIndexNumber && <Button size="small" onClick={() => setSplitting(b)}>Divide</Button>}
              </Space>
            ),
          },
        ]}
      />
      {selected && <SectionsPanel barangay={selected} />}
      {editing && (
        <IndexNumberModal kind="barangays" id={editing.id} label={`Barangay ${editing.name}`} current={editing.pinIndexNumber} digits="4 digits"
          districts={districts.data} districtId={editing.cityDistrictId} onClose={() => setEditing(null)} />
      )}
      {splitting && <SplitModal barangay={splitting} onClose={() => setSplitting(null)} />}
    </Space>
  );
}

/** A barangay divided into new ones: its number is retired and the new barangays take the next numbers (MRPAAO Ch. II §1 D.5). */
function SplitModal({ barangay, onClose }: { barangay: BarangayIndexDto; onClose: () => void }) {
  const [reason, setReason] = useState('');
  const [date, setDate] = useState<Dayjs>(dayjs());
  const [rows, setRows] = useState([{ name: '', psgcCode: '' }, { name: '', psgcCode: '' }]);
  const split = useSplitBarangay();
  const ready = reason.trim() !== '' && rows.length >= 2 && rows.every((r) => r.name.trim() && r.psgcCode.trim());
  return (
    <Modal open width={620} title={`Divide barangay ${barangay.name} (${barangay.pinIndexNumber})`} okText="Divide" okButtonProps={{ danger: true, disabled: !ready }}
      confirmLoading={split.isPending} onCancel={onClose}
      onOk={() => split.mutate({ id: barangay.id, reason: reason.trim(), effectiveDate: date.format('YYYY-MM-DD'),
        successors: rows.map((r) => ({ name: r.name.trim(), psgcCode: r.psgcCode.trim() })) }, { onSuccess: onClose })}>
      {split.isError && <Alert type="error" showIcon title="Not divided" description={errorText(split.error)} style={{ marginBottom: 12 }} />}
      <Space orientation="vertical" style={{ width: '100%' }}>
        <Typography.Text>Number {barangay.pinIndexNumber} is retired; the new barangays take the next numbers after the highest ever used, in this order.</Typography.Text>
        {rows.map((r, i) => (
          <Space key={i} wrap>
            <Input aria-label={`New barangay ${i + 1} name`} placeholder="Name" value={r.name} style={{ width: 260 }}
              onChange={(e) => setRows((rs) => rs.map((x, j) => (j === i ? { ...x, name: e.target.value } : x)))} />
            <Input aria-label={`New barangay ${i + 1} PSGC code`} placeholder="PSGC code" value={r.psgcCode} style={{ width: 180 }}
              onChange={(e) => setRows((rs) => rs.map((x, j) => (j === i ? { ...x, psgcCode: e.target.value } : x)))} />
          </Space>
        ))}
        <Button size="small" icon={<PlusOutlined />} onClick={() => setRows((rs) => [...rs, { name: '', psgcCode: '' }])}>Another barangay</Button>
        <DatePicker aria-label="Effective date" value={date} allowClear={false} onChange={(d) => d && setDate(d)} />
        <Input.TextArea aria-label="Reason" placeholder="Reason, e.g. the ordinance creating the barangays (required)" rows={2} maxLength={1000}
          value={reason} onChange={(e) => setReason(e.target.value)} />
      </Space>
    </Modal>
  );
}

function SectionsPanel({ barangay }: { barangay: BarangayIndexDto }) {
  const { data = [], isLoading } = useSections(barangay.id);
  const create = useCreateSection();
  const retire = useRetireSection();
  const [retiring, setRetiring] = useState<TaxMapSectionDto | null>(null);
  const [reason, setReason] = useState('');
  return (
    <Card size="small" title={`Tax map sections — ${barangay.name}${barangay.pinIndexNumber ? ` (${barangay.pinIndexNumber})` : ''}`}>
      <Space orientation="vertical" style={{ width: '100%' }}>
        <Typography.Text type="secondary">Sections are numbered from "001" at the upper-left section in an inverted "S" (the PIN's 10th–12th digits).</Typography.Text>
        {!barangay.retiredOn && (
          <Button icon={<PlusOutlined />} loading={create.isPending} onClick={() => create.mutate({ barangayId: barangay.id, indexNumber: null, remarks: null, splitFromSectionId: null })}>
            Add section (next number)
          </Button>
        )}
        {create.isError && <Alert type="error" showIcon title="Not added" description={errorText(create.error)} />}
        <Table<TaxMapSectionDto> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false}
          locale={{ emptyText: <Empty description="No sections yet" /> }}
          columns={[
            { title: 'Section No.', dataIndex: 'indexNumber' },
            { title: 'Remarks', dataIndex: 'remarks', render: (v: string | null) => v ?? '—' },
            { title: 'Status', key: 'status', render: (_, x) => (x.retiredOn ? <Tag color="orange">Retired {x.retiredOn}</Tag> : <Tag color="green">Active</Tag>) },
            { title: '', key: 'retire', render: (_, x) => !x.retiredOn && <Button size="small" danger onClick={() => { setReason(''); retire.reset(); setRetiring(x); }}>Retire</Button> },
          ]} />
      </Space>
      <Modal open={retiring !== null} title={`Retire section ${retiring?.indexNumber}`} okText="Retire" okButtonProps={{ danger: true, disabled: reason.trim() === '' }}
        confirmLoading={retire.isPending} onCancel={() => setRetiring(null)}
        onOk={() => retiring && retire.mutate({ id: retiring.id, reason: reason.trim(), effectiveDate: dayjs().format('YYYY-MM-DD') }, { onSuccess: () => setRetiring(null) })}>
        {retire.isError && <Alert type="error" showIcon title="Not retired" description={errorText(retire.error)} style={{ marginBottom: 12 }} />}
        <Typography.Paragraph>The number stays reserved and is never given to another section.</Typography.Paragraph>
        <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={2} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Modal>
    </Card>
  );
}
