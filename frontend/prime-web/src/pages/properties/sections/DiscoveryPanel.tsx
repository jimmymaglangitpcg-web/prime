import { useState } from 'react';
import { Alert, Button, DatePicker, Form, Input, Modal, Select, Space, Table, Tag, Tooltip, Typography } from 'antd';
import { PlusOutlined, WarningOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useDiscovery, useIssueSummons, useRecordSummonsOutcome, useRecordSummonsService, useRecordVerification } from '../../../api/transactions';
import { ApiRequestError } from '../../../lib/apiClient';
import { type DiscoverySummonsDto, type NoticeServiceMode, type SummonsOutcome, serviceModeLabel } from '../../../lib/types';
import { PrintFormButton } from '../../../components/PrintFormButton';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const outcomeTag: Record<SummonsOutcome, { color: string; label: string }> = {
  Pending: { color: 'default', label: 'Pending' },
  Complied: { color: 'green', label: 'Complied' },
  NotComplied: { color: 'red', label: 'Not complied with' },
};

/**
 * Discovery summonses on a new-discovery transaction (LGC §213; LAM 2025 Book III pp.85–86;
 * assessment-listing-exemptions.md §4.4, Q10): a summons to declare within the configured period after receipt,
 * a second one only after the first is not complied with, then the verification with other agencies before the
 * assessor declares the property (LGC §204). Summonses never block the transaction.
 */
