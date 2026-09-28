import { useState } from 'react';
import { Alert, Button, Card, Descriptions, Empty, Popconfirm, Space, Table, Tabs, Tag, Tooltip, Typography, Upload, message } from 'antd';
import { CloudUploadOutlined, EyeOutlined, ImportOutlined, ReloadOutlined } from '@ant-design/icons';
import {
  useContentImportItems, useContentImports, useContentPacks, useImportContentPack, usePreviewContentPack, useUploadContentPack,
  type ContentChangeDto, type ContentFilePreviewDto, type ContentImportDto, type ContentImportItemDto, type ContentIssueDto,
  type ContentPackInfo, type ContentPackPreviewDto,
} from '../../api/contentPacks';
import { ApiRequestError } from '../../lib/apiClient';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const fileLabel = (f: { kind: string; lookup: string | null }) => (f.lookup ? `lookup: ${f.lookup}` : f.kind);
const short = (hash: string | null) => (hash ? `${hash.slice(0, 12)}…` : '—');

/**
 * LGU content packs (docs/analysis/lgu-content-pack.md, step C4): LAM and LGU
 * content kept out of the repository (CLAUDE.md §118) is uploaded or placed in
 * the content folder, previewed as a dry run, then imported. Geography and
 * lookups apply on import; configuration arrives as Draft versions that a
 * second user approves on its own screen.
 */
export function ContentPacksPage() {
  return (
    <div>
      <Typography.Title level={3}>Content Packs</Typography.Title>
      <Typography.Paragraph type="secondary" style={{ maxWidth: 900 }}>
        Loads the province&apos;s geography and index numbers, lookups, transaction types, numbering schemes, approval chains and
        forms from a content pack. A preview writes nothing. An import records who applied which files; nothing is ever deleted, and
        configuration comes in as drafts that another user must approve.
      </Typography.Paragraph>
      <Tabs items={[
        { key: 'packs', label: 'Packs', children: <PacksTab /> },
        { key: 'history', label: 'Import history', children: <HistoryTab /> },
      ]} />
    </div>
  );
}

function PacksTab() {
  const packs = useContentPacks();
  const upload = useUploadContentPack();
  const preview = usePreviewContentPack();
  const importPack = useImportContentPack();
  const [toast, ctx] = message.useMessage();
  const [result, setResult] = useState<string | null>(null);

  const runPreview = (pack: string) => {
    setResult(null);
    importPack.reset();
    preview.mutate(pack, { onError: (e) => toast.error(errorText(e)) });
  };

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      {ctx}
      {packs.isError && <Alert type="warning" showIcon title="Content packs are unavailable" description={errorText(packs.error)} />}
      <Space wrap>
        <Upload accept=".zip,application/zip" showUploadList={false} disabled={upload.isPending}
          beforeUpload={(file) => {
            upload.mutate(file, {
              onSuccess: (p) => { toast.success(`Uploaded pack ${p.pack}.`); runPreview(p.pack); },
              onError: (e) => toast.error(errorText(e)),
            });
            return false;
          }}>
          <Button icon={<CloudUploadOutlined />} loading={upload.isPending}>Upload pack (.zip)</Button>
        </Upload>
        <Button icon={<ReloadOutlined />} onClick={() => packs.refetch()}>Refresh</Button>
        <Typography.Text type="secondary">A zip holds manifest.json at its root or in one folder. It replaces the pack of the same name; the old copy is kept.</Typography.Text>
      </Space>

      <Table<ContentPackInfo>
        rowKey="pack" size="small" loading={packs.isLoading} dataSource={packs.data ?? []} pagination={false}
        locale={{ emptyText: <Empty description="No packs in the content folder" /> }}
        columns={[
          { title: 'Pack', dataIndex: 'pack' },
          { title: 'Manifest', dataIndex: 'hasManifest', render: (v: boolean) => (v ? <Tag color="green">present</Tag> : <Tag color="red">missing</Tag>) },
          {
            title: 'Actions', render: (_, p) => (
              <Button size="small" icon={<EyeOutlined />} loading={preview.isPending && preview.variables === p.pack} onClick={() => runPreview(p.pack)}>
                Preview
              </Button>
            ),
          },
        ]}
      />

      {result && <Alert type="success" showIcon closable title={result} onClose={() => setResult(null)} />}
      {preview.data && (
        <PreviewCard
          preview={preview.data}
          importing={importPack.isPending}
          importError={importPack.isError ? errorText(importPack.error) : null}
          onPreviewAgain={() => runPreview(preview.data.pack)}
          onImport={() => importPack.mutate({ pack: preview.data.pack, fingerprint: preview.data.fingerprint! }, {
            onSuccess: (r) => {
              setResult(r.message);
              preview.reset();
            },
          })}
        />
      )}
    </Space>
  );
}

