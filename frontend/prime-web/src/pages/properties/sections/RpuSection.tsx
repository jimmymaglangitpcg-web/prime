import { useState } from 'react';
import { Alert, Badge, Button, Empty, Input, Modal, Space, Table, Tag, Tooltip, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import type { RpuSummaryDto, TaxDeclarationDto } from '../../../lib/types';
import { useTaxDeclarationsByRpu, useTdAction, type TdAction } from '../../../api/taxDeclarations';
import { usePropertyRpus } from '../../../api/rpus';
import { useCan } from '../../../api/offices';
import { ApiRequestError } from '../../../lib/apiClient';
import { TdAnnotationsModal } from '../modals/TdAnnotationsModal';
import { AddRpuModal } from '../modals/AddRpuModal';
import { AddTaxDeclarationModal } from '../modals/AddTaxDeclarationModal';
import { PrintFormButton } from '../../../components/PrintFormButton';
import { PropertyDetailForRpu } from './PropertyDetailForRpu';
import { AssessmentsForRpu } from './AssessmentsForRpu';
import { IndependentAppraisalsForRpu } from './IndependentAppraisalsForRpu';
import { WorkflowStatusTag } from '../../../components/StatusTag';
import { TaxabilityTag } from '../../../components/TaxabilityTag';
import { DocNumber } from '../../../components/DocNumber';


type ReasonAction = 'reject' | 'request-cancellation' | 'reject-cancellation';

/** The dialogs that ask for a reason. */
const reasonDialog: Record<ReasonAction, { title: string; ok: string; text: string }> = {
  reject: { title: 'Reject TD', ok: 'Reject', text: 'The TD stays on record as rejected.' },
  'request-cancellation': {
    title: 'Request cancellation of TD',
    ok: 'Request cancellation',
    text: 'Asks for this declaration to be cancelled outright, with no successor (e.g. a duplicate). '
      + 'Nothing changes until a second user approves the request; the TD then stays on record, marked cancelled.',
  },
  'reject-cancellation': {
    title: 'Reject the cancellation of TD',
    ok: 'Reject request',
    text: 'The TD stays approved. The request is kept with your reason.',
  },
};

/** TDs of one RPU with their lifecycle (docs/FORMS-REVISION-PLAN.md A4): submit → approve/reject → cancel (requested, then decided by a second user); annotations; print. */
/** The MRPAAO FAAS form for a unit type (Att. 1–3; docs/analysis/mrpaao-forms-model.md §13). */
const faasFormFor: Partial<Record<RpuSummaryDto['rpuType'], string>> = { Land: 'FAAS_LAND', Building: 'FAAS_BUILDING', Machinery: 'FAAS_MACHINERY' };

function TaxDeclarationsForRpu({ propertyId, rpuId, rpuType }: { propertyId: string; rpuId: string; rpuType: RpuSummaryDto['rpuType'] }) {
  const { data, isLoading } = useTaxDeclarationsByRpu(rpuId);
  const [addOpen, setAddOpen] = useState(false);
  const [annotating, setAnnotating] = useState<TaxDeclarationDto | null>(null);
  const [asking, setAsking] = useState<{ td: TaxDeclarationDto; action: ReasonAction } | null>(null);
  const [reason, setReason] = useState('');
  const action = useTdAction(propertyId, rpuId);
  const can = useCan();
  const [modal, modalContext] = Modal.useModal();
  const numberOf = (id: string | null) => data?.find((td) => td.id === id)?.taxDeclarationNumber;

  function run(td: TaxDeclarationDto, kind: TdAction) {
    if (kind === 'reject' || kind === 'request-cancellation' || kind === 'reject-cancellation') {
      setReason('');
      action.reset();
      setAsking({ td, action: kind });
      return;
    }
    const replaces = numberOf(td.previousTaxDeclarationId);
    const title = kind === 'approve' ? `Approve TD ${td.taxDeclarationNumber}?`
      : kind === 'approve-cancellation' ? `Cancel TD ${td.taxDeclarationNumber}?`
      : `Submit TD ${td.taxDeclarationNumber} for review?`;
    const content = kind === 'approve' && replaces ? `Approval cancels TD ${replaces}, which this declaration replaces.`
      : kind === 'approve-cancellation' ? `Approving the request cancels this declaration outright. Reason given: ${td.openCancellationRequest?.reason}`
      : undefined;
    modal.confirm({
      title,
      content,
      okButtonProps: kind === 'approve-cancellation' ? { danger: true } : undefined,
      // A failure shows in the alert above the table; let the dialog close rather than stay open over it.
      onOk: () => action.mutateAsync({ id: td.id, action: kind, cancellationRequestId: td.openCancellationRequest?.id, rowVersion: td.rowVersion })
        .catch(() => undefined),
    });
  }

  return (
    <div style={{ padding: '8px 24px' }}>
      {modalContext}
      <Button size="small" icon={<PlusOutlined />} onClick={() => setAddOpen(true)} style={{ marginBottom: 8 }}>
        Add Tax Declaration
      </Button>
      {action.isError && !asking && (
        <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Action failed"
          description={action.error instanceof ApiRequestError ? action.error.apiError.message : (action.error as Error).message} />
      )}
      <Table<TaxDeclarationDto>
        size="small"
        rowKey="id"
        loading={isLoading}
        dataSource={data ?? []}
        pagination={false}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: <Empty description="No Tax Declarations yet" image={Empty.PRESENTED_IMAGE_SIMPLE} /> }}
        columns={[
          { title: 'TD Number', dataIndex: 'taxDeclarationNumber', render: (v: string) => <DocNumber>{v}</DocNumber> },
          {
            title: 'FAAS No.',
            render: (_, td) => td.faasNumber ?? <Tooltip title="Declares no assessment yet — it becomes a FAAS once it does">—</Tooltip>,
          },
          { title: 'Code', dataIndex: 'transactionCode', width: 70, render: (v: string | null) => v ?? '—' },
          { title: 'Revision', dataIndex: 'revisionNumber', width: 90 },
          { title: 'Assessment Year', dataIndex: 'assessmentYear', width: 130 },
          { title: 'Effectivity', dataIndex: 'effectivityDate' },
          { title: 'Replaces', render: (_, td) => numberOf(td.previousTaxDeclarationId) ?? '—' },
          { title: 'Taxability', render: (_, td) => <TaxabilityTag value={td.taxability} /> },
          {
            title: 'Status',
            render: (_, td) => {
              const tag = <WorkflowStatusTag status={td.status} />;
              const note = td.supersededByTaxDeclarationId
                ? `Cancelled by TD ${numberOf(td.supersededByTaxDeclarationId) ?? ''}`
                : td.cancellationReason;
              return note ? <Tooltip title={note}>{tag}</Tooltip> : tag;
            },
          },
          {
            title: 'Actions',
            render: (_, td) => (
              <Space size={4} wrap>
                {td.propertyTransactionId && ['Draft', 'PendingReview'].includes(td.status) && (
                  <Tooltip title="Submitted and approved with its property transaction (Transactions tab)"><Tag color="purple">In transaction</Tag></Tooltip>
                )}
                {!td.propertyTransactionId && td.status === 'Draft' && can('td.prepare') && <Button size="small" onClick={() => run(td, 'submit-for-review')}>Submit</Button>}
                {!td.propertyTransactionId && td.status === 'PendingReview' && can('td.approve') && <Button size="small" type="primary" onClick={() => run(td, 'approve')}>Approve</Button>}
                {!td.propertyTransactionId && td.status === 'PendingReview' && can('td.approve') && <Button size="small" danger onClick={() => run(td, 'reject')}>Reject</Button>}
                {td.status === 'Approved' && !td.openCancellationRequest && can('td.prepare') && (
                  <Button size="small" danger onClick={() => run(td, 'request-cancellation')}>Request cancellation</Button>
                )}
                {td.openCancellationRequest && (
                  <>
                    <Tooltip title={`Reason: ${td.openCancellationRequest.reason}. A second user approves or rejects it.`}>
                      <Tag color="orange">Cancellation requested</Tag>
                    </Tooltip>
                    {can('td.approve') && (
                      <>
                        <Button size="small" danger onClick={() => run(td, 'approve-cancellation')}>Approve cancellation</Button>
                        <Button size="small" onClick={() => run(td, 'reject-cancellation')}>Reject cancellation</Button>
                      </>
                    )}
                  </>
                )}
                <Badge count={td.activeAnnotationCount} size="small">
                  <Button size="small" onClick={() => setAnnotating(td)}>Annotations</Button>
                </Badge>
                <PrintFormButton formCode="TAX_DECLARATION" subjectId={td.id} issuable={td.status !== 'Rejected' && td.status !== 'Voided'} />
                {faasFormFor[rpuType] && td.assessmentId && (
                  // The FAAS is this TD with the assessment it declares; issued once the TD is approved.
                  <PrintFormButton formCode={faasFormFor[rpuType]!} subjectId={td.id}
                    issuable={td.status === 'Approved' || td.status === 'Cancelled'} label="FAAS" />
                )}
              </Space>
            ),
          },
        ]}
      />
      <AddTaxDeclarationModal propertyId={propertyId} rpuId={rpuId} open={addOpen} onClose={() => setAddOpen(false)} />
      <TdAnnotationsModal td={annotating} propertyId={propertyId} onClose={() => setAnnotating(null)} />
      <Modal
        title={asking ? `${reasonDialog[asking.action].title} ${asking.td.taxDeclarationNumber}` : ''}
        open={asking !== null}
        okText={asking ? reasonDialog[asking.action].ok : ''}
        cancelText="Back"
        okButtonProps={{ danger: true, disabled: reason.trim() === '', loading: action.isPending }}
        onCancel={() => setAsking(null)}
        onOk={() => asking && action.mutate({ id: asking.td.id, action: asking.action, reason: reason.trim(),
          cancellationRequestId: asking.td.openCancellationRequest?.id, rowVersion: asking.td.rowVersion }, { onSuccess: () => setAsking(null) })}
        destroyOnHidden
      >
        {action.isError && (
          <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Action failed"
            description={action.error instanceof ApiRequestError ? action.error.apiError.message : (action.error as Error).message} />
        )}
        <Typography.Paragraph>{asking ? reasonDialog[asking.action].text : ''}</Typography.Paragraph>
        <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Modal>
    </div>
  );
}

