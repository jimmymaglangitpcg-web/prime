import { useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Button, Card, DatePicker, Empty, Form, Input, Select, Space, Table, Typography } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useSearchParams } from 'react-router-dom';
import { useBarangays } from '../../api/referenceData';
import { useCan } from '../../api/offices';
import {
  useReportDownload, useReportPreview, useReports, type ReportCell, type ReportColumn, type ReportFileFormat, type ReportRunRequest,
} from '../../api/reports';
import { MunicipalityFilter } from '../../components/MunicipalityFilter';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';

const area = new Intl.NumberFormat('en-PH', { maximumFractionDigits: 4 });
const count = new Intl.NumberFormat('en-PH');

function formatCell(column: ReportColumn, value: ReportCell) {
  if (value === null || value === undefined || value === '') return '';
  if (typeof value === 'number') {
    if (column.type === 'Money') return formatMoney(value);
    if (column.type === 'Area') return area.format(value);
    if (column.type === 'Integer') return count.format(value);
  }
  return String(value);
}

const numeric = (column: ReportColumn) => column.type === 'Money' || column.type === 'Area' || column.type === 'Integer';

/** A report row with its position in the whole report, as the table's key. */
interface KeyedRow { key: number; cells: ReportCell[] }

interface ParameterForm {
  asOf?: Dayjs;
  period?: [Dayjs, Dayjs];
  municipalityId?: string;
  barangayId?: string;
  status?: string;
  transactionCode?: string;
  pin?: string;
}

/** The Tax Declaration statuses the TD list filters by. */
const tdStatuses = [
  { value: 'Draft', label: 'Draft' },
  { value: 'PendingReview', label: 'Pending review' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'Cancelled', label: 'Cancelled' },
];

const blank = (text?: string) => (text?.trim() ? text.trim() : null);

/**
 * Reports (CLAUDE.md §57; docs/analysis/reporting.md §4.1): choose a report and its parameters, see it a page at a time,
 * and download the whole of it as CSV or Excel. Figures are the FAAS in force on the as-of date, in the user's
 * jurisdiction; a download is recorded in the audit trail.
 */