function PreviewCard({ preview, importing, importError, onImport, onPreviewAgain }: {
  preview: ContentPackPreviewDto; importing: boolean; importError: string | null; onImport: () => void; onPreviewAgain: () => void;
}) {
  const totals = preview.files.reduce((t, f) => ({ n: t.n + f.new, c: t.c + f.changed }), { n: 0, c: 0 });
  const nothingToDo = totals.n + totals.c === 0;
  return (
    <Card title={`Preview: ${preview.pack}`} extra={<Button size="small" icon={<ReloadOutlined />} onClick={onPreviewAgain}>Preview again</Button>}>
      <Descriptions size="small" column={{ xs: 1, md: 2 }} bordered style={{ marginBottom: 12 }} items={[
        { key: 'v', label: 'Version', children: preview.version ?? '—' },
        {
          key: 's', label: 'Status', children: preview.canImport
            ? <Tag color="green">Ready to import</Tag>
            : <Tag color="red">{preview.errorCount} error{preview.errorCount === 1 ? '' : 's'}: cannot import</Tag>,
        },
        { key: 'd', label: 'Description', children: preview.description ?? '—', span: 'filled' },
        { key: 'c', label: 'Would apply', children: `${totals.n} new, ${totals.c} changed` },
        { key: 'w', label: 'Warnings', children: preview.warningCount },
        { key: 'f', label: 'Fingerprint', children: <Tooltip title={preview.fingerprint}><Typography.Text code>{short(preview.fingerprint)}</Typography.Text></Tooltip> },
      ]} />

      {preview.issues.length > 0 && <IssueTable issues={preview.issues} title="Manifest" />}

      <Table<ContentFilePreviewDto>
        rowKey="path" size="small" dataSource={preview.files} pagination={false} scroll={{ x: 'max-content' }}
        expandable={{
          rowExpandable: (f) => f.issues.length + f.changes.length + f.missingKeys.length > 0,
          expandedRowRender: (f) => <FileDetail file={f} />,
        }}
        columns={[
          { title: 'File', render: (_, f) => <Space orientation="vertical" size={0}><Typography.Text strong>{fileLabel(f)}</Typography.Text><Typography.Text type="secondary">{f.path}</Typography.Text></Space> },
          { title: 'Source', dataIndex: 'source', render: (v: string | null) => v ?? <Typography.Text type="secondary">per row</Typography.Text> },
          { title: 'Rows', dataIndex: 'rows' },
          { title: 'New', dataIndex: 'new' },
          { title: 'Changed', dataIndex: 'changed' },
          { title: 'Unchanged', dataIndex: 'unchanged' },
          {
            title: <Tooltip title="Records PRIME has that this file does not list. They are reported only, never removed.">Not in pack</Tooltip>,
            dataIndex: 'missingFromPack',
          },
          {
            title: 'Issues', render: (_, f) => {
              const errors = f.issues.filter((i) => i.severity === 'Error').length;
              const warnings = f.issues.length - errors;
              return (
                <Space size={4}>
                  {!f.supported && <Tag>later step</Tag>}
                  {errors > 0 && <Tag color="red">{errors} error{errors === 1 ? '' : 's'}</Tag>}
                  {warnings > 0 && <Tag color="gold">{warnings} warning{warnings === 1 ? '' : 's'}</Tag>}
                  {errors + warnings === 0 && f.supported && <Tag color="green">ok</Tag>}
                </Space>
              );
            },
          },
        ]}
      />

      {importError && <Alert type="error" showIcon style={{ marginTop: 12 }} title="Import refused" description={importError} />}
      <Space style={{ marginTop: 12 }} wrap>
        <Popconfirm
          title="Import this pack?"
          description={<div style={{ maxWidth: 380 }}>
            {totals.n} new and {totals.c} changed records, in one transaction, recorded under your name. Transaction types, numbering
            schemes, approval chains and forms become drafts that another user must approve.
          </div>}
          okText="Import" onConfirm={onImport} disabled={!preview.canImport || nothingToDo}>
          <Button type="primary" icon={<ImportOutlined />} loading={importing} disabled={!preview.canImport || nothingToDo}>Import</Button>
        </Popconfirm>
        {!preview.canImport && <Typography.Text type="danger">Fix the errors in the pack, then preview again.</Typography.Text>}
        {preview.canImport && nothingToDo && <Typography.Text type="secondary">PRIME already matches this pack.</Typography.Text>}
      </Space>
    </Card>
  );
}

