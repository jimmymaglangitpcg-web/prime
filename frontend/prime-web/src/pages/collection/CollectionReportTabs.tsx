import { useState } from 'react';
import { Alert, Button, DatePicker, Descriptions, Empty, Input, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import { DownloadOutlined, SendOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useCollectionSummary, useCreateRemittance, useDecideRemittance, useReconciliation, useRemittances } from '../../api/payments';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import type { CollectionGroupBy, CollectionSummaryRowDto, ReconciliationRowDto, RemittanceDto, RemittanceStatus } from '../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const remittanceColor: Record<RemittanceStatus, string> = { Submitted: 'blue', Accepted: 'green', Returned: 'orange' };
const ymd = (d: Dayjs) => d.format('YYYY-MM-DD');

/**
 * A cashier remits the day's posted, unremitted receipts; another user accepts
 * the remittance or returns it with a reason (docs/analysis/collection.md §4.7).
 * Once remitted, a receipt can only be reversed, not voided.
 */
export function RemittancesTab() {
  const [date, setDate] = useState<Dayjs>(dayjs());
  const { data = [], isLoading, isError, error } = useRemittances(ymd(date));
  const create = useCreateRemittance();
  const decide = useDecideRemittance();
  const [modal, modalContext] = Modal.useModal();
  const [deciding, setDeciding] = useState<{ remittance: RemittanceDto; decision: 'accept' | 'return' } | null>(null);
  const [remarks, setRemarks] = useState('');

  function confirmRemit() {
    create.reset();
    modal.confirm({
      title: `Remit your receipts of ${ymd(date)}?`,
      content: 'Every posted receipt of yours dated that day and not yet remitted is included. After this, they can only be reversed, not voided.',
      okText: 'Remit',
      onOk: () => create.mutateAsync({ collectionDate: ymd(date), remarks: null }).catch(() => undefined),
    });
  }

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      {modalContext}
      <Space wrap>
        <DatePicker aria-label="Collection date" value={date} allowClear={false} onChange={(d) => d && setDate(d)} />
        <Button type="primary" icon={<SendOutlined />} loading={create.isPending} onClick={confirmRemit}>Remit my receipts</Button>
      </Space>
      {create.isError && <Alert type="error" showIcon title="Nothing was remitted" description={errorText(create.error)} />}
      {isError && <Alert type="error" showIcon title="Could not load remittances" description={errorText(error)} />}
      <Table<RemittanceDto>
        rowKey="id"
        size="small"
        loading={isLoading}
        dataSource={data}
        pagination={false}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: <Empty description="No remittances on this date" /> }}
        expandable={{ expandedRowRender: (r) => <RemittanceDetail remittance={r} /> }}
        columns={[
          { title: 'No.', dataIndex: 'remittanceNumber', render: (v: string | null) => v ?? '—' },
          { title: 'Cashier', key: 'cashier', render: (_, r) => r.cashierName ?? r.cashierUserId },
          { title: 'Submitted', dataIndex: 'submittedAt', render: (v: string) => new Date(v).toLocaleString('en-PH') },
          { title: 'Receipts', dataIndex: 'paymentCount', align: 'right' },
          { title: 'Total', dataIndex: 'totalAmount', align: 'right', render: (v: number) => <strong>{formatMoney(v)}</strong> },
          { title: 'Status', dataIndex: 'status', render: (v: RemittanceStatus) => <Tag color={remittanceColor[v]}>{v}</Tag> },
          {
            title: 'Decision', key: 'decide', render: (_, r) => r.status === 'Submitted' ? (
              <Space>
                <Button size="small" type="primary" onClick={() => { setRemarks(''); decide.reset(); setDeciding({ remittance: r, decision: 'accept' }); }}>Accept</Button>
                <Button size="small" danger onClick={() => { setRemarks(''); decide.reset(); setDeciding({ remittance: r, decision: 'return' }); }}>Return</Button>
              </Space>
            ) : (r.decisionRemarks ?? '—'),
          },
        ]}
      />
      <Modal
        open={deciding !== null}
        title={deciding ? `${deciding.decision === 'accept' ? 'Accept' : 'Return'} the remittance of ${formatMoney(deciding.remittance.totalAmount)}` : ''}
        okText={deciding?.decision === 'accept' ? 'Accept' : 'Return'}
        okButtonProps={{ danger: deciding?.decision === 'return', loading: decide.isPending, disabled: deciding?.decision === 'return' && remarks.trim() === '' }}
        onCancel={() => setDeciding(null)}
        onOk={() => deciding && decide.mutate({ id: deciding.remittance.id, decision: deciding.decision, remarks: remarks.trim() || null }, { onSuccess: () => setDeciding(null) })}
        destroyOnHidden
      >
        {decide.isError && <Alert type="error" showIcon title="Not decided" description={errorText(decide.error)} style={{ marginBottom: 12 }} />}
        <Typography.Paragraph>
          {deciding?.decision === 'accept'
            ? 'Accept once the money counted matches the totals by mode of payment.'
            : 'Returning frees its receipts; the cashier remits them again after correcting.'}
        </Typography.Paragraph>
        <Input.TextArea aria-label="Remarks" placeholder={deciding?.decision === 'return' ? 'Reason (required)' : 'Remarks (optional)'}
          rows={3} maxLength={1000} value={remarks} onChange={(e) => setRemarks(e.target.value)} />
      </Modal>
    </Space>
  );
}

