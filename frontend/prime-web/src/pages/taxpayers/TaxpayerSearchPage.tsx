import { useState } from 'react';
import { Button, Input, Space, Table, Typography } from 'antd';
import { PlusOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { useTaxpayerSearch } from '../../api/taxpayers';
import type { TaxpayerDto } from '../../lib/types';

export function TaxpayerSearchPage() {
  const navigate = useNavigate();
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
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
        dataSource={data?.items ?? []}
        pagination={{
          current: page,
          pageSize,
          total: data?.totalCount ?? 0,
          onChange: setPage,
          showSizeChanger: false,
        }}
        columns={[
          { title: 'Name', dataIndex: 'displayName' },
          { title: 'Type', dataIndex: 'taxpayerType' },
          { title: 'TIN', dataIndex: 'tin' },
          { title: 'Contact', dataIndex: 'contactNumber' },
        ]}
      />
    </div>
  );
}
