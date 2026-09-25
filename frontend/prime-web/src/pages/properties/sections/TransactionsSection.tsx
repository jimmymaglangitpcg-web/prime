import { useState } from 'react';
import { TaxClearanceModal } from '../modals/DescriptionModals';
import {
  Alert, Button, DatePicker, Descriptions, Drawer, Empty, Form, Input, InputNumber, Modal, Select, Space, Table, Tag, Typography,
} from 'antd';
import { CheckOutlined, MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  useOpenTransaction, usePropertyTransactions, useSatisfyRequirement, useTransactionAction, useTransactionTypes, type TransactionAction,
} from '../../../api/transactions';
import { useTaxpayerSearch } from '../../../api/taxpayers';
import { useOwnershipTypes } from '../../../api/referenceData';
import { ApiRequestError } from '../../../lib/apiClient';
import {
  partyRoleLabel, type PropertyPartyRole, type PropertyTransactionDto, type RpuSummaryDto, type TaxDeclarationSummaryDto,
  type TransactionRequirementStatusDto, type WorkflowStatus,
} from '../../../lib/types';
import { AddTaxDeclarationModal } from '../modals/AddTaxDeclarationModal';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const statusColor: Partial<Record<WorkflowStatus, string>> = { Draft: 'default', PendingReview: 'gold', Approved: 'green', Rejected: 'red', Cancelled: 'red' };
const statusTag = (s: WorkflowStatus) => <Tag color={statusColor[s]}>{s}</Tag>;

/** Searchable taxpayer picker (min. 2 characters). */
function TaxpayerPicker({ value, onChange }: { value?: string; onChange?: (v: string) => void }) {
  const [term, setTerm] = useState('');
  const { data, isFetching } = useTaxpayerSearch({ searchTerm: term, pageSize: 10 }, { enabled: term.length >= 2 });
  return (
    <Select showSearch value={value} onChange={onChange} filterOption={false} onSearch={setTerm} loading={isFetching} style={{ width: 260 }}
      placeholder="Search taxpayer" notFoundContent={term.length >= 2 ? 'No taxpayers found' : 'Type to search'}
      options={data?.items.map((t) => ({ value: t.id, label: t.displayName }))} />
  );
}

/**
 * Property transactions (CLAUDE.md §34–§37; docs/FORMS-REVISION-PLAN.md A5).
 * A transaction collects its prerequisites, the TDs it issues and cancels and,
 * for a transfer, the new parties; approval applies them all at once.
 */
export function TransactionsSection({ propertyId, rpus, taxDeclarations }: {
  propertyId: string;
  rpus: RpuSummaryDto[];
  taxDeclarations: TaxDeclarationSummaryDto[];
}) {
  const { data = [], isLoading } = usePropertyTransactions(propertyId);
  const [openNew, setOpenNew] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = data.find((t) => t.id === selectedId) ?? null;

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <Typography.Title level={5} style={{ margin: 0 }}>Transactions</Typography.Title>
        <Button icon={<PlusOutlined />} onClick={() => setOpenNew(true)}>New transaction</Button>
      </div>
      <Table<PropertyTransactionDto>
        rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: <Empty description="No transactions yet" /> }}
        onRow={(t) => ({ onClick: () => setSelectedId(t.id), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'No.', dataIndex: 'transactionNumber', render: (v: string | null) => v ?? '—' },
          { title: 'Type', render: (_, t) => `${t.typeCode} — ${t.typeName}` },
          { title: 'Effective', dataIndex: 'effectiveDate' },
          { title: 'Description', dataIndex: 'description', ellipsis: true },
          { title: 'Status', dataIndex: 'status', render: statusTag },
          { title: '', render: (_, t) => <Button size="small" onClick={(e) => { e.stopPropagation(); setSelectedId(t.id); }}>Open</Button> },
        ]}
      />
      <NewTransactionModal propertyId={propertyId} rpus={rpus} taxDeclarations={taxDeclarations} open={openNew}
        onClose={(createdId) => { setOpenNew(false); if (createdId) setSelectedId(createdId); }} />
      <TransactionDrawer tx={selected} propertyId={propertyId} rpus={rpus} onClose={() => setSelectedId(null)} />
    </div>
  );
}

