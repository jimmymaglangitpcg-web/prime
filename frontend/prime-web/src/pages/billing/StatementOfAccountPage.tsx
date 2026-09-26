import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Button, Skeleton, Space, Table, Typography } from 'antd';
import { ArrowLeftOutlined, DollarOutlined, PrinterOutlined } from '@ant-design/icons';
import { useStatementOfAccount } from '../../api/bills';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import { PrintFormButton } from '../../components/PrintFormButton';
import type { PaymentStatus, StatementLineDto, StatementPaymentDto } from '../../lib/types';

const lguName = import.meta.env.VITE_LGU_NAME?.trim();
const lguOffice = import.meta.env.VITE_LGU_OFFICE?.trim();

/**
 * CLAUDE.md §52/§57 statement of account: the posted bill per RPU and tax
 * year for one property, what standing payments have settled of its tax,
 * what is outstanding and what settling it would cost today (with today's
 * discounts, penalties and interest), and the property's receipts
 * (docs/analysis/collection.md §6). Printable (A4, the GisPrintPage
 * conventions; LGU branding from configuration, §85).
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
        {/* The configured STATEMENT_OF_ACCOUNT form; preview only until statements are kept as records. */}
        <PrintFormButton formCode="STATEMENT_OF_ACCOUNT" subjectId={data.propertyId} issuable={false} />
        <Button icon={<DollarOutlined />} disabled={data.totalOutstandingPrincipal === 0} onClick={() => navigate(`/collection/pay?propertyId=${data.propertyId}`)}>
          Take payment
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
        <br />
        <strong>Amounts due as of:</strong> {data.asOfDate}
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
          { title: 'Assessed Value', dataIndex: 'assessedValue', align: 'right', render: formatMoney },
          { title: 'Tax', dataIndex: 'principalOwed', align: 'right', render: formatMoney },
          { title: 'Paid', dataIndex: 'principalPaid', align: 'right', render: formatMoney },
          { title: 'Outstanding', dataIndex: 'outstandingPrincipal', align: 'right', render: formatMoney },
          { title: `Due ${data.asOfDate}`, dataIndex: 'dueAsOf', align: 'right', render: (v: number) => <strong>{formatMoney(v)}</strong> },
        ]}
        summary={() => (
          <Table.Summary.Row>
            <Table.Summary.Cell index={0} colSpan={5} align="right"><strong>Total</strong></Table.Summary.Cell>
            <Table.Summary.Cell index={1} align="right"><strong>{formatMoney(data.totalPrincipalPaid)}</strong></Table.Summary.Cell>
            <Table.Summary.Cell index={2} align="right"><strong>{formatMoney(data.totalOutstandingPrincipal)}</strong></Table.Summary.Cell>
            <Table.Summary.Cell index={3} align="right"><strong>{formatMoney(data.totalDueAsOf)}</strong></Table.Summary.Cell>
          </Table.Summary.Row>
        )}
      />

      <Typography.Title level={5} style={{ marginTop: 24 }}>Receipts</Typography.Title>
      <Table<StatementPaymentDto>
        rowKey="paymentId"
        size="small"
        bordered
        dataSource={data.payments}
        pagination={false}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No payments recorded.' }}
        columns={[
          { title: 'OR No.', dataIndex: 'officialReceiptNumber' },
          { title: 'Date', dataIndex: 'paymentDate' },
          { title: 'Payor', dataIndex: 'payorName' },
          { title: 'Status', dataIndex: 'status', render: (v: PaymentStatus) => (v === 'Posted' ? v : <strong>{v.toUpperCase()}</strong>) },
          { title: 'Amount', dataIndex: 'amount', align: 'right', render: formatMoney },
        ]}
      />

      <Typography.Paragraph type="secondary" style={{ marginTop: 16, fontSize: 12 }}>
        Paid and outstanding count tax only. "Due" is what settling the outstanding tax would cost on {data.asOfDate}, with that
        day's discounts, penalties and interest. Voided and reversed receipts are listed but not counted. Rates are DEMO
        configuration values unless the LGU has entered its ordinance values.
      </Typography.Paragraph>
    </div>
  );
}
