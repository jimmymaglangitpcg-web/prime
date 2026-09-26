import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Button, Descriptions, Drawer, Empty, Input, Modal, Space, Table, Tag, Typography } from 'antd';
import { DollarOutlined } from '@ant-design/icons';
import { usePayment, usePropertyPayments, useRequestCancellation, useRequestCorrection } from '../../../api/payments';
import { PrintFormButton } from '../../../components/PrintFormButton';
import { ApiRequestError } from '../../../lib/apiClient';
import { formatMoney } from '../../../lib/format';
import { paymentStatusColor } from '../../../lib/collection';
import type { PaymentCancellationDto, PaymentDto, PaymentItemRequest, PaymentStatus, PaymentSummaryDto } from '../../../lib/types';
import { AllocationsTable } from '../../collection/PaymentWorkspacePage';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);

/**
 * The property's receipts (docs/analysis/collection.md §6), including voided
 * and reversed ones — history is never hidden. From here a cashier takes a
 * payment, prints a receipt, or asks for a void/reversal or a correction,
 * which another user decides on the Collection page.
 */
export function PaymentsSection({ propertyId }: { propertyId: string }) {
  const navigate = useNavigate();
  const { data = [], isLoading, isError, error } = usePropertyPayments(propertyId);
  const [openId, setOpenId] = useState<string | null>(null);

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, flexWrap: 'wrap', marginBottom: 12 }}>
        <Typography.Title level={5} style={{ margin: 0 }}>Payments</Typography.Title>
        <Button type="primary" icon={<DollarOutlined />} onClick={() => navigate(`/collection/pay?propertyId=${propertyId}`)}>Take payment</Button>
      </div>
      {isError && <Alert type="error" showIcon title="Could not load payments" description={errorText(error)} style={{ marginBottom: 12 }} />}
      <Table<PaymentSummaryDto>
        rowKey="id"
        size="small"
        loading={isLoading}
        dataSource={data}
        pagination={false}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: <Empty description="No payments recorded" /> }}
        columns={[
          { title: 'OR No.', dataIndex: 'officialReceiptNumber', render: (v: string, p) => <Button type="link" size="small" style={{ padding: 0 }} onClick={() => setOpenId(p.id)}>{v}</Button> },
          { title: 'Transaction No.', dataIndex: 'transactionNumber' },
          { title: 'Date', dataIndex: 'paymentDate' },
          { title: 'Payor', dataIndex: 'payorName' },
          { title: 'Amount', dataIndex: 'amountDue', align: 'right', render: formatMoney },
          { title: 'Status', dataIndex: 'status', render: (v: PaymentStatus) => <Tag color={paymentStatusColor[v]}>{v}</Tag> },
          { title: '', key: 'open', render: (_, p) => <Button size="small" onClick={() => setOpenId(p.id)}>Details</Button> },
        ]}
      />
      <PaymentDrawer paymentId={openId} onClose={() => setOpenId(null)} />
    </div>
  );
}

/** One payment: tenders, allocation lines, cancellation history, and the actions a cashier may take. */
export function PaymentDrawer({ paymentId, onClose }: { paymentId: string | null; onClose: () => void }) {
  const { data: payment, isLoading } = usePayment(paymentId ?? undefined);
  const [asking, setAsking] = useState<'cancel' | 'correct' | null>(null);
  const pending = payment?.cancellations.find((c) => c.status === 'Pending');

  return (
    <Drawer open={paymentId !== null} onClose={onClose} size="large" title={payment ? `OR ${payment.officialReceiptNumber}` : 'Payment'} destroyOnHidden>
      {isLoading || !payment ? null : (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Space wrap>
            <Tag color={paymentStatusColor[payment.status]}>{payment.status}</Tag>
            {payment.status === 'Posted' && <PrintFormButton formCode="OFFICIAL_RECEIPT" subjectId={payment.id} issuable label="Print receipt" />}
            {payment.status === 'Posted' && !pending && (
              <>
                <Button size="small" danger onClick={() => setAsking('cancel')}>Request void / reversal</Button>
                <Button size="small" onClick={() => setAsking('correct')}>Request correction</Button>
              </>
            )}
          </Space>
          {pending && <Alert type="warning" showIcon title={`${pending.isCorrection ? 'Correction' : 'Cancellation'} requested — waiting for another user's decision`} description={pending.reason} />}

          <Descriptions size="small" bordered column={{ xs: 1, sm: 2, md: 2, lg: 2, xl: 2, xxl: 2 }}>
            <Descriptions.Item label="Transaction No.">{payment.transactionNumber}</Descriptions.Item>
            <Descriptions.Item label="Received">{new Date(payment.receivedAt).toLocaleString('en-PH', { dateStyle: 'long', timeStyle: 'short' })}</Descriptions.Item>
            <Descriptions.Item label="Payor" span="filled">{payment.payorName}{payment.payorAddress ? `, ${payment.payorAddress}` : ''}</Descriptions.Item>
            <Descriptions.Item label="Amount due">{formatMoney(payment.amountDue)}</Descriptions.Item>
            <Descriptions.Item label="Tendered / change">{formatMoney(payment.amountTendered)} / {formatMoney(payment.change)}</Descriptions.Item>
            <Descriptions.Item label="Modes" span="filled">
              {payment.tenders.map((t) => `${t.modeName} ${formatMoney(t.amount)}${t.reference ? ` (${t.reference}${t.bank ? `, ${t.bank}` : ''})` : ''}`).join('; ')}
            </Descriptions.Item>
            {payment.replacesPaymentId && <Descriptions.Item label="Corrects" span="filled">an earlier receipt (voided/reversed in the same step)</Descriptions.Item>}
            {payment.remarks && <Descriptions.Item label="Remarks" span="filled">{payment.remarks}</Descriptions.Item>}
          </Descriptions>

          <AllocationsTable allocations={payment.allocations} total={payment.amountDue} />

          {payment.cancellations.length > 0 && <CancellationHistory rows={payment.cancellations} />}
        </Space>
      )}
      {payment && asking === 'cancel' && <CancellationModal payment={payment} onClose={() => setAsking(null)} />}
      {payment && asking === 'correct' && <CorrectionModal payment={payment} onClose={() => setAsking(null)} />}
    </Drawer>
  );
}

