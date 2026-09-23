import { useState } from 'react';
import { Button, Input, Space, Table, Tag, Typography } from 'antd';
import { PlusOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { usePropertySearch } from '../../api/properties';
import type { PropertyDto } from '../../lib/types';

const statusColor: Record<string, string> = {
  Active: 'green',
  Inactive: 'default',
  Cancelled: 'red',
  Subdivided: 'orange',
  Consolidated: 'orange',
  Superseded: 'default',
};

export function PropertySearchPage() {
  const navigate = useNavigate();
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const { data, isLoading, isError, error } = usePropertySearch({ searchTerm: searchTerm || undefined, page, pageSize });

  return (
    <div>
      <Space style={{ marginBottom: 16, width: '100%', justifyContent: 'space-between' }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          Property Registry
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/properties/new')}>
          Register Property
        </Button>
      </Space>

      <Input.Search
        placeholder="Search by PIN, lot number, title number, survey number, or tax map number"
        allowClear
        enterButton={<SearchOutlined />}
        style={{ marginBottom: 16, maxWidth: 600 }}
        onSearch={(value) => {
          setSearchTerm(value);
          setPage(1);
        }}
      />

      {isError && (
        <Typography.Text type="danger">Could not load properties: {(error as Error).message}</Typography.Text>
      )}

      <Table<PropertyDto>
        rowKey="id"
        loading={isLoading}
        dataSource={data?.items ?? []}
        onRow={(record) => ({ onClick: () => navigate(`/properties/${record.id}`), style: { cursor: 'pointer' } })}
        pagination={{
          current: page,
          pageSize,
          total: data?.totalCount ?? 0,
          onChange: setPage,
          showSizeChanger: false,
        }}
        columns={[
          { title: 'PIN', dataIndex: 'propertyIdentificationNumber' },
          { title: 'Lot No.', dataIndex: 'lotNumber' },
          { title: 'Barangay', dataIndex: 'barangayName' },
          { title: 'Municipality', dataIndex: 'municipalityName' },
          {
            title: 'Status',
            dataIndex: 'status',
            render: (status: string) => <Tag color={statusColor[status] ?? 'default'}>{status}</Tag>,
          },
        ]}
      />
    </div>
  );
}
