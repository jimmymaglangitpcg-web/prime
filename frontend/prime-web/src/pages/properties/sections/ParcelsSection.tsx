import { useState } from 'react';
import { Button, Empty, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import type { ParcelSummaryDto } from '../../../lib/types';
import { AddParcelModal } from '../modals/AddParcelModal';

export function ParcelsSection({ propertyId, parcels }: { propertyId: string; parcels: ParcelSummaryDto[] }) {
  const [addOpen, setAddOpen] = useState(false);

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <Typography.Title level={5} style={{ margin: 0 }}>
          Parcels
        </Typography.Title>
        <Button icon={<PlusOutlined />} onClick={() => setAddOpen(true)}>
          Add Parcel
        </Button>
      </div>

      <Table<ParcelSummaryDto>
        rowKey="id"
        dataSource={parcels}
        pagination={false}
        locale={{ emptyText: <Empty description="No parcels registered yet" /> }}
        columns={[
          { title: 'Lot Number', dataIndex: 'lotNumber' },
          { title: 'Barangay', dataIndex: 'barangayName' },
          { title: 'Area (sqm)', dataIndex: 'area' },
          { title: 'Status', dataIndex: 'status', render: (v: string) => <Tag>{v}</Tag> },
        ]}
      />

      <AddParcelModal propertyId={propertyId} open={addOpen} onClose={() => setAddOpen(false)} />
    </div>
  );
}