function FileDetail({ file }: { file: ContentFilePreviewDto }) {
  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      {file.issues.length > 0 && <IssueTable issues={file.issues} />}
      {file.changes.length > 0 && (
        <Table<ContentChangeDto>
          rowKey={(c) => `${c.action}-${c.key}`} size="small" dataSource={file.changes} title={() => 'Records to add or change'}
          pagination={{ pageSize: 10, hideOnSinglePage: true }}
          columns={[
            { title: 'Key', dataIndex: 'key' },
            { title: 'Name', dataIndex: 'name' },
            { title: 'Action', dataIndex: 'action', render: (a: string) => <Tag color={a === 'New' ? 'blue' : 'orange'}>{a}</Tag> },
            { title: 'Fields', render: (_, c) => <FieldChanges changes={c.fields} /> },
          ]}
        />
      )}
      {file.missingKeys.length > 0 && (
        <Alert type="info" showIcon title={`${file.missingFromPack} record(s) in PRIME are not in this file (kept as they are)`}
          description={file.missingKeys.join('; ') + (file.missingFromPack > file.missingKeys.length ? '; …' : '')} />
      )}
    </Space>
  );
}

function IssueTable({ issues, title }: { issues: ContentIssueDto[]; title?: string }) {
  return (
    <Table<ContentIssueDto>
      rowKey={(i) => `${i.code}-${i.line}-${i.field}-${i.message}`} size="small" dataSource={issues} title={title ? () => title : undefined}
      pagination={{ pageSize: 10, hideOnSinglePage: true }}
      columns={[
        { title: 'Severity', dataIndex: 'severity', render: (s: string) => <Tag color={s === 'Error' ? 'red' : 'gold'}>{s}</Tag> },
        { title: 'Line', dataIndex: 'line', render: (v: number | null) => v ?? '—' },
        { title: 'Field', dataIndex: 'field', render: (v: string | null) => v ?? '—' },
        { title: 'Code', dataIndex: 'code', render: (v: string) => <Typography.Text code>{v}</Typography.Text> },
        { title: 'Message', dataIndex: 'message' },
      ]}
    />
  );
}

function FieldChanges({ changes }: { changes: { field: string; from: string | null; to: string | null }[] }) {
  if (changes.length === 0) {
    return <Typography.Text type="secondary">—</Typography.Text>;
  }
  return (
    <ul style={{ margin: 0, paddingLeft: 18 }}>
      {changes.map((c) => (
        <li key={c.field}>
          <Typography.Text strong>{c.field}</Typography.Text>: {c.from ?? <em>none</em>} → {c.to ?? <em>none</em>}
        </li>
      ))}
    </ul>
  );
}

function HistoryTab() {
  const [page, setPage] = useState(1);
  const imports = useContentImports(page);
  return (
    <Table<ContentImportDto>
      rowKey="id" size="small" loading={imports.isLoading} dataSource={imports.data?.items ?? []} scroll={{ x: 'max-content' }}
      pagination={{ current: page, pageSize: 10, total: imports.data?.totalCount ?? 0, onChange: setPage, hideOnSinglePage: true }}
      locale={{ emptyText: <Empty description="No content pack has been imported yet" /> }}
      expandable={{ expandedRowRender: (r) => <ImportItems id={r.id} /> }}
      columns={[
        { title: 'Imported', dataIndex: 'importedAt', render: (v: string) => new Date(v).toLocaleString() },
        { title: 'Pack', render: (_, r) => `${r.pack} ${r.packVersion}` },
        { title: 'By', render: (_, r) => r.importedByName ?? r.importedBy },
        { title: 'Created', dataIndex: 'createdCount' },
        { title: 'Changed', dataIndex: 'changedCount' },
        { title: 'Warnings', render: (_, r) => r.warnings.length },
        { title: 'Fingerprint', dataIndex: 'fingerprint', render: (v: string) => <Tooltip title={v}><Typography.Text code>{short(v)}</Typography.Text></Tooltip> },
      ]}
    />
  );
}

function ImportItems({ id }: { id: string }) {
  const [page, setPage] = useState(1);
  const items = useContentImportItems(id, page);
  return (
    <Table<ContentImportItemDto>
      rowKey="sequence" size="small" loading={items.isLoading} dataSource={items.data?.items ?? []}
      pagination={{ current: page, pageSize: 20, total: items.data?.totalCount ?? 0, onChange: setPage, hideOnSinglePage: true }}
      columns={[
        { title: '#', dataIndex: 'sequence' },
        { title: 'Record', render: (_, i) => `${i.entityType} ${i.key}` },
        { title: 'Action', dataIndex: 'action', render: (a: string) => <Tag color={a === 'Created' ? 'blue' : 'orange'}>{a}</Tag> },
        { title: 'Changes', render: (_, i) => <FieldChanges changes={i.changes} /> },
        { title: 'Source', dataIndex: 'source' },
        { title: 'From', render: (_, i) => `${i.filePath}:${i.line}` },
      ]}
    />
  );
}
