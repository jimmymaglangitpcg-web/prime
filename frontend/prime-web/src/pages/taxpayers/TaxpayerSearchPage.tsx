import { useState } from 'react';
import { Button, Input, Space, Table, Tag, Tooltip, Typography } from 'antd';
import { PlusOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { useTaxpayerSearch } from '../../api/taxpayers';
import type { TaxpayerDto } from '../../lib/types';
import { TaxpayerDetailsModal } from './TaxpayerDetailsModal';

export function TaxpayerSearchPage() {
  const navigate = useNavigate();
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<TaxpayerDto | null>(null);
  const pageSize = 20;

  const { data, isLoading } = useTaxpayerSearch({ searchTerm: searchTerm || undefined, page, pageSize });

  return (
    <div>
      <Space style={{ marginBottom: 16, width: '100%', justifyContent: 'space-between' }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          Taxpayer Registry
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/taxpayers/new')}>
          Register Taxpayer
        </Button>
      </Space>

      <Input.Search
        placeholder="Search by name or TIN"
        allowClear
        enterButton={<SearchOutlined />}
        style={{ marginBottom: 16, maxWidth: 500 }}
        onSearch={(value) => {
          setSearchTerm(value);
          setPage(1);
        }}
      />

      <Table<TaxpayerDto>
        rowKey="id"
        loading={isLoading}
        scroll={{ x: 'max-content' }}
        dataSource={data?.items ?? []}
        pagination={{
          current: page,
          pageSize,
          total: data?.totalCount ?? 0,
          onChange: setPage,
          showSizeChanger: false,
          showTotal: (n) => (data?.totalIsLowerBound
            ? `More than ${n.toLocaleString('en-PH')} owners: refine the search to see the rest`
            : `${n.toLocaleString('en-PH')} owners`),
        }}
        columns={[
          {
            title: 'Name', dataIndex: 'displayName', render: (name: string, t) => (
              <Space size={4} wrap>
                {name}
                {t.limited && (
                  <Tooltip title="Not a party to a property in your office's jurisdiction: only the name and TIN are shown, enough to link the existing record instead of registering it twice.">
                    <Tag>other jurisdiction</Tag>
                  </Tooltip>
                )}
                {t.personalDataMasked && (
                  <Tooltip title="Personal data (TIN, contact, e-mail, address) is masked: seeing it needs the taxpayer.view-personal permission.">
                    <Tag>masked</Tag>
                  </Tooltip>
                )}
              </Space>
            ),
          },
          { title: 'Type', dataIndex: 'taxpayerType' },
          { title: 'TIN', dataIndex: 'tin' },
          { title: 'Contact', dataIndex: 'contactNumber' },
          { title: 'Email', dataIndex: 'email' },
          {
            title: '', key: 'actions', width: 110,
            render: (_: unknown, t) => !t.limited && !t.personalDataMasked && <Button size="small" onClick={() => setEditing(t)}>Edit details</Button>,
          },
        ]}
      />
      {editing && <TaxpayerDetailsModal taxpayer={editing} onClose={() => setEditing(null)} />}
    </div>
  );
}
