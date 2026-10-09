import { useState } from 'react';
import { Button, Input, Space, Table, Tag, Typography } from 'antd';
import { PlusOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { usePropertySearch } from '../../api/properties';
import type { PropertyDto } from '../../lib/types';
import { MunicipalityFilter } from '../../components/MunicipalityFilter';

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
  const [municipalityId, setMunicipalityId] = useState<string>();
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const { data, isLoading, isError, error } = usePropertySearch({ searchTerm: searchTerm || undefined, municipalityId, page, pageSize });

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

      <Space wrap style={{ marginBottom: 16, width: '100%' }}>
        <Input.Search
          placeholder="Search by PIN, lot number, title number, survey number, or tax map number"
          allowClear
          enterButton={<SearchOutlined />}
          style={{ width: 600, maxWidth: '100%' }}
          onSearch={(value) => {
            setSearchTerm(value);
            setPage(1);
          }}
        />
        <MunicipalityFilter value={municipalityId} onChange={(id) => { setMunicipalityId(id); setPage(1); }} />
      </Space>

      {isError && (
        <Typography.Text type="danger">Could not load properties: {(error as Error).message}</Typography.Text>
      )}

      <Table<PropertyDto>
        rowKey="id"
        scroll={{ x: true }}
        loading={isLoading}
        dataSource={data?.items ?? []}
        onRow={(record) => ({ onClick: () => navigate(`/properties/${record.id}`), style: { cursor: 'pointer' } })}
        pagination={{
          current: page,
          pageSize,
          total: data?.totalCount ?? 0,
          onChange: setPage,
          showSizeChanger: false,
          showTotal: (n) => (data?.totalIsLowerBound
            ? `More than ${n.toLocaleString('en-PH')} properties: refine the search to see the rest`
            : `${n.toLocaleString('en-PH')} properties`),
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
