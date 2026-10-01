import { Alert, Button, Empty, Space, Table, Tag, Typography } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { useApprovalQueue, type ApprovalQueueItemDto } from '../../api/approvals';
import { ApiRequestError } from '../../lib/apiClient';

const kind: Record<ApprovalQueueItemDto['subjectType'], string> = {
  TaxDeclaration: 'Tax Declaration', Assessment: 'Assessment', PropertyTransaction: 'Transaction',
};

/**
 * Awaiting my approval (docs/analysis/province-wide-operation.md §3.4): records pending review in the
 * user's jurisdiction whose next step they may sign now, by the same rules as signing. The approval
 * itself is given on the property's page.
 */
export function ApprovalsPage() {
  const queue = useApprovalQueue();
  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ margin: 0 }}>Awaiting my approval</Typography.Title>
        <Typography.Paragraph type="secondary" style={{ maxWidth: 900, marginBottom: 0 }}>
          Records whose next approval step you may sign today. Municipal offices review their own records; the Provincial Assessor gives final
          approval unless it is delegated to the municipal Assessor.
        </Typography.Paragraph>
      </div>
      <Button icon={<ReloadOutlined />} onClick={() => queue.refetch()} loading={queue.isFetching} style={{ alignSelf: 'flex-start' }}>Refresh</Button>
      {queue.isError && (
        <Alert type="error" showIcon title="Could not load the queue"
          description={queue.error instanceof ApiRequestError ? queue.error.apiError.message : (queue.error as Error).message} />
      )}
      <Table<ApprovalQueueItemDto>
        rowKey="subjectId" size="small" loading={queue.isLoading} dataSource={queue.data ?? []} pagination={{ pageSize: 25 }} scroll={{ x: true }}
        locale={{ emptyText: <Empty description="Nothing is waiting for you" /> }}
        columns={[
          { title: 'Record', render: (_, i) => <span><Tag>{kind[i.subjectType]}</Tag>{i.reference}</span> },
          { title: 'Property', render: (_, i) => <Link to={`/properties/${i.propertyId}`}>{i.pin}</Link> },
          {
            title: 'Your step', render: (_, i) => (
              <Space size={4} wrap>
                {i.stepLabel}
                {i.underDelegation && <Tag color="purple">under delegation: {i.underDelegation}</Tag>}
              </Space>
            ),
          },
          { title: 'Since', dataIndex: 'createdAt', render: (v: string) => new Date(v).toLocaleDateString() },
        ]}
      />
    </Space>
  );
}
