import { useMemo, useState, type ReactNode } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Alert, Button, Card, DatePicker, Descriptions, Empty, Input, InputNumber, Modal, Result, Select, Skeleton, Space, Table, Tag, Tooltip, Typography,
} from 'antd';
import { ArrowLeftOutlined, CalculatorOutlined, DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { useOutstanding, usePaymentModes, usePostPayment, useQuotePayment } from '../../api/payments';
import { usePropertyProfile } from '../../api/properties';
import { PrintFormButton } from '../../components/PrintFormButton';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import type { BillingComponent, PaymentAllocationDto, PaymentDto, PaymentItemRequest, PaymentQuoteDto } from '../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
const componentColor: Record<BillingComponent, string> = { Tax: 'blue', Discount: 'green', Penalty: 'orange', Interest: 'volcano' };
const cents = (v: number) => Math.round(v * 100) / 100;

interface InstallmentRow {
  key: string;
  rpuId: string;
  rpuNumber: string;
  taxDeclarationNumber: string;
  billNumber: string | null;
  taxYear: number;
  installmentSequence: number;
  dueDate: string;
  principalOwed: number;
  principalPaid: number;
  outstanding: number;
  dueToday: number | null;
  overpaid: boolean;
}

interface TenderRow {
  id: number;
  paymentModeId: string | undefined;
  amount: number | null;
  reference: string;
  bank: string;
  checkDate: string | null;
}

/**
 * The payment workspace (CLAUDE.md §53; docs/analysis/collection.md §6): the
 * property's outstanding installments, a quote as of today (charges follow the
 * principal paid), payor and tenders, a confirmation, then the receipt. The
 * payment date is always today — PRIME does not back-date.
 */
export function PaymentWorkspacePage() {
  const [params] = useSearchParams();
  const propertyId = params.get('propertyId') ?? undefined;
  const navigate = useNavigate();
  const [modal, modalContext] = Modal.useModal();

  const profile = usePropertyProfile(propertyId);
  const outstanding = useOutstanding(propertyId);
  const modes = usePaymentModes();
  const quote = useQuotePayment();
  const post = usePostPayment();

  const [selected, setSelected] = useState<string[]>([]);
  const [partial, setPartial] = useState<Record<string, number | null>>({});
  const [quoted, setQuoted] = useState<PaymentQuoteDto | null>(null);
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID());
  const [payorName, setPayorName] = useState<string | null>(null);
  const [payorAddress, setPayorAddress] = useState<string | null>(null);
  const [payorTaxpayerId, setPayorTaxpayerId] = useState<string | null | undefined>(undefined);
  const [receiptNumber, setReceiptNumber] = useState('');
  const [remarks, setRemarks] = useState('');
  const [tenders, setTenders] = useState<TenderRow[]>([]);
  const [posted, setPosted] = useState<PaymentDto | null>(null);

  const rows: InstallmentRow[] = useMemo(() => (outstanding.data?.bills ?? []).flatMap((b) =>
    b.installments.map((i) => ({
      key: `${b.rpuId}:${b.taxYear}:${i.installmentSequence}`,
      rpuId: b.rpuId, rpuNumber: b.rpuNumber, taxDeclarationNumber: b.taxDeclarationNumber, billNumber: b.billNumber, taxYear: b.taxYear,
      installmentSequence: i.installmentSequence, dueDate: i.dueDate, principalOwed: i.principalOwed, principalPaid: i.principalPaid,
      outstanding: i.outstanding, dueToday: i.dueIfPaidAsOf, overpaid: i.overpaid,
    }))), [outstanding.data]);

  // Payor defaults to the first current owner until the cashier changes it.
  const firstOwner = profile.data?.owners.find((o) => o.isCurrent && o.role === 'Owner');
  const payor = {
    name: payorName ?? firstOwner?.taxpayerDisplayName ?? '',
    address: payorAddress ?? firstOwner?.address ?? '',
    taxpayerId: payorTaxpayerId === undefined ? (firstOwner?.taxpayerId ?? null) : payorTaxpayerId,
  };

  const modeById = new Map((modes.data ?? []).filter((m) => m.isActive).map((m) => [m.id, m]));
  const tendered = cents(tenders.reduce((sum, t) => sum + (t.amount ?? 0), 0));
  const change = quoted ? cents(tendered - quoted.total) : 0;

  function resetQuote() {
    setQuoted(null);
    quote.reset();
    post.reset();
  }

  function items(): PaymentItemRequest[] {
    return rows.filter((r) => selected.includes(r.key)).map((r) => ({
      rpuId: r.rpuId, taxYear: r.taxYear, installmentSequence: r.installmentSequence, principalAmount: partial[r.key] ?? null,
    }));
  }

  function computeQuote() {
    post.reset();
    quote.mutate(items(), {
      onSuccess: (q) => {
        setQuoted(q);
        setIdempotencyKey(crypto.randomUUID());
        const cash = (modes.data ?? []).find((m) => m.isActive && m.allowsChange) ?? (modes.data ?? []).find((m) => m.isActive);
        setTenders([{ id: Date.now(), paymentModeId: cash?.id, amount: q.total, reference: '', bank: '', checkDate: null }]);
      },
    });
  }

  function confirmPost() {
    if (!quoted) return;
    modal.confirm({
      title: 'Post this payment?',
      width: 480,
      content: (
        <Descriptions size="small" column={1} style={{ marginTop: 12 }}>
          <Descriptions.Item label="Payor">{payor.name}</Descriptions.Item>
          <Descriptions.Item label="Amount due">{formatMoney(quoted.total)}</Descriptions.Item>
          <Descriptions.Item label="Tendered">{formatMoney(tendered)}</Descriptions.Item>
          <Descriptions.Item label="Change">{formatMoney(change)}</Descriptions.Item>
        </Descriptions>
      ),
      okText: 'Post payment',
      // Errors show in the alert on the page; let the dialog close rather than stay over it.
      onOk: () => post.mutateAsync({
        idempotencyKey,
        payorTaxpayerId: payor.taxpayerId,
        payorName: payor.name.trim(),
        payorAddress: payor.address.trim() || null,
        items: items(),
        tenders: tenders.map((t) => ({
          paymentModeId: t.paymentModeId!, amount: t.amount ?? 0, reference: t.reference.trim() || null, bank: t.bank.trim() || null, checkDate: t.checkDate,
        })),
        expectedTotal: quoted.total,
        officialReceiptNumber: receiptNumber.trim() || null,
        remarks: remarks.trim() || null,
      }).then(setPosted).catch(() => undefined),
    });
  }

  if (!propertyId) {
    return <Alert type="info" showIcon title="Open a property first" description="Take a payment from a property's Payments tab, or its Statement of Account." />;
  }
  if (profile.isLoading || outstanding.isLoading) {
    return <Skeleton active />;
  }
  if (profile.isError || outstanding.isError) {
    return <Alert type="error" showIcon title="Could not load the property's balance" description={errorText(profile.error ?? outstanding.error)} />;
  }

  if (posted) {
    return (
      <Result
        status="success"
        title={`Payment posted — OR ${posted.officialReceiptNumber}`}
        subTitle={`Transaction ${posted.transactionNumber} · ${formatMoney(posted.amountDue)} received from ${posted.payorName}${posted.change > 0 ? ` · change ${formatMoney(posted.change)}` : ''}`}
        extra={[
          <PrintFormButton key="print" formCode="OFFICIAL_RECEIPT" subjectId={posted.id} issuable label="Print receipt" />,
          <Button key="again" onClick={() => { setPosted(null); setSelected([]); setPartial({}); resetQuote(); }}>Take another payment</Button>,
          <Button key="back" type="primary" onClick={() => navigate(`/properties/${propertyId}`)}>Back to property</Button>,
        ]}
      />
    );
  }

  const quoteProblem = quote.isError ? errorText(quote.error) : null;
  const tendersIncomplete = tenders.length === 0 || tenders.some((t) => !t.paymentModeId || !t.amount || t.amount <= 0
    || (modeById.get(t.paymentModeId)?.requiresReference && t.reference.trim() === ''));

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      {modalContext}
      <Space wrap>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(`/properties/${propertyId}`)}>Back to property</Button>
        <Typography.Title level={3} style={{ margin: 0 }}>Take payment — {profile.data?.property.propertyIdentificationNumber}</Typography.Title>
      </Space>

      <Card title={`1. What is being paid (as of ${outstanding.data?.asOfDate})`}>
        <Table<InstallmentRow>
          rowKey="key"
          size="small"
          dataSource={rows}
          pagination={false}
          scroll={{ x: 'max-content' }}
          locale={{ emptyText: <Empty description="No posted bills. Generate and post a bill on the Billing tab first." /> }}
          rowSelection={{
            selectedRowKeys: selected,
            getCheckboxProps: (r) => ({ disabled: r.outstanding === 0 }),
            onChange: (keys) => { setSelected(keys as string[]); resetQuote(); },
          }}
          columns={[
            { title: 'Tax year', dataIndex: 'taxYear' },
            { title: 'RPU', dataIndex: 'rpuNumber' },
            { title: 'TD No.', dataIndex: 'taxDeclarationNumber' },
            { title: 'Inst.', dataIndex: 'installmentSequence' },
            { title: 'Due', dataIndex: 'dueDate' },
            { title: 'Tax', dataIndex: 'principalOwed', align: 'right', render: formatMoney },
            { title: 'Paid', dataIndex: 'principalPaid', align: 'right', render: (v: number, r) => <>{formatMoney(v)}{r.overpaid && <Tag color="red" style={{ marginLeft: 4 }}>overpaid</Tag>}</> },
            { title: 'Outstanding', dataIndex: 'outstanding', align: 'right', render: formatMoney },
            { title: 'Due today', dataIndex: 'dueToday', align: 'right', render: (v: number | null) => (v === null ? '—' : <strong>{formatMoney(v)}</strong>) },
            {
              title: 'Pay only (tax)',
              key: 'partial',
              render: (_, r) => (
                <InputNumber<number>
                  size="small"
                  aria-label={`Partial amount for ${r.taxYear} installment ${r.installmentSequence}`}
                  placeholder="whole"
                  min={0.01}
                  max={r.outstanding}
                  precision={2}
                  disabled={!selected.includes(r.key)}
                  value={partial[r.key] ?? null}
                  onChange={(v) => { setPartial((p) => ({ ...p, [r.key]: v })); resetQuote(); }}
                />
              ),
            },
          ]}
        />
        <Space style={{ marginTop: 12 }} wrap>
          <Button onClick={() => { setSelected(rows.filter((r) => r.outstanding > 0).map((r) => r.key)); resetQuote(); }} disabled={rows.every((r) => r.outstanding === 0)}>
            Select everything outstanding
          </Button>
          <Button type="primary" icon={<CalculatorOutlined />} disabled={selected.length === 0} loading={quote.isPending} onClick={computeQuote}>
            Compute amount due
          </Button>
          <Typography.Text type="secondary">A partial amount pays that much tax; its penalty and interest are added. Only a whole installment paid on time gets a discount.</Typography.Text>
        </Space>
        {quoteProblem && <Alert type="error" showIcon title="Cannot compute the amount" description={quoteProblem} style={{ marginTop: 12 }} />}
      </Card>

      {quoted && (
        <>
          <Card title={`2. Amount due on ${quoted.paymentDate}: ${formatMoney(quoted.total)}`}>
            <AllocationsTable allocations={quoted.allocations} total={quoted.total} />
          </Card>

          <Card title="3. Payor and payment">
            <Space orientation="vertical" style={{ width: '100%' }}>
              <Space wrap align="end" style={{ width: '100%' }}>
                <Labeled label="Payor">
                  <Input style={{ width: 320 }} aria-label="Payor name" value={payor.name}
                    onChange={(e) => { setPayorName(e.target.value); setPayorTaxpayerId(null); }} />
                </Labeled>
                <Labeled label="Address">
                  <Input style={{ width: 380 }} aria-label="Payor address" value={payor.address} onChange={(e) => setPayorAddress(e.target.value)} />
                </Labeled>
                <Labeled label="OR No. (only for a pre-printed receipt)">
                  <Input style={{ width: 260 }} aria-label="Official receipt number" placeholder="generated" value={receiptNumber}
                    onChange={(e) => setReceiptNumber(e.target.value)} />
                </Labeled>
              </Space>
              {payor.taxpayerId && <Typography.Text type="secondary">Payor is the registered owner; editing the name records it as typed.</Typography.Text>}

              <Table<TenderRow>
                rowKey="id"
                size="small"
                dataSource={tenders}
                pagination={false}
                scroll={{ x: 'max-content' }}
                columns={[
                  {
                    title: 'Mode', key: 'mode', render: (_, t) => (
                      <Select style={{ width: 160 }} aria-label="Mode of payment" value={t.paymentModeId}
                        options={[...modeById.values()].map((m) => ({ value: m.id, label: m.name }))}
                        onChange={(v) => setTenders((ts) => ts.map((x) => (x.id === t.id ? { ...x, paymentModeId: v } : x)))} />
                    ),
                  },
                  {
                    title: 'Amount', key: 'amount', render: (_, t) => (
                      <InputNumber<number> aria-label="Tender amount" min={0.01} precision={2} value={t.amount}
                        onChange={(v) => setTenders((ts) => ts.map((x) => (x.id === t.id ? { ...x, amount: v } : x)))} />
                    ),
                  },
                  {
                    title: 'Reference', key: 'reference', render: (_, t) => (
                      <Input aria-label="Reference" placeholder={t.paymentModeId && modeById.get(t.paymentModeId)?.requiresReference ? 'required' : 'optional'} value={t.reference}
                        onChange={(e) => setTenders((ts) => ts.map((x) => (x.id === t.id ? { ...x, reference: e.target.value } : x)))} />
                    ),
                  },
                  {
                    title: 'Bank', key: 'bank', render: (_, t) => (
                      <Input aria-label="Bank" value={t.bank} onChange={(e) => setTenders((ts) => ts.map((x) => (x.id === t.id ? { ...x, bank: e.target.value } : x)))} />
                    ),
                  },
                  {
                    title: 'Check date', key: 'checkDate', render: (_, t) => (
                      <DatePicker aria-label="Check date" onChange={(d) => setTenders((ts) => ts.map((x) => (x.id === t.id ? { ...x, checkDate: d ? d.format('YYYY-MM-DD') : null } : x)))} />
                    ),
                  },
                  {
                    title: '', key: 'remove', render: (_, t) => (
                      <Button size="small" aria-label="Remove tender" icon={<DeleteOutlined />} disabled={tenders.length === 1}
                        onClick={() => setTenders((ts) => ts.filter((x) => x.id !== t.id))} />
                    ),
                  },
                ]}
              />
              <Space wrap>
                <Button icon={<PlusOutlined />} onClick={() => setTenders((ts) => [...ts, { id: Date.now(), paymentModeId: undefined, amount: null, reference: '', bank: '', checkDate: null }])}>
                  Add mode of payment
                </Button>
                <Typography.Text>Tendered <strong>{formatMoney(tendered)}</strong></Typography.Text>
                <Typography.Text type={change < 0 ? 'danger' : undefined}>
                  {change < 0 ? `Short by ${formatMoney(-change)}` : `Change ${formatMoney(change)}`}
                </Typography.Text>
              </Space>
              <Input.TextArea aria-label="Remarks" placeholder="Remarks (optional)" rows={2} maxLength={1000} value={remarks} onChange={(e) => setRemarks(e.target.value)} />

              {post.isError && <Alert type="error" showIcon title="The payment was not posted" description={errorText(post.error)}
                action={<Button size="small" onClick={computeQuote}>Recompute</Button>} />}
              <Button type="primary" size="large" disabled={payor.name.trim() === '' || tendersIncomplete || change < 0} loading={post.isPending} onClick={confirmPost}>
                Post payment of {formatMoney(quoted.total)}
              </Button>
            </Space>
          </Card>
        </>
      )}
    </Space>
  );
}

