import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Input, InputNumber, Select, Space, Table, Typography } from 'antd';
import { apiGet, apiPut, ApiRequestError } from '../../lib/apiClient';
import { useClassifications, useSubClassifications } from '../../api/referenceData';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);

interface CriterionDto {
  id: string; classificationId: string; classificationName: string; subClassificationId: string; subClassificationName: string; sequence: number; criteria: string;
}
interface Row { key: string; classificationId: string | null; subClassificationId: string | null; sequence: number; criteria: string }

/**
 * The SMV's sub-class criteria (SMV Form 1; docs/analysis/smv-preparation-general-revision.md §4.2): the province's text, set with
 * the draft SMV by the provincial office; printed, never used to classify a property.
 */
export function SubClassCriteriaCard({ smvId, editable }: { smvId: string; editable: boolean }) {
  const qc = useQueryClient();
  const { data = [], isLoading } = useQuery({
    queryKey: ['smv', smvId, 'sub-class-criteria'], queryFn: () => apiGet<CriterionDto[]>(`/api/smv/${smvId}/sub-class-criteria`),
  });
  const save = useMutation({
    mutationFn: (rows: Row[]) => apiPut<CriterionDto[]>(`/api/smv/${smvId}/sub-class-criteria`, {
      criteria: rows.map((r) => ({ classificationId: r.classificationId, subClassificationId: r.subClassificationId, sequence: r.sequence, criteria: r.criteria })),
    }),
    onSuccess: (d) => qc.setQueryData(['smv', smvId, 'sub-class-criteria'], d),
  });
  // The editor starts from the saved criteria and starts again whenever they change.
  const version = JSON.stringify(data);
  return (
    <Card size="small" title="Sub-class criteria (SMV Form 1)">
      <Typography.Paragraph type="secondary">
        The province&apos;s description of each sub-class; set while the SMV is a draft, fixed once it is approved.
      </Typography.Paragraph>
      {save.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not save" description={errorText(save.error)} />}
      {editable ? (
        <CriteriaEditor key={version} saved={data} loading={isLoading} saving={save.isPending} onSave={(rows) => save.mutate(rows)} />
      ) : (
        <Table<CriterionDto> rowKey="id" size="small" dataSource={data} pagination={false} loading={isLoading} locale={{ emptyText: 'No criteria entered' }}
          columns={[
            { title: 'Class', dataIndex: 'classificationName' },
            { title: 'Sub-class', dataIndex: 'subClassificationName' },
            { title: 'Criteria', dataIndex: 'criteria', render: (v: string) => <span style={{ whiteSpace: 'pre-wrap' }}>{v}</span> },
          ]} />
      )}
    </Card>
  );
}

const toRows = (d: CriterionDto[]): Row[] =>
  d.map((c) => ({ key: c.id, classificationId: c.classificationId, subClassificationId: c.subClassificationId, sequence: c.sequence, criteria: c.criteria }));

function CriteriaEditor({ saved, loading, saving, onSave }: { saved: CriterionDto[]; loading: boolean; saving: boolean; onSave: (rows: Row[]) => void }) {
  const { data: classes = [] } = useClassifications();
  const { data: subs = [] } = useSubClassifications();
  const [rows, setRows] = useState<Row[]>(() => toRows(saved));
  const set = (key: string, patch: Partial<Row>) => setRows(rows.map((r) => (r.key === key ? { ...r, ...patch } : r)));
  const complete = rows.every((r) => r.classificationId && r.subClassificationId && r.criteria.trim() && r.sequence >= 1);
  const dirty = JSON.stringify(rows) !== JSON.stringify(toRows(saved));
  return (
    <>
      <Table<Row> rowKey="key" size="small" dataSource={rows} pagination={false} loading={loading} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No criteria entered' }}
        columns={[
          { title: 'Class', render: (_, r) => <Select aria-label="Class" showSearch optionFilterProp="label" style={{ width: 200 }} value={r.classificationId ?? undefined}
            onChange={(v) => set(r.key, { classificationId: v })} options={classes.map((c) => ({ value: c.id, label: c.name }))} /> },
          { title: 'Sub-class', render: (_, r) => <Select aria-label="Sub-class" showSearch optionFilterProp="label" style={{ width: 170 }} value={r.subClassificationId ?? undefined}
            onChange={(v) => set(r.key, { subClassificationId: v })} options={subs.map((x) => ({ value: x.id, label: x.name }))} /> },
          { title: 'Order', render: (_, r) => <InputNumber<number> aria-label="Order" min={1} precision={0} value={r.sequence} onChange={(v) => set(r.key, { sequence: v ?? 1 })} /> },
          { title: 'Criteria', render: (_, r) => <Input.TextArea aria-label="Criteria" autoSize style={{ width: 380 }} maxLength={4000} value={r.criteria}
            onChange={(e) => set(r.key, { criteria: e.target.value })} /> },
          { title: '', render: (_, r) => <Button size="small" danger onClick={() => setRows(rows.filter((x) => x.key !== r.key))}>Remove</Button> },
        ]} />
      <Space style={{ marginTop: 8 }}>
        <Button size="small" onClick={() => setRows([...rows, { key: crypto.randomUUID(), classificationId: null, subClassificationId: null, sequence: rows.length + 1, criteria: '' }])}>
          Add a sub-class
        </Button>
        <Button size="small" type="primary" disabled={!dirty || !complete} loading={saving} onClick={() => onSave(rows)}>Save criteria</Button>
      </Space>
    </>
  );
}