function RemittanceDetail({ remittance }: { remittance: RemittanceDto }) {
  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      {remittance.remarks && <Typography.Text>Remarks: {remittance.remarks}</Typography.Text>}
      <Descriptions size="small" bordered column={1} title="By mode of payment (net of change)">
        {remittance.modeTotals.map((m) => <Descriptions.Item key={m.paymentModeId} label={m.modeName}>{formatMoney(m.amount)}</Descriptions.Item>)}
      </Descriptions>
      <Table
        size="small"
        rowKey="accountCode"
        dataSource={remittance.accountTotals}
        pagination={false}
        scroll={{ x: 'max-content' }}
        columns={[
          { title: 'Account', key: 'account', render: (_, a) => `${a.accountCode} — ${a.accountName}` },
          { title: 'Fund', dataIndex: 'fund', render: (v: string | null) => v ?? '—' },
          { title: 'Amount', dataIndex: 'amount', align: 'right', render: formatMoney },
        ]}
      />
      <Table
        size="small"
        rowKey="paymentId"
        dataSource={remittance.items}
        pagination={false}
        scroll={{ x: 'max-content' }}
        columns={[
          { title: 'OR No.', dataIndex: 'officialReceiptNumber' },
          { title: 'Payor', dataIndex: 'payorName' },
          { title: 'Receipt now', dataIndex: 'paymentStatus' },
          { title: 'Amount', dataIndex: 'amount', align: 'right', render: formatMoney },
        ]}
      />
    </Space>
  );
}

const groupings: { value: CollectionGroupBy; label: string }[] = [
  { value: 'Date', label: 'Date' },
  { value: 'Cashier', label: 'Cashier' },
  { value: 'Mode', label: 'Mode of payment' },
  { value: 'TaxType', label: 'Tax type' },
  { value: 'TaxYear', label: 'Tax year' },
  { value: 'YearCategory', label: 'Current / prior / advance' },
  { value: 'Fund', label: 'Fund' },
  { value: 'Account', label: 'Revenue account' },
  { value: 'Barangay', label: 'Barangay' },
];

/** CSV-escape one value (RFC 4180). */
const csvCell = (v: string | number) => {
  const text = String(v);
  return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
};

/**
 * Collections by one dimension over a date range (CLAUDE.md §41). Receipts
 * count on their payment date; a reversal is a negative on the day it was
 * approved; voided receipts are never counted. Exportable as CSV (PDF/Excel
 * with Phase 11).
 */
