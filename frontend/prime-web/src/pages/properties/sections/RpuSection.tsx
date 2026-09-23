import { useState } from 'react';
import { Button, Empty, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import type { RpuSummaryDto } from '../../../lib/types';
import { useTaxDeclarationsByRpu } from '../../../api/taxDeclarations';
import { AddRpuModal } from '../modals/AddRpuModal';
import { AddTaxDeclarationModal } from '../modals/AddTaxDeclarationModal';

const workflowStatusColor: Record<string, string> = {
  Draft: 'default',
  Submitted: 'blue',
  PendingReview: 'gold',
  Approved: 'green',
  Rejected: 'red',
  Posted: 'green',
  Cancelled: 'red',
  Voided: 'red',
};

function TaxDeclarationsForRpu({ propertyId, rpuId }: { propertyId: string; rpuId: string }) {
  const { data, isLoading } = useTaxDeclarationsByRpu(rpuId);
  const [addOpen, setAddOpen] = useState(false);

  return (
    <div style={{ padding: '8px 24px' }}>
      <Button size="small" icon={<PlusOutlined />} onClick={() => setAddOpen(true)} style={{ marginBottom: 8 }}>
        Add Tax Declaration
      </Button>
      <Table
        size="small"
        rowKey="id"
        loading={isLoading}
        dataSource={data ?? []}
        pagination={false}
        locale={{ emptyText: <Empty description="No Tax Declarations yet" image={Empty.PRESENTED_IMAGE_SIMPLE} /> }}
        columns={[
          { title: 'TD Number', dataIndex: 'taxDeclarationNumber' },
          { title: 'Revision', dataIndex: 'revisionNumber', width: 90 },
          { title: 'Assessment Year', dataIndex: 'assessmentYear', width: 130 },
          { title: 'Effectivity', dataIndex: 'effectivityDate' },
          {
            title: 'Status',
            dataIndex: 'status',
            render: (status: string) => <Tag color={workflowStatusColor[status] ?? 'default'}>{status}</Tag>,
          },
        ]}
      />
      <AddTaxDeclarationModal propertyId={propertyId} rpuId={rpuId} open={addOpen} onClose={() => setAddOpen(false)} />
    </div>
  );
}

export function RpuSection({ propertyId, rpus }: { propertyId: string; rpus: RpuSummaryDto[] }) {
  const [addOpen, setAddOpen] = useState(false);

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <Typography.Title level={5} style={{ margin: 0 }}>
          Real Property Units
        </Typography.Title>
        <Button icon={<PlusOutlined />} onClick={() => setAddOpen(true)}>
          Add RPU
        </Button>
      </div>

      <Table<RpuSummaryDto>
        rowKey="id"
        dataSource={rpus}
        pagination={false}
        locale={{ emptyText: <Empty description="No RPUs registered yet" /> }}
        expandable={{
          expandedRowRender: (rpu) => <TaxDeclarationsForRpu propertyId={propertyId} rpuId={rpu.id} />,
        }}
        columns={[
          { title: 'RPU Number', dataIndex: 'rpuNumber' },
          { title: 'Type', dataIndex: 'rpuType' },
          { title: 'Effectivity', dataIndex: 'effectivityDate' },
          {
            title: 'Status',
            dataIndex: 'status',
            render: (status: string) => <Tag>{status}</Tag>,
          },
        ]}
      />

      <AddRpuModal propertyId={propertyId} open={addOpen} onClose={() => setAddOpen(false)} />
    </div>
  );
}
