import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Button, Card, Descriptions, Skeleton, Tabs, Tag, Typography } from 'antd';
import { GlobalOutlined } from '@ant-design/icons';
import { usePropertyProfile } from '../../api/properties';
import { OwnersSection } from './sections/OwnersSection';
import { ParcelsSection } from './sections/ParcelsSection';
import { RpuSection } from './sections/RpuSection';
import { BillingSection } from './sections/BillingSection';

/**
 * CLAUDE.md §50 Property Profile — "one of the most important screens in
 * PRIME." Sections shown here (Basic Info, Owners, Parcels, RPUs/Tax
 * Declarations) are the ones with real data as of Phase 4; the GIS map is
 * reached via "View on map" (the Phase 7 GIS workspace); Billing is the
 * Phase 8 tab. Current assessment, payments, and delinquency sections are not
 * rendered yet — those phases haven't landed, and a placeholder claiming
 * data that doesn't exist would be worse than omitting the section.
 */
export function PropertyProfilePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: profile, isLoading, isError, error } = usePropertyProfile(id);

  if (isLoading) {
    return <Skeleton active />;
  }

  if (isError || !profile) {
    return <Alert type="error" showIcon title="Could not load property" description={(error as Error)?.message ?? 'Not found'} />;
  }

  const { property } = profile;

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap', marginBottom: 16 }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          {property.propertyIdentificationNumber}
        </Typography.Title>
        <Button icon={<GlobalOutlined />} onClick={() => navigate(`/gis?propertyId=${property.id}`)}>
          View on map
        </Button>
      </div>

      <Card style={{ marginBottom: 24 }}>
        <Descriptions title="Basic Information" bordered column={2} size="small">
          <Descriptions.Item label="Status">
            <Tag>{property.status}</Tag>
          </Descriptions.Item>
          <Descriptions.Item label="PIN">{property.propertyIdentificationNumber}</Descriptions.Item>
          <Descriptions.Item label="Province">{property.provinceName}</Descriptions.Item>
          <Descriptions.Item label="Municipality">{property.municipalityName}</Descriptions.Item>
          <Descriptions.Item label="Barangay">{property.barangayName}</Descriptions.Item>
          <Descriptions.Item label="Zone">{property.zoneName ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Street">{property.street ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Sitio">{property.sitio ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Lot Number">{property.lotNumber ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Block Number">{property.blockNumber ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Survey Number">{property.surveyNumber ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Title Number">{property.titleNumber ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Tax Map Number">{property.taxMapNumber ?? '—'}</Descriptions.Item>
        </Descriptions>
      </Card>

      <Card>
        <Tabs
          items={[
            { key: 'owners', label: `Owners (${profile.owners.length})`, children: <OwnersSection propertyId={property.id} owners={profile.owners} /> },
            { key: 'parcels', label: `Parcels (${profile.parcels.length})`, children: <ParcelsSection propertyId={property.id} parcels={profile.parcels} /> },
            { key: 'rpus', label: `RPUs (${profile.rpus.length})`, children: <RpuSection propertyId={property.id} rpus={profile.rpus} /> },
            { key: 'billing', label: 'Billing', children: <BillingSection propertyId={property.id} rpus={profile.rpus} /> },
          ]}
        />
      </Card>

      <Alert
        style={{ marginTop: 24 }}
        type="info"
        showIcon
        title="Current assessment, payments, and delinquency sections are not shown yet"
        description="Assessments exist in the API (Phase 6) but have no screen yet; payments arrive with Phase 9 (Collection) and delinquency with Phase 10."
      />
    </div>
  );
}
