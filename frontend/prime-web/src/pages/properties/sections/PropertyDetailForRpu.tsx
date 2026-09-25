import { useState } from 'react';
import { Button, Descriptions, Empty, Table, Tag } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import type { MachineryDto, RpuSummaryDto } from '../../../lib/types';
import { useLandByRpu } from '../../../api/land';
import { useBuildingByRpu } from '../../../api/buildings';
import { useMachineryUnitsByRpu } from '../../../api/machinery';
import { AddLandModal } from '../modals/AddLandModal';
import { AddBuildingModal } from '../modals/AddBuildingModal';
import { AddMachineryModal } from '../modals/AddMachineryModal';
import { ApiRequestError } from '../../../lib/apiClient';
import { LandAppraisalRows } from './LandAppraisalRows';
import { BuildingAppraisalRows } from './BuildingAppraisalRows';
import { MachineryDescriptionModal } from '../modals/DescriptionModals';
import { formatMoney } from '../../../lib/format';

// A LAND_NOT_FOUND/BUILDING_NOT_FOUND/MACHINERY_NOT_FOUND response (mapped
// to HTTP 404 by ApiControllerBase) means "not registered yet" here, since
// each of these is 1:1 with its RPU — not a real error.
function isNotFound(error: unknown): boolean {
  return error instanceof ApiRequestError && error.status === 404;
}

function LandDetail({ propertyId, rpuId }: { propertyId: string; rpuId: string }) {
  const { data, isLoading, isError, error } = useLandByRpu(rpuId);
  const [addOpen, setAddOpen] = useState(false);

  if (isLoading) {
    return null;
  }

  if (isError && isNotFound(error)) {
    return (
      <div style={{ marginBottom: 8 }}>
        <Button size="small" icon={<PlusOutlined />} onClick={() => setAddOpen(true)}>
          Add Land
        </Button>
        <AddLandModal propertyId={propertyId} rpuId={rpuId} open={addOpen} onClose={() => setAddOpen(false)} />
      </div>
    );
  }

  if (isError || !data) {
    return <Empty description="Could not load Land details" image={Empty.PRESENTED_IMAGE_SIMPLE} />;
  }

  return (
    <>
    <Descriptions size="small" column={2} bordered style={{ marginBottom: 8 }}>
      <Descriptions.Item label="Status">
        <Tag>{data.status}</Tag>
      </Descriptions.Item>
      <Descriptions.Item label="Area">
        {data.area} {data.areaUnit}
      </Descriptions.Item>
      <Descriptions.Item label="Classification">{data.classificationName}</Descriptions.Item>
      <Descriptions.Item label="Actual Use">{data.actualUseName}</Descriptions.Item>
      <Descriptions.Item label="Corner Lot">{data.isCornerLot ? 'Yes' : 'No'}</Descriptions.Item>
      <Descriptions.Item label="Zoning">{data.zoning ?? '—'}</Descriptions.Item>
      <Descriptions.Item label="Market Value">{data.marketValue ?? 'Not yet valued'}</Descriptions.Item>
      <Descriptions.Item label="Assessed Value">{data.assessedValue ?? 'Not yet assessed'}</Descriptions.Item>
    </Descriptions>
    <LandAppraisalRows land={data} rpuId={rpuId} propertyId={propertyId} />
    </>
  );
}

function BuildingDetail({ propertyId, rpuId }: { propertyId: string; rpuId: string }) {
  const { data, isLoading, isError, error } = useBuildingByRpu(rpuId);
  const [addOpen, setAddOpen] = useState(false);

  if (isLoading) {
    return null;
  }

  if (isError && isNotFound(error)) {
    return (
      <div style={{ marginBottom: 8 }}>
        <Button size="small" icon={<PlusOutlined />} onClick={() => setAddOpen(true)}>
          Add Building
        </Button>
        <AddBuildingModal propertyId={propertyId} rpuId={rpuId} open={addOpen} onClose={() => setAddOpen(false)} />
      </div>
    );
  }

  if (isError || !data) {
    return <Empty description="Could not load Building details" image={Empty.PRESENTED_IMAGE_SIMPLE} />;
  }

  return (
    <>
    <Descriptions size="small" column={2} bordered style={{ marginBottom: 8 }}>
      <Descriptions.Item label="Status">
        <Tag>{data.status}</Tag>
      </Descriptions.Item>
      <Descriptions.Item label="Building Type">{data.buildingTypeName}</Descriptions.Item>
      <Descriptions.Item label="Structural Type">{data.structuralTypeName}</Descriptions.Item>
      <Descriptions.Item label="Actual Use">{data.actualUseName}</Descriptions.Item>
      <Descriptions.Item label="Condition">{data.conditionName}</Descriptions.Item>
      <Descriptions.Item label="Storeys">{data.numberOfStoreys}</Descriptions.Item>
      <Descriptions.Item label="Floor Area">{data.floorArea} sqm</Descriptions.Item>
      <Descriptions.Item label="Total Floor Area">{data.totalFloorArea} sqm</Descriptions.Item>
      <Descriptions.Item label="Year Constructed">{data.yearConstructed ?? '—'}</Descriptions.Item>
      <Descriptions.Item label="Completion">{data.completionPercentage}%</Descriptions.Item>
      <Descriptions.Item label="Market Value">{data.marketValue ?? 'Not yet valued'}</Descriptions.Item>
      <Descriptions.Item label="Assessed Value">{data.assessedValue ?? 'Not yet assessed'}</Descriptions.Item>
    </Descriptions>
    <BuildingAppraisalRows building={data} rpuId={rpuId} propertyId={propertyId} />
    </>
  );
}

