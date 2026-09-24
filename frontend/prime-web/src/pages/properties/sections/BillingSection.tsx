import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Button, Descriptions, Empty, Input, Modal, Space, Table, Tag, Typography } from 'antd';
import { FileTextOutlined, PlusOutlined } from '@ant-design/icons';
import { useCancelBill, usePostBill, usePropertyBills } from '../../../api/bills';
import { ApiRequestError } from '../../../lib/apiClient';
import { formatMoney } from '../../../lib/format';
import type { BillingComponent, RpuSummaryDto, TaxBillDetailDto, TaxBillDto, WorkflowStatus } from '../../../lib/types';
import { GenerateBillModal } from '../modals/GenerateBillModal';
import { PrintFormButton } from '../../../components/PrintFormButton';

const statusColor: Partial<Record<WorkflowStatus, string>> = { Draft: 'default', Posted: 'green', Cancelled: 'red' };
const componentColor: Record<BillingComponent, string> = { Tax: 'blue', Discount: 'green', Penalty: 'orange', Interest: 'volcano' };

const errorMessage = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);

/**
 * CLAUDE.md §52 billing view on the Property Profile: every bill (Draft,
 * Posted, Cancelled — history is never hidden), its per-tax-type annual tax
 * with any cap, and each line's rule-based explanation (§31).
 */
export function BillingSection({ propertyId, rpus }: { propertyId: string; rpus: RpuSummaryDto[] }) {
  const navigate = useNavigate();
  const [modal, modalContext] = Modal.useModal();
  const [generateOpen, setGenerateOpen] = useState(false);
  const [cancelling, setCancelling] = useState<TaxBillDto | null>(null);
  const [reason, setReason] = useState('');
  const { data: bills = [], isLoading, isError, error } = usePropertyBills(propertyId);
  const postBill = usePostBill(propertyId);
  const cancelBill = useCancelBill(propertyId);

  function confirmPost(bill: TaxBillDto) {
    modal.confirm({
      title: `Post the ${bill.taxYear} bill for ${bill.rpuNumber}?`,
      content: `Total ${formatMoney(bill.total)}. Any bill already posted for this RPU and tax year will be cancelled as superseded.`,
      okText: 'Post bill',
      onOk: () => postBill.mutateAsync(bill.id),
    });
  }

  return (
    <div>
      {modalContext}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, flexWrap: 'wrap', marginBottom: 12 }}>
        <Typography.Title level={5} style={{ margin: 0 }}>
          Tax Bills
        </Typography.Title>
        <Space wrap>
          <Button icon={<FileTextOutlined />} onClick={() => navigate(`/properties/${propertyId}/statement-of-account`)}>
            Statement of Account
          </Button>
          <Button icon={<PlusOutlined />} disabled={rpus.length === 0} onClick={() => setGenerateOpen(true)}>
            Generate Bill
          </Button>
        </Space>
      </div>

      {isError && <Alert type="error" showIcon title="Could not load bills" description={errorMessage(error)} style={{ marginBottom: 12 }} />}
      {postBill.isError && <Alert type="error" showIcon title="Could not post bill" description={errorMessage(postBill.error)} style={{ marginBottom: 12 }} />}

      <Table<TaxBillDto>
        rowKey="id"
        loading={isLoading}
        dataSource={bills}
        pagination={false}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: <Empty description="No bills generated yet" /> }}
        expandable={{ expandedRowRender: (bill) => <BillBreakdown bill={bill} /> }}
        columns={[
          { title: 'Tax Year', dataIndex: 'taxYear' },
          { title: 'Bill No.', dataIndex: 'billNumber', render: (v: string | null) => v ?? '—' },
          { title: 'RPU', dataIndex: 'rpuNumber' },
          { title: 'TD No.', dataIndex: 'taxDeclarationNumber' },
          { title: 'As of', dataIndex: 'asOfDate' },
          { title: 'Assessed Value', dataIndex: 'assessedValue', align: 'right', render: formatMoney },
          { title: 'Total', dataIndex: 'total', align: 'right', render: (v: number) => <strong>{formatMoney(v)}</strong> },
          { title: 'Status', dataIndex: 'status', render: (v: WorkflowStatus) => <Tag color={statusColor[v]}>{v}</Tag> },
          {
            title: 'Actions',
            key: 'actions',
            render: (_, bill) =>
              bill.status === 'Cancelled' ? null : (
                <Space>
                  <PrintFormButton formCode="TAX_BILL" subjectId={bill.id} issuable={bill.status === 'Posted'} />
                  {bill.status === 'Draft' && (
                    <Button size="small" type="primary" onClick={() => confirmPost(bill)}>
                      Post
                    </Button>
                  )}
                  <Button size="small" danger onClick={() => { setReason(''); cancelBill.reset(); setCancelling(bill); }}>
                    Cancel
                  </Button>
                </Space>
              ),
          },
        ]}
      />

      <GenerateBillModal propertyId={propertyId} rpus={rpus} open={generateOpen} onClose={() => setGenerateOpen(false)} />

      <Modal
        title={cancelling ? `Cancel the ${cancelling.taxYear} bill for ${cancelling.rpuNumber}` : ''}
        open={cancelling !== null}
        okText="Cancel bill"
        okButtonProps={{ danger: true, disabled: reason.trim() === '', loading: cancelBill.isPending }}
        cancelText="Keep bill"
        onCancel={() => setCancelling(null)}
        onOk={() => cancelling && cancelBill.mutate({ billId: cancelling.id, reason: reason.trim() }, { onSuccess: () => setCancelling(null) })}
        destroyOnHidden
      >
        {cancelBill.isError && <Alert type="error" showIcon title="Could not cancel bill" description={errorMessage(cancelBill.error)} style={{ marginBottom: 12 }} />}
        <Typography.Paragraph>The bill is kept in history as Cancelled; a reason is recorded in the audit trail.</Typography.Paragraph>
        <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Modal>
    </div>
  );
}

