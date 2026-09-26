import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, Input, Select, Space, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useSwornStatementSearch } from '../../api/swornStatements';
import { formatMoney } from '../../lib/format';
import { declarantCapacityLabel, type DeclarantCapacity, swornStatusColor, type SwornStatementStatus, type SwornStatementSummaryDto } from '../../lib/types';

/** The owners' sworn statements of market value (MRPAAO Att. 11; docs/analysis/mrpaao-forms-model.md §16). */
export function SwornStatementsPage() {
  const navigate = useNavigate();
  const [declarant, setDeclarant] = useState('');
  const [tdNumber, setTdNumber] = useState('');
  const [status, setStatus] = useState<SwornStatementStatus>();
  const [page, setPage] = useState(1);
  const { data, isFetching } = useSwornStatementSearch({ declarant: declarant || undefined, tdNumber: tdNumber || undefined, status, page, pageSize: 20 });

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Typography.Title level={3} style={{ margin: 0 }}>Sworn Statements</Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/sworn-statements/new')}>New sworn statement</Button>
      </Space>
      <Card>
        <Space wrap style={{ marginBottom: 12 }}>
          <Input.Search allowClear placeholder="Declarant, owner or index no." onSearch={(v) => { setDeclarant(v); setPage(1); }} style={{ width: 280 }} />
          <Input.Search allowClear placeholder="Exact TD number" onSearch={(v) => { setTdNumber(v); setPage(1); }} style={{ width: 220 }} />
          <Select allowClear placeholder="Status" value={status} onChange={(v) => { setStatus(v); setPage(1); }} style={{ width: 160 }}
            options={(['Draft', 'Filed', 'Superseded', 'Cancelled'] as const).map((s) => ({ value: s, label: s }))} />
        </Space>
        <Table<SwornStatementSummaryDto> rowKey="id" size="small" loading={isFetching} dataSource={data?.items ?? []} scroll={{ x: true }}
          onRow={(r) => ({ onClick: () => navigate(`/sworn-statements/${r.id}`), style: { cursor: 'pointer' } })}
          pagination={{ current: page, pageSize: 20, total: data?.totalCount ?? 0, onChange: setPage, showSizeChanger: false }}
          columns={[
            { title: 'Index No.', dataIndex: 'number', render: (v: string | null) => v ?? '—' },
            { title: 'Status', dataIndex: 'status', render: (s: SwornStatementStatus) => <Tag color={swornStatusColor[s]}>{s}</Tag> },
            { title: 'Declarant', dataIndex: 'declarantName' },
            { title: 'Capacity', dataIndex: 'capacity', render: (c: DeclarantCapacity) => declarantCapacityLabel[c] },
            { title: 'City/Municipality', dataIndex: 'municipalityName' },
            { title: 'Received', dataIndex: 'receivedOn', render: (v: string | null) => v ?? '—' },
            { title: 'Properties', dataIndex: 'itemCount', align: 'right' },
            { title: 'Total declared', dataIndex: 'totalDeclaredValue', align: 'right', render: formatMoney },
          ]} />
      </Card>
    </Space>
  );
}