/** The machines of a machinery RPU — one row each on its FAAS (MRPAAO Att. 3). */
function MachineryDetail({ propertyId, rpuId }: { propertyId: string; rpuId: string }) {
  const { data = [], isLoading, isError } = useMachineryUnitsByRpu(rpuId);
  const [addOpen, setAddOpen] = useState(false);
  const [editing, setEditing] = useState<MachineryDto | null>(null);

  if (isError) {
    return <Empty description="Could not load the machinery" image={Empty.PRESENTED_IMAGE_SIMPLE} />;
  }

  return (
    <div style={{ marginBottom: 8 }}>
      <Button size="small" icon={<PlusOutlined />} onClick={() => setAddOpen(true)} style={{ marginBottom: 6 }}>
        Add machine
      </Button>
      <Table<MachineryDto> size="small" rowKey="id" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No machines recorded' }}
        columns={[
          { title: 'Type', dataIndex: 'machineryTypeName' },
          { title: 'Brand / model / serial', render: (_, m) => [m.brand, m.model, m.serialNumber].map((v) => v ?? '—').join(' / ') },
          { title: 'Acquired', dataIndex: 'dateAcquired', render: (v: string | null) => v ?? '—' },
          { title: 'Condition', dataIndex: 'isBrandNew', render: (v: boolean) => (v ? 'Brand new' : 'Not brand new') },
          { title: 'Economic / remaining life', render: (_, m) => `${m.economicLifeYears ?? '—'} / ${m.remainingLifeYears ?? '—'} yrs` },
          { title: 'Assessed under', render: (_, m) => (m.actualUseName ? `${m.classificationName ?? ''} / ${m.actualUseName}` : 'Tax Declaration’s use') },
          { title: 'Installed / operating since', render: (_, m) => `${m.yearInstalled ?? '—'} / ${m.yearOfInitialOperation ?? '—'}` },
          { title: 'Market value', dataIndex: 'marketValue', align: 'right', render: (v: number | null) => (v === null ? 'Not yet valued' : formatMoney(v)) },
          { title: '', render: (_, m) => <Button size="small" onClick={() => setEditing(m)}>Edit</Button> },
        ]} />
      {editing && <MachineryDescriptionModal machine={editing} rpuId={rpuId} onClose={() => setEditing(null)} />}
      <AddMachineryModal propertyId={propertyId} rpuId={rpuId} open={addOpen} onClose={() => setAddOpen(false)} />
    </div>
  );
}

// RpuType 'OtherImprovement' has no matching backend entity yet (CLAUDE.md
// §22 defines the type, but no Application service was ever built for it —
// not inventing one here). Land/Building/Machinery are each 1:1 with their
// owning RPU (Phase 6's unique-RpuId-index change), so this dispatch by
// rpu.rpuType is exhaustive for what actually exists server-side.
export function PropertyDetailForRpu({ propertyId, rpu }: { propertyId: string; rpu: RpuSummaryDto }) {
  if (rpu.rpuType === 'Land') {
    return <LandDetail propertyId={propertyId} rpuId={rpu.id} />;
  }
  if (rpu.rpuType === 'Building') {
    return <BuildingDetail propertyId={propertyId} rpuId={rpu.id} />;
  }
  if (rpu.rpuType === 'Machinery') {
    return <MachineryDetail propertyId={propertyId} rpuId={rpu.id} />;
  }
  return null;
}
