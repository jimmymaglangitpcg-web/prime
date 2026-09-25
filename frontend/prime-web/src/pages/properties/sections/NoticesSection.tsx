import { useState } from 'react';
import { Alert, Button, DatePicker, Empty, Form, Input, Modal, Select, Space, Table, Tag, Tooltip, Typography } from 'antd';
import { PlusOutlined, WarningOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useCancelNotice, useGenerateNotice, useIssueNotice, usePropertyNotices, useRecordNoticeService } from '../../../api/notices';
import { useRpuAssessments } from '../../../api/assessments';
import { ApiRequestError } from '../../../lib/apiClient';
import { formatMoney } from '../../../lib/format';
import {
  noticeReasonLabel, serviceModeLabel, type NoticeDto, type NoticeServiceMode, type NoticeStatus, type RpuSummaryDto,
} from '../../../lib/types';
import { PrintFormButton } from '../../../components/PrintFormButton';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const statusColor: Record<NoticeStatus, string> = { Draft: 'default', Issued: 'blue', Served: 'green', Cancelled: 'red' };

/**
 * Notices of Assessment (LGC §223): generated for a posted assessment that is
 * a first assessment or an increase/decrease, issued, then served by one of
 * the three statutory modes with proof — which starts the appeal period (§226).
 */
export function NoticesSection({ propertyId, rpus }: { propertyId: string; rpus: RpuSummaryDto[] }) {
  const { data = [], isLoading } = usePropertyNotices(propertyId);
  const issue = useIssueNotice(propertyId);
  const [generating, setGenerating] = useState(false);
  const [serving, setServing] = useState<NoticeDto | null>(null);
  const [cancelling, setCancelling] = useState<NoticeDto | null>(null);
  const [reason, setReason] = useState('');
  const cancel = useCancelNotice(propertyId);

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12, gap: 8, flexWrap: 'wrap' }}>
        <Typography.Title level={5} style={{ margin: 0 }}>Notices of Assessment</Typography.Title>
        <Button icon={<PlusOutlined />} onClick={() => setGenerating(true)} disabled={rpus.length === 0}>Generate notice</Button>
      </div>
      {issue.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not issue" description={errorText(issue.error)} />}
      <Table<NoticeDto>
        rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: <Empty description="No notices yet" /> }}
        columns={[
          { title: 'No.', dataIndex: 'noticeNumber', render: (v: string | null) => v ?? '—' },
          { title: 'Year', dataIndex: 'assessmentYear' },
          { title: 'Reason', dataIndex: 'reason', render: (r: NoticeDto['reason']) => noticeReasonLabel[r] },
          {
            title: 'Assessed value', align: 'right',
            render: (_, n) => n.previousAssessedValue !== null ? `${formatMoney(n.previousAssessedValue)} → ${formatMoney(n.assessedValue)}` : formatMoney(n.assessedValue),
          },
          { title: 'Addressee', dataIndex: 'addresseeNames', ellipsis: true },
          {
            title: 'Status', render: (_, n) => (
              <Space size={4}>
                <Tag color={statusColor[n.status]}>{n.status}</Tag>
                {n.issueOverdue && <Tooltip title={`Not issued by ${n.issueDueDate} (${n.issuePeriodDays}-day period, LGC §223)`}><Tag color="red" icon={<WarningOutlined />}>Overdue</Tag></Tooltip>}
              </Space>
            ),
          },
          { title: 'Issue by', dataIndex: 'issueDueDate' },
          { title: 'Received', render: (_, n) => n.receivedDate ? `${n.receivedDate} (${serviceModeLabel[n.serviceMode!]})` : '—' },
          { title: 'Appeal until', render: (_, n) => n.appealDeadline ?? '—' },
          {
            title: 'Actions', render: (_, n) => (
              <Space size={4} wrap>
                {n.status === 'Draft' && <Button size="small" type="primary" loading={issue.isPending} onClick={() => issue.mutate(n.id)}>Issue</Button>}
                {n.status === 'Issued' && <Button size="small" type="primary" onClick={() => setServing(n)}>Record service</Button>}
                {n.status !== 'Cancelled' && <PrintFormButton formCode="NOTICE_OF_ASSESSMENT" subjectId={n.id} issuable={n.status === 'Issued' || n.status === 'Served'} />}
                {(n.status === 'Draft' || n.status === 'Issued') && (
                  <Button size="small" danger onClick={() => { setCancelling(n); setReason(''); cancel.reset(); }}>Cancel</Button>
                )}
              </Space>
            ),
          },
        ]}
      />
      <GenerateNoticeModal propertyId={propertyId} rpus={rpus} open={generating} onClose={() => setGenerating(false)} />
      <RecordServiceModal propertyId={propertyId} notice={serving} onClose={() => setServing(null)} />
      <Modal title="Cancel notice" open={cancelling !== null} okText="Cancel notice" cancelText="Back"
        okButtonProps={{ danger: true, disabled: reason.trim() === '', loading: cancel.isPending }} onCancel={() => setCancelling(null)}
        onOk={() => cancelling && cancel.mutate({ id: cancelling.id, reason: reason.trim() }, { onSuccess: () => setCancelling(null) })} destroyOnHidden>
        {cancel.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not cancel" description={errorText(cancel.error)} />}
        <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Modal>
    </div>
  );
}

