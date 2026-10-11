import { useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Button, Card, DatePicker, Empty, Form, Input, Select, Space, Table, Typography } from 'antd';
import { DownloadOutlined, PrinterOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useBarangays } from '../../api/referenceData';
import { useCan } from '../../api/offices';
import {
  printUrl, useReportDownload, useReportPreview, useReports, type ReportCell, type ReportFileFormat, type ReportRunRequest,
} from '../../api/reports';
import { MunicipalityFilter } from '../../components/MunicipalityFilter';
import { ApiRequestError } from '../../lib/apiClient';
import { formatCell, numeric } from '../../lib/reportCells';

const count = new Intl.NumberFormat('en-PH');

/** A report row with its position in the whole report, as the table's key. */
interface KeyedRow { key: number; cells: ReportCell[] }

interface ParameterForm {
  asOf?: Dayjs;
  period?: [Dayjs, Dayjs];
  month?: Dayjs;
  halfYear?: Dayjs;
  half?: 1 | 2;
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

/** The first day of the period a report names: a month, a half-year, else the period's start. */
function fromDate(values: ParameterForm, parameters: string[]) {
  if (parameters.includes('Month') && values.month) return values.month.startOf('month').format('YYYY-MM-DD');
  if (parameters.includes('HalfYear') && values.halfYear) return `${values.halfYear.year()}-${values.half === 2 ? '07' : '01'}-01`;
  return values.period?.[0].format('YYYY-MM-DD') ?? null;
}

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
  const navigate = useNavigate();

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
      fromDate: fromDate(values, report?.parameters ?? []),
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
          <Form form={form} layout="vertical" initialValues={{
            asOf: dayjs(), period: [dayjs().startOf('year'), dayjs()], month: dayjs().subtract(1, 'month'), halfYear: dayjs(),
            half: dayjs().month() < 6 ? 1 : 2,
          }} onFinish={submit}
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
              {report.parameters.includes('Month') && (
                <Form.Item name="month" label="Month" rules={[{ required: true, message: 'Choose the month' }]}>
                  <DatePicker picker="month" disabledDate={(d) => d.isAfter(dayjs(), 'month')} allowClear={false} />
                </Form.Item>
              )}
              {report.parameters.includes('HalfYear') && (
                <>
                  <Form.Item name="halfYear" label="Year" rules={[{ required: true, message: 'Choose the year' }]}>
                    <DatePicker picker="year" disabledDate={(d) => d.isAfter(dayjs(), 'year')} allowClear={false} />
                  </Form.Item>
                  <Form.Item name="half" label="Half-year" rules={[{ required: true }]}>
                    <Select style={{ width: 160 }} options={[{ value: 1, label: 'January–June' }, { value: 2, label: 'July–December' }]} />
                  </Form.Item>
                </>
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
              <Button icon={<PrinterOutlined />} disabled={tooLarge || data.totalRows === 0} onClick={() => navigate(printUrl(report.code, run))}>
                Print
              </Button>
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
