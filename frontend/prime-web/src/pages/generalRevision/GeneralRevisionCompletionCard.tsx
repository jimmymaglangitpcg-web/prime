import { useState } from 'react';
import { Alert, Button, Card, DatePicker, Form, Input, Modal, Space, Table, Tag, Typography } from 'antd';
import dayjs from 'dayjs';
import {
  useCompleteGeneralRevision, useCompleteGeneralRevisionStep, useGeneralRevisionReadiness, useLoadGeneralRevisionChecklist,
} from '../../api/generalRevision';
import { PrintFormButton } from '../../components/PrintFormButton';
import { ApiRequestError } from '../../lib/apiClient';
import { gateLabel, type ChecklistStepDto, type GateStatusDto, type GeneralRevisionDto } from '../../lib/generalRevisionTypes';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);

/**
 * Closing the general revision (docs/analysis/smv-preparation-general-revision.md §4.6, L6-6c): the conditions PRIME checks,
 * the GRI checklist loaded as content (Q13), completion, and the completion and status reports.
 */
export function GeneralRevisionCompletionCard({ gr }: { gr: GeneralRevisionDto }) {
  const { data, isFetching } = useGeneralRevisionReadiness(gr.id);
  const load = useLoadGeneralRevisionChecklist(gr.id);
  const complete = useCompleteGeneralRevision(gr.id);
  const [marking, setMarking] = useState<ChecklistStepDto>();
  const [modal, modalContext] = Modal.useModal();
  const open = gr.status === 'Planned' || gr.status === 'InProgress';
  const done = gr.status === 'Completed';
  const met = (m: boolean) => (m ? <Tag color="green">Met</Tag> : <Tag color="gold">Not yet</Tag>);
  return (
    <Card title="Completion" size="small" loading={!data && isFetching}
      extra={(
        <Space>
          <PrintFormButton formCode="GR_COMPLETION_REPORT" subjectId={gr.id} issuable={done} label="Completion report" />
          <PrintFormButton formCode="GR_STATUS_REPORT" subjectId={gr.id} issuable={done} label="Status report" />
        </Space>
      )}>
      {modalContext}
      {!done && <Typography.Paragraph type="secondary">The reports can be previewed now and are issued once the revision is completed.</Typography.Paragraph>}
      {load.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not load the checklist" description={errorText(load.error)} />}
      {complete.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not complete" description={errorText(complete.error)} />}
      {data && (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Table<GateStatusDto> scroll={{ x: true }} rowKey="gate" size="small" pagination={false} dataSource={data.gates}
            columns={[
              { title: 'Condition (checked by PRIME)', dataIndex: 'gate', render: (g: GateStatusDto['gate']) => gateLabel[g] },
              { title: '', dataIndex: 'met', render: met },
              { title: 'Detail', dataIndex: 'detail' },
            ]} />
          <div>
            <Space style={{ marginBottom: 8 }}>
              <Typography.Text strong>Checklist</Typography.Text>
              {open && !data.checklistLoaded && <Button size="small" loading={load.isPending} onClick={() => load.mutate()}>Load the checklist in force</Button>}
            </Space>
            {data.checklistLoaded ? (
              <Table<ChecklistStepDto> scroll={{ x: true }} rowKey="id" size="small" pagination={false} dataSource={data.checklist}
                columns={[
                  { title: 'Step', dataIndex: 'code' },
                  { title: 'Title', dataIndex: 'title' },
                  { title: 'Checked by', render: (_, s) => (s.gate ? <Tag>PRIME: {gateLabel[s.gate]}</Tag> : 'The office') },
                  { title: '', dataIndex: 'met', render: met },
                  {
                    title: 'Done', render: (_, s) => (s.gate ? s.gateDetail : s.completedOn
                      ? <span>{s.completedOn}{s.completedByName && ` · ${s.completedByName}`}{s.evidence && <><br /><Typography.Text type="secondary">{s.evidence}</Typography.Text></>}</span>
                      : open && <Button size="small" onClick={() => setMarking(s)}>Mark done</Button>),
                  },
                ]} />
            ) : <Typography.Text type="secondary">No checklist loaded. The general revision instructions&apos; checklist is content: load it through a content pack and have it approved.</Typography.Text>}
          </div>
          {open && (
            <Space orientation="vertical" style={{ width: '100%' }}>
              {data.blockers.length > 0
                ? <Alert type="info" showIcon title="Not ready to complete" description={<ul style={{ margin: 0, paddingLeft: 18 }}>{data.blockers.map((b) => <li key={b}>{b}</li>)}</ul>} />
                : <Alert type="success" showIcon title="Ready to complete" />}
              <Button type="primary" disabled={data.blockers.length > 0} loading={complete.isPending}
                onClick={() => modal.confirm({
                  title: 'Complete the general revision?', content: 'The revision is closed: no further run, notice batch or register run is made from it.',
                  onOk: () => complete.mutateAsync().catch(() => undefined),
                })}>
                Complete the revision
              </Button>
            </Space>
          )}
        </Space>
      )}
      {marking && <MarkDoneModal id={gr.id} step={marking} onClose={() => setMarking(undefined)} />}
    </Card>
  );
}

function MarkDoneModal({ id, step, onClose }: { id: string; step: ChecklistStepDto; onClose: () => void }) {
  const mark = useCompleteGeneralRevisionStep(id);
  return (
    <Modal open title={`${step.code} — ${step.title}`} footer={null} destroyOnHidden onCancel={onClose}>
      {step.description && <Typography.Paragraph type="secondary">{step.description}</Typography.Paragraph>}
      {mark.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not record" description={errorText(mark.error)} />}
      <Form layout="vertical" initialValues={{ completedOn: dayjs() }}
        onFinish={(v) => mark.mutate({ stepId: step.id, completedOn: v.completedOn.format('YYYY-MM-DD'), evidence: v.evidence?.trim() || null }, { onSuccess: onClose })}>
        <Form.Item name="completedOn" label="Done on" rules={[{ required: true }]}><DatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} /></Form.Item>
        <Form.Item name="evidence" label="Evidence (office order, memo, report reference)"><Input maxLength={500} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={mark.isPending}>Mark done</Button>
      </Form>
    </Modal>
  );
}
