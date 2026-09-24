import { useState } from 'react';
import { Alert, Button, DatePicker, Empty, Input, Modal, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useEndParty } from '../../../api/taxpayers';
import { ApiRequestError } from '../../../lib/apiClient';
import { partyRoleLabel, type PropertyOwnerDto, type PropertyPartyRole } from '../../../lib/types';
import { AddOwnerModal } from '../modals/AddOwnerModal';

/**
 * Parties in whose name the property is declared — owners, administrators,
 * beneficial users, claimants, or an unknown owner (LGC §§204–205). History
 * is kept: ending a party records the date and reason.
 */
export function OwnersSection({ propertyId, owners }: { propertyId: string; owners: PropertyOwnerDto[] }) {
  const [addOpen, setAddOpen] = useState(false);
  const [ending, setEnding] = useState<PropertyOwnerDto | null>(null);
  const [endDate, setEndDate] = useState(dayjs());
  const [reason, setReason] = useState('');
  const endParty = useEndParty(propertyId);
  const currentTotal = owners.filter((o) => o.isCurrent && o.role === 'Owner').reduce((sum, o) => sum + o.ownershipPercentage, 0);

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12, gap: 8, flexWrap: 'wrap' }}>
        <Typography.Title level={5} style={{ margin: 0 }}>
          Owners &amp; parties {owners.length > 0 && <Typography.Text type="secondary">({currentTotal}% ownership currently held)</Typography.Text>}
        </Typography.Title>
        <Button icon={<PlusOutlined />} onClick={() => setAddOpen(true)}>
          Add Party
        </Button>
      </div>

      <Table<PropertyOwnerDto>
        rowKey="propertyTaxpayerId"
        dataSource={owners}
        pagination={false}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: <Empty description="No owners registered yet" /> }}
        columns={[
          { title: 'Name', dataIndex: 'taxpayerDisplayName' },
          { title: 'Capacity', dataIndex: 'role', render: (r: PropertyPartyRole) => <Tag color={r === 'UnknownOwner' ? 'orange' : r === 'Owner' ? 'blue' : 'purple'}>{partyRoleLabel[r]}</Tag> },
          { title: 'Ownership Type', dataIndex: 'ownershipTypeName', render: (v: string | null) => v ?? '—' },
          { title: 'Share', render: (_, o) => (o.role === 'Owner' ? `${o.ownershipPercentage}%` : '—') },
          { title: 'Start Date', dataIndex: 'startDate' },
          { title: 'End Date', render: (_, o) => (o.endDate ? `${o.endDate}${o.endReason ? ` — ${o.endReason}` : ''}` : '—') },
          {
            title: 'Current',
            dataIndex: 'isCurrent',
            render: (isCurrent: boolean) => (isCurrent ? <Tag color="green">Current</Tag> : <Tag>Historical</Tag>),
          },
          {
            title: '',
            render: (_, o) => o.isCurrent && (
              <Button size="small" onClick={() => { setEnding(o); setEndDate(dayjs()); setReason(''); endParty.reset(); }}>End</Button>
            ),
          },
        ]}
      />

      <AddOwnerModal propertyId={propertyId} open={addOpen} onClose={() => setAddOpen(false)} />

      <Modal
        title={ending ? `End ${partyRoleLabel[ending.role].toLowerCase()} — ${ending.taxpayerDisplayName}` : ''}
        open={ending !== null}
        okText="End"
        okButtonProps={{ disabled: reason.trim() === '', loading: endParty.isPending }}
        onCancel={() => setEnding(null)}
        onOk={() => ending && endParty.mutate(
          { id: ending.propertyTaxpayerId, endDate: endDate.format('YYYY-MM-DD'), reason: reason.trim() },
          { onSuccess: () => setEnding(null) },
        )}
        destroyOnHidden
      >
        {endParty.isError && (
          <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not end"
            description={endParty.error instanceof ApiRequestError ? endParty.error.apiError.message : (endParty.error as Error).message} />
        )}
        <Typography.Paragraph>The record stays in the ownership history. Transfers between owners come with property transactions.</Typography.Paragraph>
        <DatePicker aria-label="End date" value={endDate} onChange={(d) => d && setEndDate(d)} style={{ marginBottom: 8 }} />
        <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Modal>
    </div>
  );
}
