import { useState } from 'react';
import { Alert, Button, DatePicker, Descriptions, Form, Input, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { Link } from 'react-router-dom';
import {
  useAddExemptionEvidence, useApproveExemption, useClaimExemption, useEndExemption, useExemptionTypes, usePropertyExemptions, useRejectExemption,
} from '../../../api/exemptions';
import { useActualUses } from '../../../api/referenceData';
import { ApiRequestError } from '../../../lib/apiClient';
import {
  exemptionStatusLabel, parseExemptionAppliesTo, type ExemptionStatus, type PropertyExemptionDto, type PropertyOwnerDto, type RpuSummaryDto,
} from '../../../lib/types';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const statusColor: Record<ExemptionStatus, string> = { Claimed: 'orange', ProofFiled: 'blue', Approved: 'green', Rejected: 'red', Ended: 'default' };
const day = (d: dayjs.Dayjs | null | undefined) => (d ? d.format('YYYY-MM-DD') : null);

type Action = { kind: 'evidence' | 'approve' | 'reject' | 'end'; claim: PropertyExemptionDto };

/** The claims table with its actions; used on the property profile and the exemption worklist. */
export function ExemptionClaimsTable({ claims, loading, showProperty }: { claims: PropertyExemptionDto[]; loading: boolean; showProperty?: boolean }) {
  const [action, setAction] = useState<Action | null>(null);
  return (
    <>
      <Table<PropertyExemptionDto>
        rowKey="id" size="small" loading={loading} dataSource={claims} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No exemption claims' }}
        expandable={{ expandedRowRender: (c) => <ClaimDetail claim={c} />, rowExpandable: () => true }}
        columns={[
          ...(showProperty ? [{ title: 'Property', render: (_: unknown, c: PropertyExemptionDto) => <Link to={`/properties/${c.propertyId}`}>{c.pin}</Link> }] : []),
          { title: 'Unit', render: (_, c) => `${c.rpuNumber} (${c.rpuType})` },
          { title: 'Exemption', render: (_, c) => <>{c.typeCode} — {c.typeName}<br /><Typography.Text type="secondary">{c.legalBasis}</Typography.Text></> },
          { title: 'Part', render: (_, c) => c.actualUseName ? <>{c.actualUseName}{c.portionDescription && <><br />{c.portionDescription}</>}</> : 'Whole unit' },
          { title: 'Claimed', dataIndex: 'claimedOn' },
          {
            title: 'Proof due', render: (_, c) => (
              <span>{c.proofDueDate}{c.proofOverdue && <> <Tag color="red">overdue — listed as taxable</Tag></>}{c.proofLate && <> <Tag>filed late</Tag></>}</span>
            ),
          },
          { title: 'Status', render: (_, c) => <Tag color={statusColor[c.status]}>{exemptionStatusLabel[c.status]}</Tag> },
          {
            title: 'Effective', render: (_, c) => c.effectiveDate ? `${c.effectiveDate} → ${c.endedOn ?? c.expiryDate ?? 'open'}` : '—',
          },
          {
            title: 'Actions', render: (_, c) => (
              <Space wrap>
                {(c.status === 'Claimed' || c.status === 'ProofFiled') && <Button size="small" onClick={() => setAction({ kind: 'evidence', claim: c })}>File proof</Button>}
                {c.status === 'ProofFiled' && <Button size="small" type="primary" onClick={() => setAction({ kind: 'approve', claim: c })}>Approve</Button>}
                {(c.status === 'Claimed' || c.status === 'ProofFiled') && <Button size="small" danger onClick={() => setAction({ kind: 'reject', claim: c })}>Reject</Button>}
                {c.status === 'Approved' && <Button size="small" onClick={() => setAction({ kind: 'end', claim: c })}>End</Button>}
              </Space>
            ),
          },
        ]}
      />
      <ClaimActionModal action={action} onClose={() => setAction(null)} />
    </>
  );
}

function ClaimDetail({ claim: c }: { claim: PropertyExemptionDto }) {
  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Descriptions size="small" column={2} bordered items={[
        { key: 'ref', label: 'Instrument / reference', children: c.reference ?? '—' },
        { key: 'claimant', label: 'Claimant', children: c.claimantName ?? '—' },
        { key: 'by', label: 'Recorded by', children: `${c.createdByName ?? '—'} — ${dayjs(c.createdAt).format('YYYY-MM-DD')}` },
        { key: 'decided', label: 'Decided by', children: c.decidedByName ? `${c.decidedByName} — ${dayjs(c.decidedAt).format('YYYY-MM-DD')}` : '—' },
        { key: 'decision', label: 'Decision remarks', children: c.decisionRemarks ?? '—' },
        { key: 'end', label: 'End', children: c.endedOn ? `${c.endedOn}: ${c.endReason}` : '—' },
        { key: 'remarks', label: 'Remarks', children: c.remarks ?? '—' },
        {
          key: 'reassessment', label: 'Reassessment',
          children: c.reassessmentId ? 'Opened when the exemption was decided — see the unit’s assessments' : '—',
        },
      ]} />
      <Table size="small" rowKey="sequence" pagination={false} dataSource={c.evidence} locale={{ emptyText: 'No evidence filed' }}
        columns={[
          { title: '#', dataIndex: 'sequence' },
          { title: 'Document', dataIndex: 'description' },
          { title: 'Reference No.', dataIndex: 'referenceNumber', render: (v: string | null) => v ?? '—' },
          { title: 'Dated', dataIndex: 'documentDate', render: (v: string | null) => v ?? '—' },
          { title: 'Received', render: (_, e) => `${e.receivedOn}${e.receivedBy ? ` by ${e.receivedBy}` : ''}` },
        ]} />
      <Typography.Text type="secondary">Evidence is recorded by reference; attaching the scanned file comes with the documents module.</Typography.Text>
    </Space>
  );
}

