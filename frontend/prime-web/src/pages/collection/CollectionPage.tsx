import { useState } from 'react';
import { Alert, Button, DatePicker, Empty, Input, Modal, Space, Table, Tabs, Tag, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useDecideCancellation, usePaymentCancellations, usePayments } from '../../api/payments';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import type { PaymentCancellationDto, PaymentStatus, PaymentSummaryDto } from '../../lib/types';
import { PaymentDrawer } from '../properties/sections/PaymentsSection';
import { paymentStatusColor } from '../../lib/collection';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);

/**
 * Collection (CLAUDE.md §41; docs/analysis/collection.md §6): the day's
 * receipts and the void/reversal/correction requests waiting for a decision
 * by someone other than the requester. Remittance, the collection summary
 * and reconciliation arrive with step 9e.
 */
export function CollectionPage() {
  const pending = usePaymentCancellations('Pending');
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Title level={3} style={{ margin: 0 }}>Collection</Typography.Title>
      <Tabs items={[
        { key: 'receipts', label: 'Receipts', children: <ReceiptsTab /> },
        { key: 'requests', label: `Cancellation requests (${pending.data?.length ?? 0})`, children: <RequestsTab /> },
      ]} />
    </Space>
  );
}

function ReceiptsTab() {
  const [date, setDate] = useState<Dayjs>(dayjs());
  const [openId, setOpenId] = useState<string | null>(null);
  const { data = [], isLoading, isError, error } = usePayments(date.format('YYYY-MM-DD'));
  const standing = data.filter((p) => p.status === 'Posted');

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Space wrap>
        <DatePicker aria-label="Collection date" value={date} allowClear={false} onChange={(d) => d && setDate(d)} />
        <Typography.Text>{standing.length} posted receipt(s), <strong>{formatMoney(standing.reduce((s, p) => s + p.amountDue, 0))}</strong></Typography.Text>
      </Space>
      {isError && <Alert type="error" showIcon title="Could not load receipts" description={errorText(error)} />}
      <Table<PaymentSummaryDto>
        rowKey="id"
        size="small"
        loading={isLoading}
        dataSource={data}
        pagination={{ pageSize: 50, hideOnSinglePage: true }}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: <Empty description="No receipts on this date" /> }}
        columns={[
          { title: 'OR No.', dataIndex: 'officialReceiptNumber', render: (v: string, p) => <Button type="link" size="small" style={{ padding: 0 }} onClick={() => setOpenId(p.id)}>{v}</Button> },
          { title: 'Transaction No.', dataIndex: 'transactionNumber' },
          { title: 'Time', dataIndex: 'receivedAt', render: (v: string) => new Date(v).toLocaleTimeString('en-PH', { timeStyle: 'short' }) },
          { title: 'Payor', dataIndex: 'payorName' },
          { title: 'Amount', dataIndex: 'amountDue', align: 'right', render: formatMoney },
          { title: 'Status', dataIndex: 'status', render: (v: PaymentStatus) => <Tag color={paymentStatusColor[v]}>{v}</Tag> },
        ]}
      />
      <PaymentDrawer paymentId={openId} onClose={() => setOpenId(null)} />
    </Space>
  );
}

function RequestsTab() {
  const { data = [], isLoading, isError, error } = usePaymentCancellations('Pending');
  const decide = useDecideCancellation();
  const [deciding, setDeciding] = useState<{ request: PaymentCancellationDto; decision: 'approve' | 'reject' } | null>(null);
  const [remarks, setRemarks] = useState('');
  const [openId, setOpenId] = useState<string | null>(null);

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Alert type="info" showIcon title="Maker-checker" description="The user who asked for a void, reversal or correction cannot decide it. Approved on the payment's own date it is a void; later, a reversal." />
      {isError && <Alert type="error" showIcon title="Could not load requests" description={errorText(error)} />}
      <Table<PaymentCancellationDto>
        rowKey="id"
        size="small"
        loading={isLoading}
        dataSource={data}
        pagination={false}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: <Empty description="No requests waiting" /> }}
        expandable={{
          rowExpandable: (r) => r.isCorrection,
          expandedRowRender: (r) => r.replacement && (
            <Typography.Text>
              Reissue to <b>{r.replacement.payorName}</b>{r.replacement.payorAddress ? `, ${r.replacement.payorAddress}` : ''} for {formatMoney(r.replacement.expectedTotal)}
            </Typography.Text>
          ),
        }}
        columns={[
          { title: 'OR No.', dataIndex: 'paymentOfficialReceiptNumber', render: (v: string, r) => <Button type="link" size="small" style={{ padding: 0 }} onClick={() => setOpenId(r.paymentId)}>{v}</Button> },
          { title: 'Paid on', dataIndex: 'paymentDate' },
          { title: 'Payor', dataIndex: 'payorName' },
          { title: 'Amount', dataIndex: 'paymentAmount', align: 'right', render: formatMoney },
          { title: 'Request', key: 'kind', render: (_, r) => <Tag color={r.isCorrection ? 'blue' : 'red'}>{r.isCorrection ? 'Correction' : 'Void / reversal'}</Tag> },
          { title: 'Reason', dataIndex: 'reason' },
          { title: 'Requested', dataIndex: 'requestedAt', render: (v: string) => new Date(v).toLocaleString('en-PH') },
          {
            title: 'Decision', key: 'decide', render: (_, r) => (
              <Space>
                <Button size="small" type="primary" onClick={() => { setRemarks(''); decide.reset(); setDeciding({ request: r, decision: 'approve' }); }}>Approve</Button>
                <Button size="small" danger onClick={() => { setRemarks(''); decide.reset(); setDeciding({ request: r, decision: 'reject' }); }}>Reject</Button>
              </Space>
            ),
          },
        ]}
      />
      <Modal
        open={deciding !== null}
        title={deciding ? `${deciding.decision === 'approve' ? 'Approve' : 'Reject'} the request for OR ${deciding.request.paymentOfficialReceiptNumber}` : ''}
        okText={deciding?.decision === 'approve' ? 'Approve' : 'Reject'}
        okButtonProps={{ danger: deciding?.decision === 'reject', loading: decide.isPending, disabled: deciding?.decision === 'reject' && remarks.trim() === '' }}
        onCancel={() => setDeciding(null)}
        onOk={() => deciding && decide.mutate({ id: deciding.request.id, decision: deciding.decision, remarks: remarks.trim() || null }, { onSuccess: () => setDeciding(null) })}
        destroyOnHidden
      >
        {decide.isError && <Alert type="error" showIcon title="Not decided" description={errorText(decide.error)} style={{ marginBottom: 12 }} />}
        {deciding?.decision === 'approve' && (
          <Typography.Paragraph>
            OR {deciding.request.paymentOfficialReceiptNumber} will be {deciding.request.paymentDate === dayjs().format('YYYY-MM-DD') ? 'voided' : 'reversed'} and
            what it paid becomes due again{deciding.request.isCorrection ? '; the corrected receipt is issued in the same step' : ''}.
          </Typography.Paragraph>
        )}
        <Input.TextArea aria-label="Remarks" placeholder={deciding?.decision === 'reject' ? 'Reason for rejecting (required)' : 'Remarks (optional)'}
          rows={3} maxLength={1000} value={remarks} onChange={(e) => setRemarks(e.target.value)} />
      </Modal>
      <PaymentDrawer paymentId={openId} onClose={() => setOpenId(null)} />
    </Space>
  );
}