function BillBreakdown({ bill }: { bill: TaxBillDto }) {
  return (
    <div style={{ display: 'grid', gap: 12 }}>
      <Descriptions size="small" column={{ xs: 1, md: 3 }} bordered>
        <Descriptions.Item label="Rules in force on">{bill.rulesAsOfDate}</Descriptions.Item>
        <Descriptions.Item label="Discount stacking">{bill.discountStackingAllowed ? 'Allowed' : 'Larger discount only'}</Descriptions.Item>
        <Descriptions.Item label="Created">{new Date(bill.createdAt).toLocaleString('en-PH')}</Descriptions.Item>
        {bill.postedAt && <Descriptions.Item label="Posted">{new Date(bill.postedAt).toLocaleString('en-PH')}</Descriptions.Item>}
        {bill.cancellationReason && <Descriptions.Item label="Cancellation reason" span={2}>{bill.cancellationReason}</Descriptions.Item>}
      </Descriptions>

      {bill.notes && <Alert type="warning" showIcon title="Calculation notes" description={<span style={{ whiteSpace: 'pre-line' }}>{bill.notes}</span>} />}

      <Table
        size="small"
        rowKey="taxTypeId"
        dataSource={bill.taxTypes}
        pagination={false}
        scroll={{ x: 'max-content' }}
        columns={[
          { title: 'Tax Type', render: (_, t) => `${t.taxTypeName} (${t.taxTypeCode})` },
          { title: 'Rate %', dataIndex: 'ratePercent', align: 'right' },
          { title: 'Computed Annual Tax', dataIndex: 'computedAnnualTax', align: 'right', render: formatMoney },
          { title: 'Cap Baseline', dataIndex: 'capBaselineTax', align: 'right', render: (v: number | null) => (v === null ? '—' : formatMoney(v)) },
          { title: 'Cap Limit', dataIndex: 'capLimit', align: 'right', render: (v: number | null) => (v === null ? '—' : formatMoney(v)) },
          { title: 'Annual Tax', dataIndex: 'annualTax', align: 'right', render: formatMoney },
        ]}
      />

      <Table<TaxBillDetailDto>
        size="small"
        rowKey="lineNumber"
        dataSource={bill.details}
        pagination={false}
        scroll={{ x: 'max-content' }}
        columns={[
          { title: '#', dataIndex: 'lineNumber' },
          { title: 'Inst.', dataIndex: 'installmentSequence' },
          { title: 'Due', dataIndex: 'dueDate' },
          { title: 'Tax Type', dataIndex: 'taxTypeCode' },
          { title: 'Component', dataIndex: 'component', render: (v: BillingComponent) => <Tag color={componentColor[v]}>{v}</Tag> },
          { title: 'Explanation', dataIndex: 'explanation' },
          { title: 'Amount', dataIndex: 'amount', align: 'right', render: formatMoney },
        ]}
        summary={() => (
          <Table.Summary.Row>
            <Table.Summary.Cell index={0} colSpan={6} align="right"><strong>Total</strong></Table.Summary.Cell>
            <Table.Summary.Cell index={1} align="right"><strong>{formatMoney(bill.total)}</strong></Table.Summary.Cell>
          </Table.Summary.Row>
        )}
      />
    </div>
  );
}
