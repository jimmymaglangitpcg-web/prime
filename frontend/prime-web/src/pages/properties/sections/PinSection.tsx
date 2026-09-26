import { useState } from 'react';
import { Alert, Button, Descriptions, Empty, InputNumber, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import { type PinAssignmentDto, type PinKind, usePlaceInSection, usePropertyPin, useSections } from '../../../api/propertyIdentification';
import { ApiRequestError } from '../../../lib/apiClient';
import type { ParcelSummaryDto } from '../../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const kindColor: Record<PinKind, string> = { Registered: 'default', Temporary: 'orange', Permanent: 'green' };

/**
 * The property's PIN (MRPAAO Ch. II §1; docs/analysis/property-identification.md
 * §3.3–§3.4): its parts when permanent, every PIN it has carried, and placing
 * its parcel in a tax map section, which gives the permanent PIN with the next
 * parcel number in the section.
 */
export function PinSection({ propertyId, parcels }: { propertyId: string; parcels: ParcelSummaryDto[] }) {
  const { data, isLoading, isError, error } = usePropertyPin(propertyId);
  const [placing, setPlacing] = useState(false);

  if (isLoading) return null;
  if (isError || !data) return <Alert type="error" showIcon title="Could not load the PIN" description={errorText(error)} />;
  // A subdivided or consolidated property keeps its PIN history but has no current PIN (step 10a-3).
  const retired = data.kind === null && data.history.length > 0;

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <Space>
          <Typography.Title level={4} style={{ margin: 0 }}>{data.pin}</Typography.Title>
          {data.kind && <Tag color={kindColor[data.kind]}>{data.kind}</Tag>}
        </Space>
        {data.kind !== 'Permanent' && !retired && (
          <Button type="primary" disabled={parcels.every((p) => p.status !== 'Active')} onClick={() => setPlacing(true)}>
            Place in tax map section
          </Button>
        )}
      </div>

      {data.kind === 'Permanent' ? (
        <Descriptions size="small" bordered column={{ xs: 1, sm: 5, md: 5, lg: 5, xl: 5, xxl: 5 }} layout="vertical">
          <Descriptions.Item label="Province / city">{data.lguIndex}</Descriptions.Item>
          <Descriptions.Item label="Municipality / district">{data.municipalityIndex}</Descriptions.Item>
          <Descriptions.Item label="Barangay">{data.barangayIndex}</Descriptions.Item>
          <Descriptions.Item label="Section">{data.sectionIndex}</Descriptions.Item>
          <Descriptions.Item label="Parcel">{data.parcelNumber}</Descriptions.Item>
        </Descriptions>
      ) : retired ? (
        <Typography.Text type="secondary">
          This PIN is retired and is never given again. {data.history[data.history.length - 1].retirementReason}
        </Typography.Text>
      ) : (
        <Typography.Text type="secondary">
          {data.kind === 'Temporary' ? 'A temporary PIN, until the parcel is tax-mapped.' : 'The PIN given at registration.'} The permanent PIN comes
          from the tax map section and parcel number.
        </Typography.Text>
      )}

      <Table<PinAssignmentDto>
        rowKey="id"
        size="small"
        dataSource={data.history}
        pagination={false}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: <Empty description="No PIN history" /> }}
        columns={[
          { title: 'PIN', dataIndex: 'pin' },
          { title: 'Kind', dataIndex: 'kind', render: (v: PinKind) => <Tag color={kindColor[v]}>{v}</Tag> },
          {
            title: 'Given', key: 'given',
            render: (_, h) => `${new Date(h.assignedAt).toLocaleDateString('en-PH')}${h.assignedByTransactionId ? ' — by property transaction' : ''}`,
          },
          { title: 'Retired', key: 'retired', render: (_, h) => (h.retiredAt ? `${new Date(h.retiredAt).toLocaleDateString('en-PH')} — ${h.retirementReason}` : 'Current') },
        ]}
      />
      {placing && <PlaceModal propertyId={propertyId} parcels={parcels.filter((p) => p.status === 'Active')} onClose={() => setPlacing(false)} />}
    </Space>
  );
}

function PlaceModal({ propertyId, parcels, onClose }: { propertyId: string; parcels: ParcelSummaryDto[]; onClose: () => void }) {
  const [parcelId, setParcelId] = useState<string | undefined>(parcels.length === 1 ? parcels[0].id : undefined);
  const parcel = parcels.find((p) => p.id === parcelId);
  const sections = useSections(parcel?.barangayId);
  const [sectionId, setSectionId] = useState<string>();
  const [parcelNumber, setParcelNumber] = useState<number | null>(null);
  const place = usePlaceInSection(propertyId);
  return (
    <Modal open title="Place the parcel in a tax map section" okText="Give permanent PIN" onCancel={onClose} confirmLoading={place.isPending}
      okButtonProps={{ disabled: !parcelId || !sectionId }}
      onOk={() => parcelId && sectionId && place.mutate({ parcelId, sectionId, parcelNumber }, { onSuccess: onClose })}>
      {place.isError && <Alert type="error" showIcon title="No PIN was given" description={errorText(place.error)} style={{ marginBottom: 12 }} />}
      <Space orientation="vertical" style={{ width: '100%' }}>
        <Select aria-label="Parcel" placeholder="Parcel" value={parcelId} style={{ width: '100%' }}
          options={parcels.map((p) => ({ value: p.id, label: `${p.lotNumber ? `Lot ${p.lotNumber}` : 'Parcel'} — ${p.barangayName}${p.area ? `, ${p.area} sqm` : ''}` }))}
          onChange={(v) => { setParcelId(v); setSectionId(undefined); }} />
        <Select aria-label="Section" placeholder="Tax map section" value={sectionId} style={{ width: '100%' }} disabled={!parcel}
          options={(sections.data ?? []).filter((s) => !s.retiredOn).map((s) => ({ value: s.id, label: `Section ${s.indexNumber}${s.remarks ? ` — ${s.remarks}` : ''}` }))}
          notFoundContent="No active sections in this barangay (add them under Property Identification)" onChange={setSectionId} />
        <InputNumber<number> aria-label="Parcel number (migration only)" placeholder="next number" min={1} precision={0} style={{ width: '100%' }}
          value={parcelNumber} onChange={setParcelNumber} />
        <Typography.Text type="secondary">
          Leave the parcel number empty to take the next number in the section. Enter one only when carrying over an existing tax map; numbers are never reused.
        </Typography.Text>
      </Space>
    </Modal>
  );
}