function ClaimActionModal({ action, onClose }: { action: Action | null; onClose: () => void }) {
  const evidence = useAddExemptionEvidence();
  const approve = useApproveExemption();
  const reject = useRejectExemption();
  const end = useEndExemption();
  const [form] = Form.useForm();
  const [modal, modalContext] = Modal.useModal();
  const mutation = action?.kind === 'evidence' ? evidence : action?.kind === 'approve' ? approve : action?.kind === 'reject' ? reject : end;
  const titles = { evidence: 'File proof of exemption', approve: 'Approve the exemption', reject: 'Reject the claim', end: 'End the exemption' };
  const done = { onSuccess: () => { form.resetFields(); onClose(); } };
  // Approving or ending an exemption of an assessed unit opens a reassessment (Q3): say whether it did.
  const decided = {
    onSuccess: (c: PropertyExemptionDto) => {
      done.onSuccess();
      if (c.reassessmentNote) modal.info({ title: c.reassessmentId ? 'Reassessment opened' : 'No reassessment opened', content: c.reassessmentNote });
    },
  };

  function submit(v: Record<string, never>) {
    if (!action) return;
    const id = action.claim.id;
    const rowVersion = action.claim.rowVersion;
    const a = v as unknown as Record<string, dayjs.Dayjs & string>;
    if (action.kind === 'evidence') evidence.mutate({ id, description: a.description, referenceNumber: a.referenceNumber || null, documentDate: day(a.documentDate), receivedOn: day(a.receivedOn) }, done);
    if (action.kind === 'approve') approve.mutate({ id, effectiveDate: day(a.effectiveDate)!, expiryDate: day(a.expiryDate), remarks: a.remarks || null, rowVersion }, decided);
    if (action.kind === 'reject') reject.mutate({ id, reason: a.reason, rowVersion }, done);
    if (action.kind === 'end') end.mutate({ id, endedOn: day(a.endedOn)!, reason: a.reason, rowVersion }, decided);
  }

  return (
    <>
    {modalContext}
    <Modal title={action ? titles[action.kind] : ''} open={action !== null} footer={null} destroyOnHidden onCancel={() => { mutation.reset(); onClose(); }}>
      {action && <Typography.Paragraph type="secondary">{action.claim.typeCode} — {action.claim.typeName} on {action.claim.rpuNumber}</Typography.Paragraph>}
      {mutation.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not saved" description={errorText(mutation.error)} />}
      <Form form={form} layout="vertical" onFinish={submit}
        initialValues={{ receivedOn: dayjs(), effectiveDate: action ? dayjs(action.claim.claimedOn) : undefined, endedOn: dayjs() }}>
        {action?.kind === 'evidence' && <>
          <Form.Item name="description" label="Document" rules={[{ required: true }]}><Input maxLength={500} placeholder="e.g. certificate of registration, title, lease contract" /></Form.Item>
          <Space wrap>
            <Form.Item name="referenceNumber" label="Reference No."><Input maxLength={100} /></Form.Item>
            <Form.Item name="documentDate" label="Dated"><DatePicker /></Form.Item>
            <Form.Item name="receivedOn" label="Received" rules={[{ required: true }]}><DatePicker /></Form.Item>
          </Space>
        </>}
        {action?.kind === 'approve' && <>
          <Alert type="info" showIcon style={{ marginBottom: 12 }} title="Approved by a second user"
            description="Whoever recorded the claim cannot approve it. Assessments made while it is in force mark the unit (or its part) exempt; if the unit is already assessed, a draft reassessment with the same values is opened, and posting it prepares the replacing TD." />
          <Space wrap>
            <Form.Item name="effectiveDate" label="Effective from" rules={[{ required: true }]}><DatePicker /></Form.Item>
            <Form.Item name="expiryDate" label="Expires (optional)"><DatePicker /></Form.Item>
          </Space>
          <Form.Item name="remarks" label="Remarks"><Input maxLength={1000} /></Form.Item>
        </>}
        {action?.kind === 'end' && <Form.Item name="endedOn" label="Ended on" rules={[{ required: true }]}><DatePicker /></Form.Item>}
        {(action?.kind === 'reject' || action?.kind === 'end') && (
          <Form.Item name="reason" label="Reason" rules={[{ required: true, whitespace: true }]}><Input.TextArea maxLength={1000} rows={3} /></Form.Item>
        )}
        <Button type="primary" htmlType="submit" danger={action?.kind === 'reject'} loading={mutation.isPending}>Save</Button>
      </Form>
    </Modal>
    </>
  );
}

/** The property's exemption claims (CLAUDE.md §43). */
export function ExemptionsSection({ propertyId, rpus, owners }: { propertyId: string; rpus: RpuSummaryDto[]; owners: PropertyOwnerDto[] }) {
  const { data = [], isLoading } = usePropertyExemptions(propertyId);
  const { data: types = [] } = useExemptionTypes(true);
  const { data: uses = [] } = useActualUses();
  const claim = useClaimExemption();
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();
  const rpuId = Form.useWatch('rpuId', form);
  const rpu = rpus.find((r) => r.id === rpuId);
  const applicable = types.filter((t) => !rpu || parseExemptionAppliesTo(t.appliesTo).includes(rpu.rpuType === 'MineralRight' ? 'OtherImprovement' : rpu.rpuType));

  return (
    <div>
      <Space style={{ marginBottom: 12 }} wrap>
        <Button icon={<PlusOutlined />} onClick={() => setOpen(true)} disabled={rpus.length === 0}>Record a claim</Button>
        <Typography.Text type="secondary">A claimed unit stays listed as taxable until its proof is filed and the exemption approved (LGC §206).</Typography.Text>
      </Space>
      <ExemptionClaimsTable claims={data} loading={isLoading} />
      <Modal title="Record an exemption claim" open={open} footer={null} width={640} destroyOnHidden onCancel={() => { claim.reset(); setOpen(false); }}>
        {claim.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not recorded" description={errorText(claim.error)} />}
        {types.length === 0 && <Alert type="info" showIcon style={{ marginBottom: 12 }} title="No exemption type is in force" description="Load and approve the exemption types first (Forms & Numbering)." />}
        <Form form={form} layout="vertical" initialValues={{ claimedOn: dayjs() }}
          onFinish={(v) => claim.mutate({
            rpuId: v.rpuId, exemptionTypeId: v.exemptionTypeId, actualUseId: v.actualUseId ?? null, portionDescription: v.portionDescription || null,
            claimantTaxpayerId: v.claimantTaxpayerId ?? null, claimedOn: day(v.claimedOn), reference: v.reference || null, remarks: v.remarks || null,
          }, { onSuccess: () => { form.resetFields(); setOpen(false); } })}>
          <Form.Item name="rpuId" label="Unit" rules={[{ required: true }]}>
            <Select options={rpus.map((r) => ({ value: r.id, label: `${r.rpuNumber} (${r.rpuType})` }))} />
          </Form.Item>
          <Form.Item name="exemptionTypeId" label="Exemption" rules={[{ required: true }]}>
            <Select options={applicable.map((t) => ({ value: t.id, label: `${t.code} — ${t.name} (${t.legalBasis})` }))} />
          </Form.Item>
          <Space wrap>
            <Form.Item name="actualUseId" label="Part in this actual use (empty: whole unit)" style={{ minWidth: 280 }}>
              <Select allowClear options={uses.map((u) => ({ value: u.id, label: u.name }))} />
            </Form.Item>
            <Form.Item name="claimedOn" label="Date of declaration" rules={[{ required: true }]}><DatePicker /></Form.Item>
          </Space>
          <Form.Item name="portionDescription" label="Description of the exempt part"><Input maxLength={500} /></Form.Item>
          <Form.Item name="claimantTaxpayerId" label="Claimant">
            <Select allowClear options={owners.filter((o) => o.taxpayerId).map((o) => ({ value: o.taxpayerId!, label: `${o.taxpayerDisplayName} (${o.role})` }))} />
          </Form.Item>
          <Form.Item name="reference" label="Instrument / reference"><Input maxLength={500} placeholder="e.g. registration, CADT, lease or charter number" /></Form.Item>
          <Form.Item name="remarks" label="Remarks"><Input maxLength={1000} /></Form.Item>
          <Button type="primary" htmlType="submit" loading={claim.isPending} disabled={types.length === 0}>Record claim</Button>
        </Form>
      </Modal>
    </div>
  );
}