export function SummaryTab() {
  const [range, setRange] = useState<[Dayjs, Dayjs]>([dayjs().startOf('month'), dayjs()]);
  const [groupBy, setGroupBy] = useState<CollectionGroupBy>('TaxType');
  const { data, isLoading, isError, error } = useCollectionSummary(ymd(range[0]), ymd(range[1]), groupBy);

  function downloadCsv() {
    if (!data) return;
    const header = ['Group', 'Receipts', 'Collected', 'Reversed', 'Net'];
    const lines = [header, ...data.rows.map((r) => [r.label, r.receipts, r.collected.toFixed(2), r.reversed.toFixed(2), r.net.toFixed(2)]),
      ['TOTAL', '', data.totalCollected.toFixed(2), data.totalReversed.toFixed(2), data.net.toFixed(2)]];
    const blob = new Blob([lines.map((l) => l.map(csvCell).join(',')).join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `collections-${data.groupBy.toLowerCase()}-${data.from}-to-${data.to}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Space wrap>
        <DatePicker.RangePicker aria-label="Collection period" value={range} allowClear={false}
          onChange={(v) => v?.[0] && v?.[1] && setRange([v[0], v[1]])} />
        <Select aria-label="Group by" style={{ width: 220 }} value={groupBy} options={groupings} onChange={setGroupBy} />
        <Button icon={<DownloadOutlined />} disabled={!data} onClick={downloadCsv}>CSV</Button>
      </Space>
      {isError && <Alert type="error" showIcon title="Could not load the summary" description={errorText(error)} />}
      <Table<CollectionSummaryRowDto>
        rowKey="key"
        size="small"
        loading={isLoading}
        dataSource={data?.rows ?? []}
        pagination={false}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: <Empty description="No collections in this period" /> }}
        columns={[
          { title: groupings.find((g) => g.value === groupBy)?.label, dataIndex: 'label' },
          { title: 'Receipts', dataIndex: 'receipts', align: 'right' },
          { title: 'Collected', dataIndex: 'collected', align: 'right', render: formatMoney },
          { title: 'Reversed', dataIndex: 'reversed', align: 'right', render: formatMoney },
          { title: 'Net', dataIndex: 'net', align: 'right', render: (v: number) => <strong>{formatMoney(v)}</strong> },
        ]}
        summary={() => data && (
          <Table.Summary.Row>
            <Table.Summary.Cell index={0} colSpan={2} align="right"><strong>Total</strong></Table.Summary.Cell>
            <Table.Summary.Cell index={1} align="right"><strong>{formatMoney(data.totalCollected)}</strong></Table.Summary.Cell>
            <Table.Summary.Cell index={2} align="right"><strong>{formatMoney(data.totalReversed)}</strong></Table.Summary.Cell>
            <Table.Summary.Cell index={3} align="right"><strong>{formatMoney(data.net)}</strong></Table.Summary.Cell>
          </Table.Summary.Row>
        )}
      />
    </Space>
  );
}

/** Per cashier and day: receipts, their lines, the money tendered and the remittances must agree; any difference is listed. */
export function ReconciliationTab() {
  const [date, setDate] = useState<Dayjs>(dayjs());
  const { data, isLoading, isError, error } = useReconciliation(ymd(date));
  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Space wrap>
        <DatePicker aria-label="Reconciliation date" value={date} allowClear={false} onChange={(d) => d && setDate(d)} />
        {data && (data.balanced ? <Tag color="green">Balanced</Tag> : <Tag color="red">Needs attention</Tag>)}
      </Space>
      {isError && <Alert type="error" showIcon title="Could not reconcile" description={errorText(error)} />}
      <Table<ReconciliationRowDto>
        rowKey={(r) => r.cashierUserId ?? 'none'}
        size="small"
        loading={isLoading}
        dataSource={data?.cashiers ?? []}
        pagination={false}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: <Empty description="No receipts on this date" /> }}
        expandable={{
          rowExpandable: (r) => r.problems.length > 0,
          expandedRowRender: (r) => <ul style={{ margin: 0 }}>{r.problems.map((p) => <li key={p}>{p}</li>)}</ul>,
        }}
        columns={[
          { title: 'Cashier', key: 'cashier', render: (_, r) => r.cashierName ?? r.cashierUserId ?? '(no user)' },
          { title: 'Receipts', dataIndex: 'receipts', align: 'right' },
          { title: 'Amount', dataIndex: 'amountDue', align: 'right', render: formatMoney },
          { title: 'Lines', dataIndex: 'allocationTotal', align: 'right', render: formatMoney },
          { title: 'Tendered − change', dataIndex: 'tenderedLessChange', align: 'right', render: formatMoney },
          { title: 'Remitted', dataIndex: 'remitted', align: 'right', render: formatMoney },
          { title: 'Unremitted', dataIndex: 'unremitted', align: 'right', render: formatMoney },
          { title: 'Voided', dataIndex: 'voidedReceipts', align: 'right' },
          { title: 'Check', key: 'check', render: (_, r) => (r.problems.length === 0 ? <Tag color="green">OK</Tag> : <Tag color="red">{r.problems.length} issue(s)</Tag>) },
        ]}
      />
    </Space>
  );
}