export function ReportsPage() {
  const [params, setParams] = useSearchParams();
  const { data: reports = [], isLoading } = useReports();
  const can = useCan();
  const code = params.get('report') ?? undefined;
  const report = reports.find((r) => r.code === code);
  const [form] = Form.useForm<ParameterForm>();
  const municipalityId = Form.useWatch('municipalityId', form);
  const { data: barangays = [] } = useBarangays(municipalityId);
  const [run, setRun] = useState<ReportRunRequest>();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const preview = useReportPreview(report?.code, run, page, pageSize);
  const download = useReportDownload();

  const groups = useMemo(() => {
    const byGroup = new Map<string, typeof reports>();
    reports.forEach((r) => byGroup.set(r.group, [...(byGroup.get(r.group) ?? []), r]));
    return [...byGroup.entries()];
  }, [reports]);

  function choose(next: string) {
    setParams({ report: next });
    setRun(undefined);
    setPage(1);
  }

  function submit(values: ParameterForm) {
    setPage(1);
    setRun({
      asOf: (values.asOf ?? dayjs()).format('YYYY-MM-DD'),
      municipalityId: values.municipalityId ?? null,
      barangayId: values.barangayId ?? null,
      fromDate: values.period?.[0].format('YYYY-MM-DD') ?? null,
      toDate: values.period?.[1].format('YYYY-MM-DD') ?? null,
      status: values.status ?? null,
      transactionCode: blank(values.transactionCode),
      pin: blank(values.pin),
    });
  }

  const data = preview.data;
  const columns = (data?.columns ?? report?.columns ?? []).map((column, index) => ({
    key: column.key,
    title: column.title,
    align: numeric(column) ? ('right' as const) : undefined,
    render: (_: unknown, row: KeyedRow) => formatCell(column, row.cells[index]),
  }));
  const offset = ((data?.page ?? 1) - 1) * (data?.pageSize ?? pageSize);
  const rows: KeyedRow[] = (data?.rows ?? []).map((cells, i) => ({ key: offset + i, cells }));
  const tooLarge = !!data && data.totalRows > data.syncRowLimit;

  // The table's scroll area takes focus so a keyboard user can scroll a wide report (WCAG 2.1.1, axe scrollable-region-focusable).
  const tableArea = useRef<HTMLDivElement>(null);
  useEffect(() => {
    tableArea.current?.querySelectorAll<HTMLElement>('.ant-table-content').forEach((area) => {
      area.tabIndex = 0;
      area.setAttribute('role', 'region');
      area.setAttribute('aria-label', `${data?.title ?? 'Report'} table`);
    });
  });
  const error = preview.error ?? download.error;

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Title level={3} style={{ margin: 0 }}>Reports</Typography.Title>
      <Card>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 24, alignItems: 'flex-start' }}>
          <Space orientation="vertical" size={4} style={{ width: 'min(360px, 100%)' }}>
            <Typography.Text strong id="report-choice">Report</Typography.Text>
            <Select aria-labelledby="report-choice" loading={isLoading} value={report?.code} onChange={choose} placeholder="Choose a report"
              style={{ width: '100%' }}
              options={groups.map(([group, items]) => ({ label: group, options: items.map((r) => ({ value: r.code, label: r.title })) }))} />
          </Space>
          {report && <Typography.Paragraph type="secondary" style={{ maxWidth: 560, marginTop: 26, marginBottom: 0 }}>{report.description}</Typography.Paragraph>}
        </div>
        {report && (
          <Form form={form} layout="vertical" initialValues={{ asOf: dayjs(), period: [dayjs().startOf('year'), dayjs()] }} onFinish={submit}
            style={{ marginTop: 16 }}>
            <Space wrap align="end">
              {report.parameters.includes('AsOf') && (
                <Form.Item name="asOf" label="As of" rules={[{ required: true, message: 'Choose the date' }]}>
                  <DatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} allowClear={false} />
                </Form.Item>
              )}
              {report.parameters.includes('Period') && (
                <Form.Item name="period" label="Period" rules={[{ required: true, message: 'Choose the period' }]}>
                  <DatePicker.RangePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} allowClear={false} />
                </Form.Item>
              )}
              {report.parameters.includes('Municipality') && (
                <Form.Item name="municipalityId" label="Municipality">
                  <MunicipalityFilter value={municipalityId} placeholder="All in your jurisdiction"
                    onChange={(id) => form.setFieldsValue({ municipalityId: id, barangayId: undefined })} />
                </Form.Item>
              )}
              {report.parameters.includes('Barangay') && (
                <Form.Item name="barangayId" label="Barangay">
                  <Select allowClear showSearch optionFilterProp="label" disabled={!municipalityId} style={{ width: 240 }}
                    placeholder={municipalityId ? 'All barangays' : 'Choose a municipality first'}
                    options={barangays.map((b) => ({ value: b.id, label: b.name }))} />
                </Form.Item>
              )}
              {report.parameters.includes('TdStatus') && (
                <Form.Item name="status" label="Status">
                  <Select allowClear placeholder="Any status" options={tdStatuses} style={{ width: 160 }} />
                </Form.Item>
              )}
              {report.parameters.includes('TransactionCode') && (
                <Form.Item name="transactionCode" label="Transaction code" rules={[{ max: 20 }]}>
                  <Input allowClear placeholder="Any" style={{ width: 140 }} />
                </Form.Item>
              )}
              {report.parameters.includes('Pin') && (
                <Form.Item name="pin" label="PIN (or its first part)" rules={[{ max: 100 }]}>
                  <Input allowClear placeholder="Any" style={{ width: 220 }} />
                </Form.Item>
              )}
              <Form.Item>
                <Button type="primary" htmlType="submit" loading={preview.isFetching}>Run</Button>
              </Form.Item>
            </Space>
          </Form>
        )}
      </Card>

      {error && (
        <Alert type="error" showIcon title="The report could not be produced"
          description={error instanceof ApiRequestError ? error.apiError.message : (error as Error).message} />
      )}

      {report && run && data && (
        <Card
          title={data.title}
          extra={can('records.export') && (
            <Space>
              {(['csv', 'xlsx'] as ReportFileFormat[]).map((format) => (
                <Button key={format} icon={<DownloadOutlined />} disabled={tooLarge || data.totalRows === 0}
                  loading={download.isPending && download.variables?.format === format}
                  onClick={() => download.mutate({ code: report.code, parameters: run, format })}>
                  {format === 'csv' ? 'CSV' : 'Excel'}
                </Button>
              ))}
            </Space>
          )}>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>{data.parameterLines.join(' · ')}</Typography.Paragraph>
          {tooLarge && (
            <Alert type="warning" showIcon style={{ marginBottom: 12 }} title="Too large to download"
              description={`This report has ${count.format(data.totalRows)} rows; a download holds at most ${count.format(data.syncRowLimit)}. Choose a municipality or barangay to narrow it.`} />
          )}
          <div ref={tableArea}>
          <Table<KeyedRow>
            size="small"
            rowKey="key"
            columns={columns}
            dataSource={rows}
            loading={preview.isFetching}
            scroll={{ x: 'max-content' }}
            locale={{ emptyText: <Empty description="Nothing to report for these parameters." /> }}
            pagination={{
              current: page, pageSize, total: data.totalRows, showSizeChanger: true, pageSizeOptions: [25, 50, 100, 200],
              showTotal: (total) => `${count.format(total)} rows`,
              onChange: (p, s) => { setPage(s === pageSize ? p : 1); setPageSize(s); },
            }}
            summary={() => data.totals && data.rows.length > 0 ? (
              <Table.Summary fixed>
                <Table.Summary.Row>
                  {data.columns.map((column, index) => (
                    <Table.Summary.Cell key={column.key} index={index} align={numeric(column) ? 'right' : undefined}>
                      <Typography.Text strong>{formatCell(column, data.totals![index])}</Typography.Text>
                    </Table.Summary.Cell>
                  ))}
                </Table.Summary.Row>
              </Table.Summary>
            ) : null}
          />
          </div>
          {data.notes.map((note) => (
            <Typography.Paragraph key={note} type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>{note}</Typography.Paragraph>
          ))}
        </Card>
      )}
    </Space>
  );
}
