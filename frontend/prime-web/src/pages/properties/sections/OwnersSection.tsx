import { useState } from 'react';
import { Button, Empty, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import type { PropertyOwnerDto } from '../../../lib/types';
import { AddOwnerModal } from '../modals/AddOwnerModal';

export function OwnersSection({ propertyId, owners }: { propertyId: string; owners: PropertyOwnerDto[] }) {
  const [addOpen, setAddOpen] = useState(false);
  const currentTotal = owners.filter((o) => o.isCurrent).reduce((sum, o) => sum + o.ownershipPercentage, 0);

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <Typography.Title level={5} style={{ margin: 0 }}>
          Owners {owners.length > 0 && <Typography.Text type="secondary">({currentTotal}% currently held)</Typography.Text>}
        </Typography.Title>
        <Button icon={<PlusOutlined />} onClick={() => setAddOpen(true)} disabled={currentTotal >= 100}>
          Add Owner
        </Button>
      </div>

      <Table<PropertyOwnerDto>
        rowKey="propertyTaxpayerId"
        dataSource={owners}
        pagination={false}
        locale={{ emptyText: <Empty description="No owners registered yet" /> }}
        columns={[
          { title: 'Taxpayer', dataIndex: 'taxpayerDisplayName' },
          { title: 'Ownership Type', dataIndex: 'ownershipTypeName' },
          { title: 'Share', dataIndex: 'ownershipPercentage', render: (v: number) => `${v}%` },
          { title: 'Start Date', dataIndex: 'startDate' },
          { title: 'End Date', dataIndex: 'endDate', render: (v: string | null) => v ?? '—' },
          {
            title: 'Current',
            dataIndex: 'isCurrent',
            render: (isCurrent: boolean) => (isCurrent ? <Tag color="green">Current</Tag> : <Tag>Historical</Tag>),
          },
        ]}
      />

      <AddOwnerModal propertyId={propertyId} open={addOpen} onClose={() => setAddOpen(false)} />
    </div>
  );
}
