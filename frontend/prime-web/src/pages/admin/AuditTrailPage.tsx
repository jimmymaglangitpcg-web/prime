import { useState } from 'react';
import { DownloadOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Checkbox, DatePicker, Input, Select, Space, Tag, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import { auditActions, useAuditDownload, useAuditTables, type AuditAction, type AuditLogFilter } from '../../api/audit';
import { ApiRequestError } from '../../lib/apiClient';
import { ActionTag, AuditTable } from '../../components/AuditTable';
import { useCan } from '../../api/offices';

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * The audit trail (CLAUDE.md §48, §77; docs/analysis/workflow-security.md §4.3), for holders of `audit.view`: every
 * change, decision, sign-in and export, with who, when, the reason and the values before and after. Read-only.
 */
export function AuditTrailPage() {
  const can = useCan();
  if (!can('audit.view')) {
    return <Alert type="warning" showIcon title="Not permitted"
      description="Reading the audit trail needs the audit.view permission (AUDITOR and SYSTEM_ADMIN by default)." />;
  }
  return <AuditTrail />;
}

function AuditTrail() {
  const tables = useAuditTables();
  const can = useCan();
  const download = useAuditDownload();
  const [tableName, setTableName] = useState<string>();
  const [action, setAction] = useState<AuditAction>();
  const [recordText, setRecordText] = useState('');
  const [includeChildren, setIncludeChildren] = useState(true);
  const [userId, setUserId] = useState<string>();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);

  const recordId = guid.test(recordText.trim()) ? recordText.trim() : undefined;
  const filter: AuditLogFilter = {
    tableName, action, userId, recordId, includeChildren: recordId ? includeChildren : undefined,
    from: range?.[0]?.startOf('day').toISOString(),
    to: range?.[1]?.add(1, 'day').startOf('day').toISOString(),
  };
  const clear = () => {
    setTableName(undefined); setAction(undefined); setRecordText(''); setUserId(undefined); setRange(null);
  };

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Title level={3} style={{ margin: 0 }}>Audit Trail</Typography.Title>
      <Alert type="info" showIcon title="Append-only"
        description="Each row was written in the same database transaction as the change it records, or when a user signed in or out, printed or exported. The database refuses to change or delete a row. Viewing a record is not logged." />
      <Card size="small">
        <Space wrap>
          <Select allowClear showSearch placeholder="Table" style={{ width: 240 }} value={tableName} onChange={setTableName} loading={tables.isLoading}
            options={(tables.data ?? []).map((t) => ({ value: t, label: t }))} aria-label="Table" />
          <Select allowClear placeholder="Action" style={{ width: 150 }} value={action} onChange={setAction} aria-label="Action"
            options={auditActions.map((a) => ({ value: a, label: <ActionTag action={a} /> }))} />
          <Input allowClear placeholder="Record id" style={{ width: 330 }} value={recordText} onChange={(e) => setRecordText(e.target.value)}
            status={recordText.trim() && !recordId ? 'error' : undefined} aria-label="Record id" />
          {recordId && <Checkbox checked={includeChildren} onChange={(e) => setIncludeChildren(e.target.checked)}>with its child rows</Checkbox>}
          <DatePicker.RangePicker value={range} onChange={(v) => setRange(v)} aria-label="Period" />
          {userId && <Tag closable onClose={() => setUserId(undefined)}>User {userId.slice(0, 8)}</Tag>}
          <Button onClick={clear}>Clear</Button>
          {can('records.export') && (['csv', 'xlsx'] as const).map((format) => (
            <Button key={format} icon={<DownloadOutlined />} loading={download.isPending && download.variables?.format === format}
              onClick={() => download.mutate({ filter, format })}>
              {format === 'csv' ? 'CSV' : 'Excel'}
            </Button>
          ))}
        </Space>
        {download.error && (
          <Alert type="error" showIcon style={{ marginTop: 12 }} title="Not downloaded"
            description={download.error instanceof ApiRequestError ? download.error.apiError.message : (download.error as Error).message} />
        )}
      </Card>
      <Card size="small" title="Rows" extra={<Typography.Text type="secondary">Newest first; open a row for its values. Click a user to see only theirs.</Typography.Text>}>
        <AuditTable filter={filter} onUser={setUserId} />
      </Card>
    </Space>
  );
}
