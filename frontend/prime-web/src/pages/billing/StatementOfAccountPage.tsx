import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Button, Skeleton, Space, Table, Typography } from 'antd';
import { ArrowLeftOutlined, PrinterOutlined } from '@ant-design/icons';
import { useStatementOfAccount } from '../../api/bills';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import type { StatementLineDto } from '../../lib/types';

const lguName = import.meta.env.VITE_LGU_NAME?.trim();
const lguOffice = import.meta.env.VITE_LGU_OFFICE?.trim();

/**
 * CLAUDE.md §52/§57 statement of account: the posted bill per RPU and tax
 * year for one property. Payments are Phase 9, so nothing is deducted yet —
 * the page says so rather than implying a balance. Printable (A4, the
 * GisPrintPage conventions; LGU branding from configuration, §85).
 */
export function StatementOfAccountPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data, isLoading, isError, error } = useStatementOfAccount(id);

  if (isLoading) {
    return <Skeleton active />;
  }
  if (isError || !data) {
    return (
      <Alert
        type="error"
        showIcon
        title="Could not load the statement of account"
        description={error instanceof ApiRequestError ? error.apiError.message : ((error as Error)?.message ?? 'Not found')}
      />
    );
  }

  return (
    <div style={{ maxWidth: 1100 }}>
      <Space className="no-print" style={{ marginBottom: 16 }} wrap>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(`/properties/${data.propertyId}`)}>
          Back to property
        </Button>
        <Button type="primary" icon={<PrinterOutlined />} onClick={() => window.print()}>
          Print
        </Button>
      </Space>

      <div style={{ textAlign: 'center', marginBottom: 16 }}>
        {lguName && <Typography.Text strong style={{ display: 'block' }}>{lguName}</Typography.Text>}
        {lguOffice && <Typography.Text style={{ display: 'block' }}>{lguOffice}</Typography.Text>}
        <Typography.Title level={3} style={{ margin: '8px 0 0' }}>
          Statement of Account — Real Property Tax
        </Typography.Title>
      </div>

      <Typography.Paragraph>
        <strong>Property (PIN):</strong> {data.propertyIdentificationNumber}
        <br />
        <strong>Generated:</strong> {new Date(data.generatedAt).toLocaleString('en-PH', { dateStyle: 'long', timeStyle: 'short' })}
      </Typography.Paragraph>

      <Table<StatementLineDto>
        rowKey="billId"
        size="small"
        bordered
        dataSource={data.bills}
        pagination={false}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No posted bills.' }}
        columns={[
          { title: 'Tax Year', dataIndex: 'taxYear' },
          { title: 'RPU', dataIndex: 'rpuNumber' },
          { title: 'TD No.', dataIndex: 'taxDeclarationNumber' },
          { title: 'As of', dataIndex: 'asOfDate' },
          { title: 'Assessed Value', dataIndex: 'assessedValue', align: 'right', render: formatMoney },
          { title: 'Tax', dataIndex: 'tax', align: 'right', render: formatMoney },
          { title: 'Discount', dataIndex: 'discount', align: 'right', render: formatMoney },
          { title: 'Penalty', dataIndex: 'penalty', align: 'right', render: formatMoney },
          { title: 'Interest', dataIndex: 'interest', align: 'right', render: formatMoney },
          { title: 'Amount Due', dataIndex: 'total', align: 'right', render: (v: number) => <strong>{formatMoney(v)}</strong> },
        ]}
        summary={() => (
          <Table.Summary.Row>
            <Table.Summary.Cell index={0} colSpan={9} align="right"><strong>Total billed</strong></Table.Summary.Cell>
            <Table.Summary.Cell index={1} align="right"><strong>{formatMoney(data.totalBilled)}</strong></Table.Summary.Cell>
          </Table.Summary.Row>
        )}
      />

      <Typography.Paragraph type="secondary" style={{ marginTop: 16, fontSize: 12 }}>
        Amounts are computed as of each bill's date. Payments are not yet recorded in PRIME (Phase 9), so no payments are
        deducted. Rates are DEMO configuration values unless the LGU has entered its ordinance values.
      </Typography.Paragraph>
    </div>
  );
}