function NewTransactionModal({ propertyId, rpus, taxDeclarations, open, onClose }: {
  propertyId: string;
  rpus: RpuSummaryDto[];
  taxDeclarations: TaxDeclarationSummaryDto[];
  open: boolean;
  onClose: (createdId?: string) => void;
}) {
  const { data: types = [] } = useTransactionTypes(true);
  const { data: ownershipTypes = [] } = useOwnershipTypes();
  const openTx = useOpenTransaction(propertyId);
  const [form] = Form.useForm();
  const typeId = Form.useWatch('transactionTypeId', form);
  const kind = types.find((t) => t.id === typeId)?.kind;

  return (
    <Modal title="New property transaction (Draft)" open={open} footer={null} width={760} destroyOnHidden onCancel={() => { openTx.reset(); onClose(); }}>
      {types.length === 0 && (
        <Alert type="info" showIcon style={{ marginBottom: 12 }} title="No transaction types are in force"
          description="Transaction types (codes, prerequisites) are configured under Forms & Numbering → Transaction types, and approved by a second user." />
      )}
      {openTx.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not open" description={errorText(openTx.error)} />}
      <Form form={form} layout="vertical" initialValues={{ effectiveDate: dayjs(), newParties: [] }}
        onFinish={(v) => openTx.mutate({
          transactionTypeId: v.transactionTypeId, propertyId, effectiveDate: v.effectiveDate.format('YYYY-MM-DD'), description: v.description,
          cancelTaxDeclarationIds: v.cancelTaxDeclarationIds,
          transferRpuId: kind === 'Transfer' ? v.transferRpuId : undefined,
          newParties: kind === 'Transfer'
            ? (v.newParties ?? []).map((p: { role: PropertyPartyRole; taxpayerId?: string; ownershipTypeId?: string; ownershipPercentage?: number }) => ({
                role: p.role,
                taxpayerId: p.role === 'UnknownOwner' ? undefined : p.taxpayerId,
                ownershipTypeId: p.role === 'Owner' ? p.ownershipTypeId : undefined,
                ownershipPercentage: p.role === 'UnknownOwner' ? 0 : p.ownershipPercentage ?? 0,
              }))
            : undefined,
        }, { onSuccess: (created) => { form.resetFields(); onClose(created.id); } })}>
        <Form.Item name="transactionTypeId" label="Type" rules={[{ required: true }]}>
          <Select options={types.map((t) => ({ value: t.id, label: `${t.code} — ${t.name} (${t.kind})` }))} />
        </Form.Item>
        <Space wrap>
          <Form.Item name="effectiveDate" label="Effective date" rules={[{ required: true }]}><DatePicker /></Form.Item>
        </Space>
        <Form.Item name="description" label="Description" rules={[{ required: true }, { max: 2000 }]}><Input.TextArea rows={2} /></Form.Item>
        <Form.Item name="cancelTaxDeclarationIds" label="TDs this transaction cancels outright (optional)"
          extra="A TD that a new TD replaces is cancelled through the new TD's “Replaces TD” — list here only TDs cancelled with no successor.">
          <Select mode="multiple" allowClear
            options={taxDeclarations.filter((t) => t.status === 'Approved').map((t) => ({ value: t.id, label: t.taxDeclarationNumber }))} />
        </Form.Item>
        {kind === 'Transfer' && (
          <>
            {rpus.some((r) => r.rpuType !== 'Land') && (
              <Form.Item name="transferRpuId" label="Transfers"
                extra="A building or machinery can change hands without the land: only that unit's owners change.">
                <Select allowClear placeholder="Whole property"
                  options={rpus.filter((r) => r.rpuType !== 'Land').map((r) => ({ value: r.id, label: `RPU ${r.rpuNumber} (${r.rpuType}) only` }))} />
              </Form.Item>
            )}
            <Typography.Text strong>New parties — the current owners are ended on approval</Typography.Text>
            <Form.List name="newParties">
              {(fields, { add, remove }) => (
                <>
                  {fields.map((field) => (
                    <Form.Item key={field.key} noStyle shouldUpdate>
                      {() => {
                        const role: PropertyPartyRole = form.getFieldValue(['newParties', field.name, 'role']) ?? 'Owner';
                        return (
                          <Space align="baseline" wrap style={{ display: 'flex' }}>
                            <Form.Item name={[field.name, 'role']} initialValue="Owner">
                              <Select style={{ width: 170 }} options={(['Owner', 'Administrator', 'LegalInterestHolder', 'BeneficialUser', 'Claimant', 'UnknownOwner'] as PropertyPartyRole[])
                                .map((r) => ({ value: r, label: partyRoleLabel[r] }))} />
                            </Form.Item>
                            {role !== 'UnknownOwner' && (
                              <Form.Item name={[field.name, 'taxpayerId']} rules={[{ required: true, message: 'Taxpayer' }]}><TaxpayerPicker /></Form.Item>
                            )}
                            {role === 'Owner' && (
                              <>
                                <Form.Item name={[field.name, 'ownershipTypeId']} rules={[{ required: true, message: 'Type' }]}>
                                  <Select style={{ width: 150 }} placeholder="Ownership type" options={ownershipTypes.map((o) => ({ value: o.id, label: o.name }))} />
                                </Form.Item>
                                <Form.Item name={[field.name, 'ownershipPercentage']} rules={[{ required: true, message: 'Share' }]}>
                                  <InputNumber min={0.01} max={100} suffix="%" style={{ width: 120 }} />
                                </Form.Item>
                              </>
                            )}
                            <MinusCircleOutlined aria-label="Remove party" onClick={() => remove(field.name)} />
                          </Space>
                        );
                      }}
                    </Form.Item>
                  ))}
                  <Button type="dashed" icon={<PlusOutlined />} onClick={() => add({ role: 'Owner' })} style={{ marginBottom: 12 }}>Add party</Button>
                </>
              )}
            </Form.List>
          </>
        )}
        <div><Button type="primary" htmlType="submit" loading={openTx.isPending} disabled={types.length === 0}>Open transaction</Button></div>
      </Form>
    </Modal>
  );
}

function TransactionDrawer({ tx, propertyId, rpus, onClose }: {
  tx: PropertyTransactionDto | null;
  propertyId: string;
  rpus: RpuSummaryDto[];
  onClose: () => void;
}) {
  const action = useTransactionAction(propertyId);
  const satisfy = useSatisfyRequirement(propertyId);
  const [modal, modalContext] = Modal.useModal();
  const [asking, setAsking] = useState<'reject' | 'withdraw' | null>(null);
  const [reason, setReason] = useState('');
  const [satisfying, setSatisfying] = useState<TransactionRequirementStatusDto | null>(null);
  const [evidence, setEvidence] = useState('');
  const [tdRpuId, setTdRpuId] = useState<string | undefined>(rpus[0]?.id);
  const [addTdOpen, setAddTdOpen] = useState(false);
  const [clearanceOpen, setClearanceOpen] = useState(false);
  if (!tx) return null;
  const editable = tx.status === 'Draft' || tx.status === 'PendingReview';

  function confirm(kind: TransactionAction, title: string, content?: string) {
    action.reset();
    // A failure shows in the drawer's alert; swallow it here so the dialog closes instead of staying open over it.
    modal.confirm({ title, content, onOk: () => action.mutateAsync({ id: tx!.id, action: kind }).catch(() => undefined) });
  }

  return (
    <Drawer open title={`${tx.transactionNumber ?? 'Transaction'} — ${tx.typeCode} ${tx.typeName}`} onClose={onClose} size={Math.min(820, window.innerWidth)}>
      {modalContext}
      {action.isError && !asking && <Alert type="error" showIcon closable style={{ marginBottom: 12 }} title="Action failed" description={errorText(action.error)} />}
      <Descriptions size="small" bordered column={{ xs: 1, md: 2 }} style={{ marginBottom: 12 }}>
        <Descriptions.Item label="Status">{statusTag(tx.status)}</Descriptions.Item>
        <Descriptions.Item label="Kind">{tx.kind}</Descriptions.Item>
        <Descriptions.Item label="Effective">{tx.effectiveDate}</Descriptions.Item>
        <Descriptions.Item label="Property">
          {tx.propertyIdentificationNumber}
          {tx.transferRpuId && ` — RPU ${rpus.find((r) => r.id === tx.transferRpuId)?.rpuNumber ?? ''} only`}
        </Descriptions.Item>
        <Descriptions.Item label="Description" span={2}>{tx.description}</Descriptions.Item>
        {tx.closeReason && <Descriptions.Item label="Closed" span={2}>{tx.closeReason}</Descriptions.Item>}
        {tx.kind === 'Transfer' && (
          <Descriptions.Item label="BIR clearance (CAR)" span={2}>
            {tx.taxClearance?.carNumber ? `CAR ${tx.taxClearance.carNumber}${tx.taxClearance.carDate ? ` of ${tx.taxClearance.carDate}` : ''}` : 'Not recorded'}
            {editable && <Button size="small" style={{ marginLeft: 8 }} onClick={() => setClearanceOpen(true)}>{tx.taxClearance ? 'Edit' : 'Record'}</Button>}
          </Descriptions.Item>
        )}
      </Descriptions>
      {clearanceOpen && (
        <TaxClearanceModal propertyId={propertyId} transactionId={tx.id} value={tx.taxClearance} onClose={() => setClearanceOpen(false)} />
      )}

      <Space wrap style={{ marginBottom: 16 }}>
        {tx.status === 'Draft' && <Button type="primary" onClick={() => confirm('submit', 'Submit for review?', 'Mandatory requirements must be satisfied. Its TDs go for review with it.')}>Submit</Button>}
        {tx.status === 'PendingReview' && (
          <Button type="primary" onClick={() => confirm('approve', 'Approve this transaction?',
            'Approval applies it: its TDs are approved (cancelling the TDs they replace), listed TDs are cancelled' + (tx.kind === 'Transfer' ? ', and the current owners are replaced by the new parties.' : '.'))}>
            Approve
          </Button>
        )}
        {tx.status === 'PendingReview' && <Button danger onClick={() => { setAsking('reject'); setReason(''); action.reset(); }}>Reject</Button>}
        {editable && <Button onClick={() => { setAsking('withdraw'); setReason(''); action.reset(); }}>Withdraw</Button>}
      </Space>

      <Typography.Title level={5}>Requirements</Typography.Title>
      <Table<TransactionRequirementStatusDto>
        rowKey="id" size="small" dataSource={tx.requirements} pagination={false} locale={{ emptyText: 'No prerequisites for this type' }}
        columns={[
          { title: '#', dataIndex: 'sequence', width: 40 },
          { title: 'Requirement', render: (_, r) => <span>{r.label}{r.isMandatory ? <Tag style={{ marginLeft: 6 }}>Mandatory</Tag> : null}{r.legalBasis && <Typography.Text type="secondary"> ({r.legalBasis})</Typography.Text>}</span> },
          {
            title: 'Evidence', render: (_, r) => r.satisfiedAt
              ? <span><CheckOutlined style={{ color: 'green' }} /> {r.evidenceReference}</span>
              : editable && <Button size="small" onClick={() => { setSatisfying(r); setEvidence(''); satisfy.reset(); }}>Mark satisfied</Button>,
          },
        ]}
      />

      <Typography.Title level={5} style={{ marginTop: 16 }}>Tax Declarations issued by this transaction</Typography.Title>
      <Table rowKey="taxDeclarationId" size="small" dataSource={tx.issuedTaxDeclarations} pagination={false} locale={{ emptyText: 'None yet' }}
        columns={[{ title: 'TD No.', dataIndex: 'taxDeclarationNumber' }, { title: 'Status', dataIndex: 'status', render: statusTag }]} />
      {tx.status === 'Draft' && rpus.length > 0 && (
        <Space style={{ marginTop: 8 }} wrap>
          <Select value={tdRpuId} onChange={setTdRpuId} style={{ width: 220 }} options={rpus.map((r) => ({ value: r.id, label: `RPU ${r.rpuNumber}` }))} />
          <Button icon={<PlusOutlined />} onClick={() => setAddTdOpen(true)} disabled={!tdRpuId}>Add TD</Button>
        </Space>
      )}
      {tdRpuId && (
        <AddTaxDeclarationModal propertyId={propertyId} rpuId={tdRpuId} transactionId={tx.id} open={addTdOpen} onClose={() => setAddTdOpen(false)} />
      )}

      {tx.cancelledTaxDeclarations.length > 0 && (
        <>
          <Typography.Title level={5} style={{ marginTop: 16 }}>Tax Declarations cancelled outright</Typography.Title>
          <Table rowKey="taxDeclarationId" size="small" dataSource={tx.cancelledTaxDeclarations} pagination={false}
            columns={[{ title: 'TD No.', dataIndex: 'taxDeclarationNumber' }, { title: 'Status', dataIndex: 'status', render: statusTag }]} />
        </>
      )}

      {tx.kind === 'Transfer' && (
        <>
          <Typography.Title level={5} style={{ marginTop: 16 }}>New parties</Typography.Title>
          <Table rowKey="id" size="small" dataSource={tx.newParties} pagination={false} locale={{ emptyText: 'None named' }}
            columns={[
              { title: 'Name', dataIndex: 'name' },
              { title: 'Capacity', dataIndex: 'role', render: (r: PropertyPartyRole) => partyRoleLabel[r] },
              { title: 'Share', render: (_, p) => (p.role === 'Owner' ? `${p.ownershipPercentage}%` : '—') },
            ]} />
        </>
      )}

      <Modal title="Mark requirement satisfied" open={satisfying !== null} okText="Save" onCancel={() => setSatisfying(null)}
        okButtonProps={{ disabled: evidence.trim() === '', loading: satisfy.isPending }}
        onOk={() => satisfying && satisfy.mutate({ id: tx.id, requirementId: satisfying.id, evidenceReference: evidence.trim() }, { onSuccess: () => setSatisfying(null) })}
        destroyOnHidden>
        {satisfy.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not save" description={errorText(satisfy.error)} />}
        <Typography.Paragraph>{satisfying?.label}</Typography.Paragraph>
        <Input aria-label="Evidence reference" placeholder="Evidence reference, e.g. official receipt or certificate no." value={evidence} onChange={(e) => setEvidence(e.target.value)} />
      </Modal>

      <Modal title={asking === 'reject' ? 'Reject transaction' : 'Withdraw transaction'} open={asking !== null}
        okText={asking === 'reject' ? 'Reject' : 'Withdraw'} okButtonProps={{ danger: true, disabled: reason.trim() === '', loading: action.isPending }}
        onCancel={() => setAsking(null)}
        onOk={() => asking && action.mutate({ id: tx.id, action: asking, reason: reason.trim() }, { onSuccess: () => setAsking(null) })}
        destroyOnHidden>
        {action.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Action failed" description={errorText(action.error)} />}
        <Typography.Paragraph>The transaction stays on record; its TDs, which never took effect, are rejected with it.</Typography.Paragraph>
        <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Modal>
    </Drawer>
  );
}
