import { useNavigate } from 'react-router-dom';
import { Alert, Button, Table, Tag, Typography } from 'antd';
import { useRpuAssessments } from '../../../api/assessments';
import { usePropertySwornStatements } from '../../../api/swornStatements';
import { formatMoney } from '../../../lib/format';
import { swornItemKindLabel, swornStatusColor, type SwornStatementDto, type SwornStatementItemDto, type SwornStatementStatus } from '../../../lib/types';

interface Row {
  key: string;
  statement: SwornStatementDto;
  item: SwornStatementItemDto;
}

/**
 * The owners' sworn statements declaring this property or its units
 * (docs/analysis/mrpaao-forms-model.md §16.4). Declared values are
 * information only: each is shown next to the unit's latest posted
 * appraisal, never used in valuation.
 */
export function SwornStatementsSection({ propertyId }: { propertyId: string }) {
  const navigate = useNavigate();
  const { data: statements = [], isLoading } = usePropertySwornStatements(propertyId);
  const rows: Row[] = statements.flatMap((s) => s.items.filter((i) => i.propertyId === propertyId).map((i) => ({ key: i.id, statement: s, item: i })));
  return (
    <>
      <Alert type="info" showIcon style={{ marginBottom: 12 }} title="Declared values are information only"
        description="The owner's declared value is shown next to the appraised value; valuation follows the SMV and the valuation rules." />
      <Table<Row> rowKey="key" size="small" loading={isLoading} dataSource={rows} pagination={false} scroll={{ x: true }}
        locale={{ emptyText: 'No filed sworn statement declares this property' }}
        columns={[
          {
            title: 'Statement', render: (_, r) => (
              <Button type="link" style={{ padding: 0 }} onClick={() => navigate(`/sworn-statements/${r.statement.id}`)}>
                {r.statement.number ?? '(unnumbered)'}
              </Button>
            ),
          },
          { title: 'Status', render: (_, r) => <Tag color={swornStatusColor[r.statement.status as SwornStatementStatus]}>{r.statement.status}</Tag> },
          { title: 'Received', render: (_, r) => r.statement.receivedOn ?? '—' },
          { title: 'Declarant', render: (_, r) => r.statement.declarantName },
          { title: 'Part', render: (_, r) => swornItemKindLabel[r.item.kind] },
          { title: 'Unit', render: (_, r) => r.item.rpuNumber ?? '—' },
          { title: 'Declared value', align: 'right', render: (_, r) => formatMoney(r.item.declaredMarketValue) },
          { title: 'Appraised (latest posted)', align: 'right', render: (_, r) => <Appraised rpuId={r.item.rpuId} declared={r.item.declaredMarketValue} /> },
        ]} />
    </>
  );
}

function Appraised({ rpuId, declared }: { rpuId: string | null; declared: number }) {
  const { data: assessments = [] } = useRpuAssessments(rpuId ?? undefined);
  const posted = assessments.filter((a) => a.status === 'Posted').sort((a, b) => b.effectiveDate.localeCompare(a.effectiveDate))[0];
  if (!rpuId) {
    return <Typography.Text type="secondary">not linked</Typography.Text>;
  }
  if (!posted) {
    return <Typography.Text type="secondary">none posted</Typography.Text>;
  }
  const difference = declared - posted.marketValue;
  return (
    <span title={`Effective ${posted.effectiveDate}`}>
      {formatMoney(posted.marketValue)}{' '}
      <Typography.Text type={difference < 0 ? 'danger' : 'secondary'} style={{ fontSize: 12 }}>
        ({difference >= 0 ? '+' : ''}{formatMoney(difference)} declared)
      </Typography.Text>
    </span>
  );
}