function Labeled({ label, children }: { label: string; children: ReactNode }) {
  return (
    <Space orientation="vertical" size={2}>
      <Typography.Text type="secondary">{label}</Typography.Text>
      {children}
    </Space>
  );
}

/** The allocation lines of a quote or payment, each coded to its revenue account (eOR §7.1). */
export function AllocationsTable({ allocations, total }: { allocations: PaymentAllocationDto[]; total: number }) {
  return (
    <Table<PaymentAllocationDto>
      rowKey="lineNumber"
      size="small"
      dataSource={allocations}
      pagination={false}
      scroll={{ x: 'max-content' }}
      columns={[
        { title: 'Tax year', dataIndex: 'taxYear', render: (v: number, a) => <>{v} <Tag>{a.yearCategory}</Tag></> },
        { title: 'TD No.', dataIndex: 'taxDeclarationNumber' },
        { title: 'Inst.', dataIndex: 'installmentSequence' },
        { title: 'Tax type', dataIndex: 'taxTypeCode' },
        { title: 'Component', dataIndex: 'component', render: (v: BillingComponent) => <Tag color={componentColor[v]}>{v}</Tag> },
        { title: 'Explanation', dataIndex: 'explanation' },
        { title: 'Account', key: 'account', render: (_, a) => <Tooltip title={`${a.accountName}${a.fund ? ` · ${a.fund}` : ''}`}><span>{a.accountCode}</span></Tooltip> },
        { title: 'Amount', dataIndex: 'amount', align: 'right', render: formatMoney },
      ]}
      summary={() => (
        <Table.Summary.Row>
          <Table.Summary.Cell index={0} colSpan={7} align="right"><strong>Total</strong></Table.Summary.Cell>
          <Table.Summary.Cell index={1} align="right"><strong>{formatMoney(total)}</strong></Table.Summary.Cell>
        </Table.Summary.Row>
      )}
    />
  );
}
