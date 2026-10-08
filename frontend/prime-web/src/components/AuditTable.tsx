import { useState } from 'react';
import { Card, Empty, Table, Tag, Typography } from 'antd';
import type { TablePaginationConfig } from 'antd';
import { useAuditLogs, type AuditAction, type AuditLogDto, type AuditLogFilter } from '../api/audit';
import { useCan } from '../api/offices';

const actionColor: Record<AuditAction, string> = {
  Create: 'blue', Update: 'default', Delete: 'red', Approve: 'green', Reject: 'volcano', Post: 'cyan', Void: 'magenta',
  Cancel: 'orange', Reverse: 'purple', Login: 'geekblue', Logout: 'geekblue', Export: 'gold',
};

export function ActionTag({ action }: { action: AuditAction }) {
  return <Tag color={actionColor[action]}>{action.toUpperCase()}</Tag>;
}

function parse(json: string | null): Record<string, unknown> {
  if (!json) {
    return {};
  }
  try {
    const value: unknown = JSON.parse(json);
    return value && typeof value === 'object' ? (value as Record<string, unknown>) : { value };
  } catch {
    return { value: json };
  }
}

const show = (v: unknown) => (v === undefined ? '' : v === null ? '—' : typeof v === 'object' ? JSON.stringify(v) : String(v));

/** A row's before and after values, field by field. */
export function AuditValues({ row }: { row: AuditLogDto }) {
  const before = parse(row.oldValue);
  const after = parse(row.newValue);
  const fields = [...new Set([...Object.keys(before), ...Object.keys(after)])];
  if (fields.length === 0) {
    return <Typography.Text type="secondary">No values recorded{row.reason ? `; reason: ${row.reason}` : ''}.</Typography.Text>;
  }
  return (
    <Table
      size="small" rowKey="field" pagination={false} scroll={{ x: 'max-content' }}
      dataSource={fields.map((field) => ({ field, before: show(before[field]), after: show(after[field]) }))}
      columns={[
        { title: 'Field', dataIndex: 'field', width: 200 },
        ...(row.oldValue ? [{ title: 'Before', dataIndex: 'before', render: (v: string) => <span style={{ wordBreak: 'break-word' }}>{v}</span> }] : []),
        ...(row.newValue ? [{ title: 'After', dataIndex: 'after', render: (v: string) => <span style={{ wordBreak: 'break-word' }}>{v}</span> }] : []),
      ]}
    />
  );
}

/** A server-paged table of audit rows; each row opens its before and after values. */
export function AuditTable({ filter, onUser, showRecord = true }: { filter: AuditLogFilter; onUser?: (userId: string) => void; showRecord?: boolean }) {
  const [paging, setPaging] = useState({ page: 1, pageSize: 20 });
  const [lastFilter, setLastFilter] = useState(filter);
  if (JSON.stringify(lastFilter) !== JSON.stringify(filter)) {
    // A new filter starts again at its first page.
    setLastFilter(filter);
    setPaging((p) => ({ ...p, page: 1 }));
  }
  const { data, isFetching, isError } = useAuditLogs({ ...filter, ...paging });
  return (
    <Table<AuditLogDto>
      rowKey="id" size="small" loading={isFetching} dataSource={data?.items ?? []} scroll={{ x: 'max-content' }}
      locale={{ emptyText: isError ? 'The audit trail could not be loaded.' : 'No audit rows.' }}
      expandable={{ expandedRowRender: (row) => <AuditValues row={row} /> }}
      pagination={{ current: paging.page, pageSize: paging.pageSize, total: data?.totalCount ?? 0, showSizeChanger: true, showTotal: (n) => `${n} rows` }}
      onChange={(p: TablePaginationConfig) => setPaging({ page: p.current ?? 1, pageSize: p.pageSize ?? 20 })}
      columns={[
        { title: 'When', dataIndex: 'timestamp', width: 170, render: (v: string) => new Date(v).toLocaleString('en-PH') },
        {
          title: 'User', dataIndex: 'userName', width: 170,
          render: (v: string | null, r) => (r.userId && onUser ? <Typography.Link onClick={() => onUser(r.userId!)}>{v ?? r.userId}</Typography.Link> : (v ?? 'System')),
        },
        { title: 'Action', dataIndex: 'action', width: 100, render: (a: AuditAction) => <ActionTag action={a} /> },
        {
          title: 'Table', dataIndex: 'tableName', width: 200,
          render: (v: string, r) => (r.parentTableName ? <span>{v}<br /><Typography.Text type="secondary" style={{ fontSize: 12 }}>of {r.parentTableName}</Typography.Text></span> : v),
        },
        ...(showRecord ? [{ title: 'Record', dataIndex: 'recordId', width: 120, render: (v: string) => <Typography.Text code copyable={{ text: v }}>{v.slice(0, 8)}</Typography.Text> }] : []),
        { title: 'Reason', dataIndex: 'reason', render: (v: string | null) => v ?? '' },
      ]}
    />
  );
}

/**
 * A record's audit history with its child rows (CLAUDE.md §50 "Audit History"), or with `propertyId` a property's whole
 * history; for holders of `audit.view` only, renders nothing for others.
 */
export function AuditHistoryCard({ recordId, propertyId, title = 'Audit History' }: { recordId?: string; propertyId?: string; title?: string }) {
  const can = useCan();
  if (!can('audit.view')) {
    return null;
  }
  return (
    <Card title={title} size="small">
      {propertyId ? <AuditTable filter={{ propertyId }} />
        : recordId ? <AuditTable filter={{ recordId, includeChildren: true }} showRecord={false} /> : <Empty />}
    </Card>
  );
}
