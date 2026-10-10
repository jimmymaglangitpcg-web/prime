import { useState } from 'react';
import { Alert, Button, Card, Col, Empty, Progress, Row, Segmented, Space, Statistic, Table, Tag, Tooltip, Typography, theme } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { Link } from 'react-router-dom';
import dayjs from 'dayjs';
import { useDashboard, type DashboardApprovalDto, type DashboardGeneralRevisionDto, type DashboardGroupDto, type DashboardTransactionDto } from '../../api/dashboard';
import { useApprovalQueue } from '../../api/approvals';
import { useCan, useCurrentUser } from '../../api/offices';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import { WorkflowStatusTag } from '../../components/StatusTag';
import { generalRevisionStatusColor } from '../../lib/generalRevisionTypes';
import { DocNumber } from '../../components/DocNumber';

const count = new Intl.NumberFormat('en-PH');
const area = new Intl.NumberFormat('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const approvalKind: Record<DashboardApprovalDto['kind'], string> = { TaxDeclaration: 'Tax Declaration', Assessment: 'Assessment', Transaction: 'Transaction' };

/**
 * One measure per group as horizontal bars (a single series: no legend, the card's title names it), largest first, the value
 * at each bar's tip in text colour; hover or focus shows the group's figures. A table view gives the same rows for screen
 * readers and copying.
 */
function GroupBars({ title, groups, emptyText, loading }: { title: string; groups: DashboardGroupDto[]; emptyText: string; loading: boolean }) {
  const { token } = theme.useToken();
  const [view, setView] = useState<'Chart' | 'Table'>('Chart');
  // The scale is the named groups'; "Others" sums many and would flatten them, so it is a text row without a bar.
  const max = Math.max(0, ...groups.filter((g) => !g.isOthers).map((g) => g.assessedValue));
  return (
    <Card size="small" title={title} loading={loading}
      extra={groups.length > 0 && <Segmented size="small" options={['Chart', 'Table']} value={view} onChange={(v) => setView(v as 'Chart' | 'Table')} aria-label={`${title}: view`} />}>
      {groups.length === 0 ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={emptyText} />
      ) : view === 'Table' ? (
        <Table<DashboardGroupDto> size="small" pagination={false} rowKey={(g) => g.key ?? g.label} dataSource={groups} scroll={{ x: true }}
          columns={[
            { title: 'Group', key: 'label', render: (_, g) => <>{g.label}{g.detail && <Typography.Text type="secondary"> · {g.detail}</Typography.Text>}</> },
            { title: 'Properties', dataIndex: 'properties', align: 'right', render: (v: number) => count.format(v) },
            { title: 'Market value', dataIndex: 'marketValue', align: 'right', render: (v: number) => formatMoney(v) },
            { title: 'Assessed value', dataIndex: 'assessedValue', align: 'right', render: (v: number) => formatMoney(v) },
          ]} />
      ) : (
        <div role="list" aria-label={title} style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {groups.map((g) => (
            <Tooltip key={g.key ?? g.label} title={
              <div>
                <div><strong>{g.label}</strong>{g.detail && ` · ${g.detail}`}</div>
                <div>Properties: {count.format(g.properties)}</div>
                <div>Market value: {formatMoney(g.marketValue)}</div>
                <div>Assessed value: {formatMoney(g.assessedValue)}</div>
              </div>
            }>
              <div role="listitem" tabIndex={0} style={{ display: 'grid', gridTemplateColumns: 'minmax(96px, 34%) 1fr', columnGap: 12, alignItems: 'center' }}
                aria-label={`${g.label}${g.detail ? `, ${g.detail}` : ''}: assessed value ${formatMoney(g.assessedValue)}, ${count.format(g.properties)} properties`}>
                <Typography.Text ellipsis style={{ fontSize: 13 }} type={g.isOthers ? 'secondary' : undefined}>{g.label}</Typography.Text>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
                  {!g.isOthers && <div style={{
                    height: 16, flex: '0 0 auto', minWidth: g.assessedValue > 0 ? 2 : 0,
                    width: `calc((100% - 120px) * ${max > 0 ? g.assessedValue / max : 0})`,
                    background: token.colorPrimary, borderRadius: '0 4px 4px 0',
                  }} />}
                  <Typography.Text style={{ fontSize: 12, fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>{formatMoney(g.assessedValue)}</Typography.Text>
                </div>
              </div>
            </Tooltip>
          ))}
        </div>
      )}
    </Card>
  );
}

/** An open general revision: its items in the jurisdiction valued, posted and declared. */
function RevisionProgress({ r, canOpen }: { r: DashboardGeneralRevisionDto; canOpen: boolean }) {
  const counted = Math.max(0, r.items - r.excluded);
  const percent = (n: number) => (counted === 0 ? 0 : Math.round((n / counted) * 100));
  const step = (label: string, n: number) => (
    <div>
      <Typography.Text style={{ fontSize: 13 }}>{label}: {count.format(n)} of {count.format(counted)}</Typography.Text>
      <Progress percent={percent(n)} size="small" aria-label={`${label}: ${percent(n)} percent`} />
    </div>
  );
  return (
    <Card size="small" type="inner"
      title={canOpen ? <Link to={`/general-revision/${r.id}`}>Revision {r.revisionYear}</Link> : `Revision ${r.revisionYear}`}
      extra={<Tag color={generalRevisionStatusColor[r.status]}>{r.status === 'InProgress' ? 'In progress' : r.status}</Tag>}>
      <Typography.Paragraph type="secondary" style={{ fontSize: 13, marginBottom: 8 }}>
        {r.scope || 'No municipality'} · {count.format(r.items)} items{r.excluded > 0 && `, ${count.format(r.excluded)} excluded`}
        {r.failed > 0 && <>, <Typography.Text type="danger">{count.format(r.failed)} failed</Typography.Text></>}
      </Typography.Paragraph>
      {step('Valued', r.valued)}
      {step('Posted', r.posted)}
      {step('Declared', r.declared)}
    </Card>
  );
}

/**
 * The dashboard (CLAUDE.md §55; docs/analysis/reporting.md §4.3): the user's jurisdiction as of today, from the database.
 * Totals and charts are cached by the API for a minute; an empty database shows zeros.
 */
export function DashboardPage() {
  const dashboard = useDashboard();
  const queue = useApprovalQueue();
  const me = useCurrentUser();
  const can = useCan();
  const d = dashboard.data;
  const f = d?.figures;
  const where = me.data?.provinceWide ? 'the whole province' : (me.data?.officeName ?? 'your jurisdiction');
  const pin = (propertyId: string, value: string) => (
    <span style={{ whiteSpace: 'nowrap' }}>{can('property.view') ? <Link to={`/properties/${propertyId}`}><DocNumber>{value}</DocNumber></Link> : <DocNumber>{value}</DocNumber>}</span>
  );

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap', alignItems: 'flex-start' }}>
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>Dashboard</Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {d ? <>FAAS in force on {dayjs(d.asOf).format('D MMMM YYYY')}, {where}. Figures read at {dayjs(d.computedAt).format('h:mm A')}.</> : 'Loading…'}
          </Typography.Paragraph>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => { dashboard.refetch(); queue.refetch(); }} loading={dashboard.isFetching}>Refresh</Button>
      </div>

      {dashboard.isError && (
        <Alert type="error" showIcon title="Could not load the dashboard"
          description={dashboard.error instanceof ApiRequestError ? dashboard.error.apiError.message : (dashboard.error as Error).message} />
      )}

      <Row gutter={[12, 12]}>
        <Col xs={12} md={8} xl={4}>
          <Card size="small" loading={!f}><Statistic title="Properties" value={f?.properties ?? 0} formatter={(v) => count.format(Number(v))} />
            {f && <Typography.Text type="secondary" style={{ fontSize: 12 }}>{count.format(f.propertiesWithFaas)} with a FAAS in force</Typography.Text>}</Card>
        </Col>
        <Col xs={12} md={8} xl={4}>
          <Card size="small" loading={!f}><Statistic title="Parcels" value={f?.parcels ?? 0} formatter={(v) => count.format(Number(v))} />
            {f && <Typography.Text type="secondary" style={{ fontSize: 12 }}>{count.format(f.unitsInForce)} units in force</Typography.Text>}</Card>
        </Col>
        <Col xs={24} md={8} xl={4}>
          <Card size="small" loading={!f}>
            <Statistic title="Land area" value={(f?.landAreaSqm ?? 0) / 10000} formatter={(v) => `${area.format(Number(v))} ha`} />
            {f && <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {f.unconvertedLandUnits > 0 ? `${count.format(f.unconvertedLandUnits)} land unit(s) in other units not counted` : `${area.format(f.landAreaSqm)} sq m`}
            </Typography.Text>}
          </Card>
        </Col>
        <Col xs={24} md={8} xl={4}>
          <Card size="small" loading={!f}>
            <Statistic title="Market value" value={(f?.taxableMarketValue ?? 0) + (f?.exemptMarketValue ?? 0)} formatter={(v) => formatMoney(Number(v))} />
            {f && <Typography.Text type="secondary" style={{ fontSize: 12 }}>Exempt: {formatMoney(f.exemptMarketValue)}</Typography.Text>}
          </Card>
        </Col>
        <Col xs={24} md={8} xl={4}>
          <Card size="small" loading={!f}>
            <Statistic title="Assessed value, taxable" value={f?.taxableAssessedValue ?? 0} formatter={(v) => formatMoney(Number(v))} />
            {f && <Typography.Text type="secondary" style={{ fontSize: 12 }}>Exempt: {formatMoney(f.exemptAssessedValue)}</Typography.Text>}
          </Card>
        </Col>
        <Col xs={24} md={8} xl={4}>
          <Card size="small" loading={queue.isLoading}>
            <Statistic title="Awaiting my approval" value={queue.data?.length ?? 0} />
            <Link to="/approvals" style={{ fontSize: 12 }}>Open the approvals inbox</Link>
          </Card>
        </Col>
      </Row>

      <Row gutter={[12, 12]}>
        <Col xs={24} lg={12}>
          <GroupBars title="Assessed value by classification" groups={d?.byClassification ?? []} emptyText="No FAAS in force" loading={!d && !dashboard.isError} />
        </Col>
        <Col xs={24} lg={12}>
          <GroupBars title="Assessed value by barangay (top ten)" groups={d?.byBarangay ?? []} emptyText="No FAAS in force" loading={!d && !dashboard.isError} />
        </Col>
      </Row>

      <Card size="small" title="General revision" loading={!d && !dashboard.isError}>
        {d && d.generalRevisions.length === 0 ? (
          <Typography.Text type="secondary">No general revision is planned or in progress.</Typography.Text>
        ) : (
          <Row gutter={[12, 12]}>
            {(d?.generalRevisions ?? []).map((r) => (
              <Col key={r.id} xs={24} md={12} xl={8}><RevisionProgress r={r} canOpen={can('gr.view')} /></Col>
            ))}
          </Row>
        )}
      </Card>

      <Row gutter={[12, 12]}>
        <Col xs={24} xl={12}>
          <Card size="small" title="Recent transactions">
            <Table<DashboardTransactionDto> size="small" rowKey="id" pagination={false} loading={!d} dataSource={d?.recentTransactions ?? []} scroll={{ x: true }}
              locale={{ emptyText: 'No transactions yet' }}
              columns={[
                { title: 'Recorded', dataIndex: 'createdAt', render: (v: string) => <span style={{ whiteSpace: 'nowrap' }}>{dayjs(v).format('YYYY-MM-DD')}</span> },
                { title: 'Transaction', key: 'type', render: (_, t) => <>{t.typeName}{t.transactionNumber && <Typography.Text type="secondary"> · {t.transactionNumber}</Typography.Text>}</> },
                { title: 'PIN', key: 'pin', render: (_, t) => pin(t.propertyId, t.pin) },
                { title: 'Status', dataIndex: 'status', render: (s: DashboardTransactionDto['status']) => <WorkflowStatusTag status={s} /> },
              ]} />
          </Card>
        </Col>
        <Col xs={24} xl={12}>
          <Card size="small" title="Recent approvals">
            <Table<DashboardApprovalDto> size="small" rowKey={(a) => `${a.kind}-${a.id}`} pagination={false} loading={!d} dataSource={d?.recentApprovals ?? []} scroll={{ x: true }}
              locale={{ emptyText: 'No approvals yet' }}
              columns={[
                { title: 'Approved', dataIndex: 'approvedAt', render: (v: string) => <span style={{ whiteSpace: 'nowrap' }}>{dayjs(v).format('YYYY-MM-DD HH:mm')}</span> },
                { title: 'Record', key: 'record', render: (_, a) => <>{approvalKind[a.kind]} <Typography.Text type="secondary">{a.reference}</Typography.Text></> },
                { title: 'PIN', key: 'pin', render: (_, a) => pin(a.propertyId, a.pin) },
                { title: 'By', dataIndex: 'approvedBy', render: (v: string | null) => v ?? '—' },
              ]} />
          </Card>
        </Col>
      </Row>
    </Space>
  );
}