export function DiscoveryPanel({ transactionId, open }: { transactionId: string; open: boolean }) {
  const { data, isLoading } = useDiscovery(transactionId);
  const issue = useIssueSummons(transactionId);
  const service = useRecordSummonsService(transactionId);
  const outcome = useRecordSummonsOutcome(transactionId);
  const verification = useRecordVerification(transactionId);
  const [issuing, setIssuing] = useState(false);
  const [serving, setServing] = useState<DiscoverySummonsDto | null>(null);
  const [closing, setClosing] = useState<DiscoverySummonsDto | null>(null);
  const [note, setNote] = useState('');
  const summonses = data?.summonses ?? [];
  const verificationDue = open && !data?.interAgencyVerification && summonses.some((s) => s.sequence === 2 && s.outcome === 'NotComplied');

  return (
    <>
      <Typography.Title level={5} style={{ marginTop: 16 }}>Discovery summonses</Typography.Title>
      {data?.nextStep && <Alert type="info" showIcon style={{ marginBottom: 8 }} title={data.nextStep} />}
      <Table<DiscoverySummonsDto>
        rowKey="id" size="small" loading={isLoading} dataSource={summonses} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No summons issued' }}
        columns={[
          {
            title: '#', render: (_, s) => (
              <span>{s.sequence}{s.summonsNumber && <><br /><Typography.Text type="secondary">{s.summonsNumber}</Typography.Text></>}</span>
            ),
          },
          {
            title: 'Addressee', width: 150, ellipsis: { showTitle: false },
            render: (_, s) => <Tooltip title={[s.addresseeName, s.addresseeAddress].filter(Boolean).join(' — ')}>{s.addresseeName}</Tooltip>,
          },
          { title: 'Issued', dataIndex: 'issuedOn' },
          {
            title: 'Received', render: (_, s) => !s.receivedOn ? '—' : (
              <span>{s.receivedOn}<br /><Typography.Text type="secondary">{serviceModeLabel[s.serviceMode!]}</Typography.Text></span>
            ),
          },
          {
            title: 'Due', render: (_, s) => !s.dueDate ? <Typography.Text type="secondary">{s.periodDays} days after receipt</Typography.Text> : (
              <Space size={4}>
                {s.dueDate}
                {s.overdue && <Tag color="red" icon={<WarningOutlined />}>Past due</Tag>}
              </Space>
            ),
          },
          {
            title: 'Outcome', render: (_, s) => (
              <Tooltip title={s.outcomeNotes}>
                <Tag color={outcomeTag[s.outcome].color}>{outcomeTag[s.outcome].label}</Tag>
                {s.outcomeOn && <><br /><Typography.Text type="secondary">{s.outcomeOn}</Typography.Text></>}
              </Tooltip>
            ),
          },
          {
            title: 'Actions', render: (_, s) => (
              <Space size={4} wrap>
                <PrintFormButton formCode="DISCOVERY_SUMMONS" subjectId={s.id} issuable />
                {open && !s.receivedOn && <Button size="small" type="primary" onClick={() => { service.reset(); setServing(s); }}>Record service</Button>}
                {open && s.receivedOn && s.outcome === 'Pending' && (
                  <Button size="small" onClick={() => { outcome.reset(); setClosing(s); }}>Record outcome</Button>
                )}
              </Space>
            ),
          },
        ]}
      />
      {data?.canIssue && (
        <Button icon={<PlusOutlined />} style={{ marginTop: 8 }} onClick={() => { issue.reset(); setIssuing(true); }}>
          {summonses.length === 0 ? 'Issue summons' : 'Issue second summons'}
        </Button>
      )}

      {data?.interAgencyVerification && (
        <Alert type="success" showIcon style={{ marginTop: 12 }} title={`Verification with other agencies (recorded ${data.verificationRecordedOn})`}
          description={data.interAgencyVerification} />
      )}
      {verificationDue && (
        <div style={{ marginTop: 12 }}>
          <Typography.Text strong>Verification with other agencies</Typography.Text>
          {verification.isError && <Alert type="error" showIcon style={{ margin: '8px 0' }} title="Could not record" description={errorText(verification.error)} />}
          <Input.TextArea aria-label="Verification note" rows={3} style={{ margin: '8px 0' }} value={note} onChange={(e) => setNote(e.target.value)}
            placeholder="Agencies coordinated with and what they confirmed (e.g. Registry of Deeds, DENR, BIR)" maxLength={2000} />
          <Button disabled={!note.trim()} loading={verification.isPending} onClick={() => verification.mutate(note.trim(), { onSuccess: () => setNote('') })}>
            Record verification
          </Button>
        </div>
      )}

      <Modal title={summonses.length === 0 ? 'Issue summons' : 'Issue second summons'} open={issuing} footer={null} destroyOnHidden onCancel={() => setIssuing(false)}>
        {issue.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not issue" description={errorText(issue.error)} />}
        <Form layout="vertical" initialValues={{ issuedOn: dayjs(), addresseeName: summonses[0]?.addresseeName, addresseeAddress: summonses[0]?.addresseeAddress }}
          onFinish={(v) => issue.mutate({ addresseeName: v.addresseeName, addresseeAddress: v.addresseeAddress, issuedOn: v.issuedOn.format('YYYY-MM-DD') },
            { onSuccess: () => setIssuing(false) })}>
          <Form.Item name="addresseeName" label="Addressee (the owner or person in charge)" rules={[{ required: true }, { max: 300 }]}><Input /></Form.Item>
          <Form.Item name="addresseeAddress" label="Address" rules={[{ max: 1000 }]}><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="issuedOn" label="Date issued" rules={[{ required: true }]}><DatePicker /></Form.Item>
          <Button type="primary" htmlType="submit" loading={issue.isPending}>Issue</Button>
        </Form>
      </Modal>

      <Modal title={serving ? `Record service — summons ${serving.sequence}` : ''} open={serving !== null} footer={null} destroyOnHidden onCancel={() => setServing(null)}>
        <Typography.Paragraph type="secondary">The owner has {serving?.periodDays} days from receipt to declare the property.</Typography.Paragraph>
        {service.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not record" description={errorText(service.error)} />}
        <Form layout="vertical" initialValues={{ receivedOn: dayjs() }}
          onFinish={(v) => serving && service.mutate({
            id: serving.id, serviceMode: v.serviceMode, receivedOn: v.receivedOn.format('YYYY-MM-DD'), servedTo: v.servedTo, proofReference: v.proofReference, notes: v.notes,
          }, { onSuccess: () => setServing(null) })}>
          <Form.Item name="serviceMode" label="Mode of service" rules={[{ required: true }]}>
            <Select options={(Object.keys(serviceModeLabel) as NoticeServiceMode[]).map((m) => ({ value: m, label: serviceModeLabel[m] }))} />
          </Form.Item>
          <Form.Item name="receivedOn" label="Date received" rules={[{ required: true }]}><DatePicker /></Form.Item>
          <Form.Item name="servedTo" label="Received by" rules={[{ required: true }, { max: 300 }]}><Input /></Form.Item>
          <Form.Item name="proofReference" label="Proof of service" rules={[{ required: true }, { max: 200 }]}><Input /></Form.Item>
          <Form.Item name="notes" label="Notes" rules={[{ max: 1000 }]}><Input.TextArea rows={2} /></Form.Item>
          <Button type="primary" htmlType="submit" loading={service.isPending}>Record service</Button>
        </Form>
      </Modal>

      <Modal title={closing ? `Outcome — summons ${closing.sequence}` : ''} open={closing !== null} footer={null} destroyOnHidden onCancel={() => setClosing(null)}>
        <Typography.Paragraph type="secondary">
          Due {closing?.dueDate}. Non-compliance can be recorded only after that date.
        </Typography.Paragraph>
        {outcome.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not record" description={errorText(outcome.error)} />}
        <Form layout="vertical" initialValues={{ outcomeOn: dayjs() }}
          onFinish={(v) => closing && outcome.mutate({ id: closing.id, outcome: v.outcome, outcomeOn: v.outcomeOn.format('YYYY-MM-DD'), notes: v.notes },
            { onSuccess: () => setClosing(null) })}>
          <Form.Item name="outcome" label="Outcome" rules={[{ required: true }]}>
            <Select options={[{ value: 'Complied', label: 'Complied: the owner declared the property' }, { value: 'NotComplied', label: 'Not complied with' }]} />
          </Form.Item>
          <Form.Item name="outcomeOn" label="Date" rules={[{ required: true }]}><DatePicker /></Form.Item>
          <Form.Item name="notes" label="Notes" rules={[{ max: 1000 }]}><Input.TextArea rows={2} /></Form.Item>
          <Button type="primary" htmlType="submit" loading={outcome.isPending}>Record outcome</Button>
        </Form>
      </Modal>
    </>
  );
}
