import { useState } from 'react';
import { Alert, Button, Descriptions, Empty, Input, InputNumber, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import { CheckOutlined } from '@ant-design/icons';
import {
  type PinAssignmentDto, type PinKind, type TieUpStage, usePlaceInSection, usePropertyPin, useRecordTieUp, useSections,
} from '../../../api/propertyIdentification';
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
  const [tieUp, setTieUp] = useState<{ stage: TieUpStage; withdraw: boolean } | null>(null);

  if (isLoading) return null;
  if (isError || !data) return <Alert type="error" showIcon title="Could not load the PIN" description={errorText(error)} />;
  // A subdivided or consolidated property keeps its PIN history but has no current PIN (step 10a-3).
  const retired = data.kind === null && data.history.length > 0;
  const temporary = data.kind === 'Temporary' ? data.history.find((h) => h.kind === 'Temporary' && !h.retiredAt) : undefined;

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

      {temporary && (
        <Descriptions size="small" bordered column={1} title="Tax mapping tie-up (pre-TMCR check marks)">
          <Descriptions.Item label="Office: FAAS tied to its parcel on the base map">
            <TieUpMark at={temporary.officeTieUpAt}
              onRecord={() => setTieUp({ stage: 'Office', withdraw: false })}
              onWithdraw={temporary.fieldConfirmedAt ? undefined : () => setTieUp({ stage: 'Office', withdraw: true })} />
          </Descriptions.Item>
          <Descriptions.Item label="Field: confirmed with the owner">
            <TieUpMark at={temporary.fieldConfirmedAt} disabled={!temporary.officeTieUpAt}
              onRecord={() => setTieUp({ stage: 'Field', withdraw: false })}
              onWithdraw={() => setTieUp({ stage: 'Field', withdraw: true })} />
          </Descriptions.Item>
          {temporary.tieUpRemarks && <Descriptions.Item label="Remarks">{temporary.tieUpRemarks}</Descriptions.Item>}
        </Descriptions>
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
          {
            title: 'Tie-up', key: 'tieUp',
            render: (_, h) => (h.kind === 'Temporary' ? `${h.officeTieUpAt ? '✓ office' : '— office'} · ${h.fieldConfirmedAt ? '✓ field' : '— field'}` : ''),
          },
        ]}
      />
      {tieUp && <TieUpModal propertyId={propertyId} {...tieUp} onClose={() => setTieUp(null)} />}
      {placing && <PlaceModal propertyId={propertyId} parcels={parcels.filter((p) => p.status === 'Active')} onClose={() => setPlacing(false)} />}
    </Space>
  );
}

function TieUpMark({ at, disabled, onRecord, onWithdraw }: { at: string | null; disabled?: boolean; onRecord: () => void; onWithdraw?: () => void }) {
  return at ? (
    <Space>
      <Tag color="green" icon={<CheckOutlined />}>{new Date(at).toLocaleString('en-PH', { dateStyle: 'medium', timeStyle: 'short' })}</Tag>
      {onWithdraw && <Button size="small" onClick={onWithdraw}>Withdraw</Button>}
    </Space>
  ) : (
    <Button size="small" disabled={disabled} onClick={onRecord}>Record</Button>
  );
}

function TieUpModal({ propertyId, stage, withdraw, onClose }: { propertyId: string; stage: TieUpStage; withdraw: boolean; onClose: () => void }) {
  const [remarks, setRemarks] = useState('');
  const record = useRecordTieUp(propertyId);
  const what = stage === 'Office' ? 'office tie-up' : 'field confirmation';
  return (
    <Modal open title={`${withdraw ? 'Withdraw' : 'Record'} the ${what}`} okText={withdraw ? 'Withdraw' : 'Record'} onCancel={onClose}
      okButtonProps={{ danger: withdraw, disabled: withdraw && remarks.trim() === '' }} confirmLoading={record.isPending}
      onOk={() => record.mutate({ stage, withdraw, remarks: remarks.trim() || null }, { onSuccess: onClose })}>
      {record.isError && <Alert type="error" showIcon title="Not recorded" description={errorText(record.error)} style={{ marginBottom: 12 }} />}
      <Input.TextArea aria-label="Remarks" rows={2} maxLength={1000} value={remarks} onChange={(e) => setRemarks(e.target.value)}
        placeholder={withdraw ? 'Reason (required)' : 'Remarks (optional), e.g. the base map lot'} />
    </Modal>
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