export function CancellationHistory({ rows }: { rows: PaymentCancellationDto[] }) {
  return (
    <Table<PaymentCancellationDto>
      rowKey="id"
      size="small"
      dataSource={rows}
      pagination={false}
      scroll={{ x: 'max-content' }}
      columns={[
        { title: 'Requested', dataIndex: 'requestedAt', render: (v: string) => new Date(v).toLocaleString('en-PH') },
        { title: 'Kind', key: 'kind', render: (_, c) => (c.isCorrection ? 'Correction' : 'Cancellation') + (c.kind ? ` (${c.kind})` : '') },
        { title: 'Reason', dataIndex: 'reason' },
        { title: 'Status', dataIndex: 'status' },
        { title: 'Decision', key: 'decision', render: (_, c) => [c.decisionRemarks, c.transactionNumber && `Txn ${c.transactionNumber}`].filter(Boolean).join(' · ') || '—' },
      ]}
    />
  );
}

function CancellationModal({ payment, onClose }: { payment: PaymentDto; onClose: () => void }) {
  const [reason, setReason] = useState('');
  const request = useRequestCancellation();
  return (
    <Modal
      open
      title={`Void or reverse OR ${payment.officialReceiptNumber}`}
      okText="Send request"
      okButtonProps={{ danger: true, disabled: reason.trim() === '', loading: request.isPending }}
      onCancel={onClose}
      onOk={() => request.mutate({ paymentId: payment.id, reason: reason.trim() }, { onSuccess: onClose })}
    >
      {request.isError && <Alert type="error" showIcon title="Request not sent" description={errorText(request.error)} style={{ marginBottom: 12 }} />}
      <Typography.Paragraph>
        Another user must approve. Approved today it is a <b>void</b>; later, a <b>reversal</b>. The receipt stays on record and what it
        paid becomes due again.
      </Typography.Paragraph>
      <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
    </Modal>
  );
}

/**
 * A correction re-issues the same payment with corrected payor details: the
 * same installments, amounts and tenders, dated like the original. Another
 * user approves; the original is then voided (or reversed) in the same step.
 */
function CorrectionModal({ payment, onClose }: { payment: PaymentDto; onClose: () => void }) {
  const [reason, setReason] = useState('');
  const [payorName, setPayorName] = useState(payment.payorName);
  const [payorAddress, setPayorAddress] = useState(payment.payorAddress ?? '');
  const request = useRequestCorrection();

  // The same principal per installment; the server checks the total as of the original date.
  const items: PaymentItemRequest[] = Object.values(payment.allocations.filter((a) => a.component === 'Tax').reduce<Record<string, PaymentItemRequest>>((acc, a) => {
    const key = `${a.rpuId}:${a.taxYear}:${a.installmentSequence}`;
    acc[key] ??= { rpuId: a.rpuId, taxYear: a.taxYear, installmentSequence: a.installmentSequence, principalAmount: 0 };
    acc[key].principalAmount = Math.round(((acc[key].principalAmount ?? 0) + a.amount) * 100) / 100;
    return acc;
  }, {}));

  return (
    <Modal
      open
      width={560}
      title={`Correct OR ${payment.officialReceiptNumber}`}
      okText="Send request"
      okButtonProps={{ disabled: reason.trim() === '' || payorName.trim() === '', loading: request.isPending }}
      onCancel={onClose}
      onOk={() => request.mutate({
        paymentId: payment.id,
        reason: reason.trim(),
        replacement: {
          payorTaxpayerId: payorName === payment.payorName ? payment.payorTaxpayerId : null,
          payorName: payorName.trim(),
          payorAddress: payorAddress.trim() || null,
          items,
          tenders: payment.tenders.map((t) => ({ paymentModeId: t.paymentModeId, amount: t.amount, reference: t.reference, bank: t.bank, checkDate: t.checkDate })),
          expectedTotal: payment.amountDue,
          remarks: payment.remarks,
        },
      }, { onSuccess: onClose })}
    >
      {request.isError && <Alert type="error" showIcon title="Request not sent" description={errorText(request.error)} style={{ marginBottom: 12 }} />}
      <Typography.Paragraph>
        The corrected receipt covers the same installments and amounts ({formatMoney(payment.amountDue)}), dated {payment.paymentDate}, and gets a
        new OR number. Another user must approve.
      </Typography.Paragraph>
      <Space orientation="vertical" style={{ width: '100%' }}>
        <Input aria-label="Corrected payor name" placeholder="Payor name" value={payorName} onChange={(e) => setPayorName(e.target.value)} />
        <Input aria-label="Corrected payor address" placeholder="Payor address" value={payorAddress} onChange={(e) => setPayorAddress(e.target.value)} />
        <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Space>
    </Modal>
  );
}