function GenerateNoticeModal({ propertyId, rpus, open, onClose }: { propertyId: string; rpus: RpuSummaryDto[]; open: boolean; onClose: () => void }) {
  const [rpuId, setRpuId] = useState<string | undefined>(rpus[0]?.id);
  const [assessmentId, setAssessmentId] = useState<string>();
  const { data: assessments = [] } = useRpuAssessments(rpuId);
  const generate = useGenerateNotice(propertyId);
  const posted = assessments.filter((a) => a.status === 'Posted');

  return (
    <Modal title="Generate Notice of Assessment" open={open} okText="Generate draft" okButtonProps={{ disabled: !assessmentId, loading: generate.isPending }}
      onCancel={() => { generate.reset(); onClose(); }}
      onOk={() => assessmentId && generate.mutate(assessmentId, { onSuccess: () => { setAssessmentId(undefined); onClose(); } })} destroyOnHidden>
      <Typography.Paragraph type="secondary">
        Required when property is assessed for the first time or its assessment is increased or decreased (LGC §223).
      </Typography.Paragraph>
      {generate.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="No notice generated" description={errorText(generate.error)} />}
      <Space orientation="vertical" style={{ width: '100%' }}>
        <Select aria-label="RPU" value={rpuId} onChange={(v) => { setRpuId(v); setAssessmentId(undefined); }} style={{ width: '100%' }}
          options={rpus.map((r) => ({ value: r.id, label: `RPU ${r.rpuNumber} (${r.rpuType})` }))} />
        <Select aria-label="Posted assessment" value={assessmentId} onChange={setAssessmentId} style={{ width: '100%' }} placeholder="Posted assessment"
          notFoundContent="No posted assessments for this RPU"
          options={posted.map((a) => ({ value: a.id, label: `${a.assessmentYear} — AV ${formatMoney(a.assessedValue)} (effective ${a.effectiveDate})` }))} />
      </Space>
    </Modal>
  );
}

function RecordServiceModal({ propertyId, notice, onClose }: { propertyId: string; notice: NoticeDto | null; onClose: () => void }) {
  const record = useRecordNoticeService(propertyId);
  const [form] = Form.useForm();
  return (
    <Modal title={notice ? `Record service — ${notice.noticeNumber ?? 'notice'}` : ''} open={notice !== null} footer={null} destroyOnHidden
      onCancel={() => { record.reset(); onClose(); }}>
      <Typography.Paragraph type="secondary">
        The appeal period ({notice?.appealPeriodDays} days, LGC §226) runs from the date of receipt.
      </Typography.Paragraph>
      {record.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not record" description={errorText(record.error)} />}
      <Form form={form} layout="vertical" initialValues={{ receivedDate: dayjs() }}
        onFinish={(v) => notice && record.mutate({
          id: notice.id, serviceMode: v.serviceMode, receivedDate: v.receivedDate.format('YYYY-MM-DD'),
          servedTo: v.servedTo, proofReference: v.proofReference, notes: v.notes,
        }, { onSuccess: () => { form.resetFields(); onClose(); } })}>
        <Form.Item name="serviceMode" label="Mode of service (LGC §223)" rules={[{ required: true }]}>
          <Select options={(Object.keys(serviceModeLabel) as NoticeServiceMode[]).map((m) => ({ value: m, label: serviceModeLabel[m] }))} />
        </Form.Item>
        <Form.Item name="receivedDate" label="Date received" rules={[{ required: true }]}><DatePicker /></Form.Item>
        <Form.Item name="servedTo" label="Received by" rules={[{ required: true }, { max: 300 }]}><Input /></Form.Item>
        <Form.Item name="proofReference" label="Proof of service" rules={[{ required: true }, { max: 200 }]}
          extra="E.g. registry return card no., or the reference of the signed receiving copy.">
          <Input />
        </Form.Item>
        <Form.Item name="notes" label="Notes"><Input.TextArea rows={2} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={record.isPending}>Record service</Button>
      </Form>
    </Modal>
  );
}