export function RpuSection({ propertyId, rpus }: { propertyId: string; rpus: RpuSummaryDto[] }) {
  const [addOpen, setAddOpen] = useState(false);
  const { data: details } = usePropertyRpus(propertyId);
  const detail = (id: string) => details?.find((d) => d.id === id);
  const numberOf = (id: string | null) => rpus.find((r) => r.id === id)?.rpuNumber;

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <Typography.Title level={5} style={{ margin: 0 }}>
          Real Property Units
        </Typography.Title>
        <Button icon={<PlusOutlined />} onClick={() => setAddOpen(true)}>
          Add RPU
        </Button>
      </div>

      {/* Fixed layout: the expanded unit is as wide as the table, so its own tables scroll inside it instead of widening the page. */}
      <Table<RpuSummaryDto>
        tableLayout="fixed"
        rowKey="id"
        dataSource={rpus}
        pagination={false}
        locale={{ emptyText: <Empty description="No RPUs registered yet" /> }}
        expandable={{
          expandedRowRender: (rpu) => (
            <>
              <PropertyDetailForRpu propertyId={propertyId} rpu={rpu} />
              <TaxDeclarationsForRpu propertyId={propertyId} rpuId={rpu.id} rpuType={rpu.rpuType} />
              <IndependentAppraisalsForRpu rpu={rpu} />
              <AssessmentsForRpu rpuId={rpu.id} />
            </>
          ),
        }}
        columns={[
          { title: 'RPU Number', dataIndex: 'rpuNumber' },
          { title: 'Type', dataIndex: 'rpuType' },
          {
            title: 'Unit PIN',
            render: (_, r) => {
              const d = detail(r.id);
              if (!d) return '—';
              return d.ownedSeparately
                ? <Tooltip title="Owned apart from the land: the parcel number is in parentheses (MRPAAO p.42)">{d.unitPin}</Tooltip>
                : d.unitPin;
            },
          },
          {
            title: 'Stands on / installed in',
            render: (_, r) => {
              const d = detail(r.id);
              const parts = [
                d?.landRpuId && `Land RPU ${numberOf(d.landRpuId) ?? ''}`,
                d?.hostRpuId && `Building RPU ${numberOf(d.hostRpuId) ?? ''}`,
              ].filter(Boolean);
              return parts.length > 0 ? parts.join(' · ') : '—';
            },
          },
          { title: 'Effectivity', dataIndex: 'effectivityDate' },
          {
            title: 'Status',
            dataIndex: 'status',
            render: (status: string) => <Tag>{status}</Tag>,
          },
        ]}
      />

      <AddRpuModal propertyId={propertyId} rpus={rpus} open={addOpen} onClose={() => setAddOpen(false)} />
    </div>
  );
}
