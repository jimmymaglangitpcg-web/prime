import { useMemo } from 'react';
import { Alert, Button, Space, Spin, Typography } from 'antd';
import { ArrowLeftOutlined, PrinterOutlined } from '@ant-design/icons';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { usePrintAudit } from '../../api/audit';
import { useReportPrint, useReports, type ReportCell, type ReportColumn, type ReportRunRequest } from '../../api/reports';
import { ApiRequestError } from '../../lib/apiClient';
import { formatCell, numeric } from '../../lib/reportCells';

function parseRun(text: string | null): ReportRunRequest | undefined {
  try {
    const value: unknown = text ? JSON.parse(text) : undefined;
    return value && typeof value === 'object' ? (value as ReportRunRequest) : undefined;
  } catch {
    return undefined;
  }
}

const capitalise = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);

/** A total or subtotal row, set in bold: one whose text cells name it so. */
const isTotal = (row: ReportCell[]) => row.some((cell) => typeof cell === 'string' && /^(Sub)?total/i.test(cell));

/** Neighbouring columns whose titles share a prefix before ": " ("At the start: RPUs, taxable"), printed under one heading. */
function columnGroups(columns: ReportColumn[]) {
  const groups: { title: string | null; columns: ReportColumn[] }[] = [];
  for (const column of columns) {
    const split = column.title.indexOf(': ');
    const title = split > 0 ? column.title.slice(0, split) : null;
    const last = groups[groups.length - 1];
    if (title && last?.title === title) last.columns.push(column);
    else groups.push({ title, columns: [column] });
  }
  return groups;
}

/**
 * The print view of a report (CLAUDE.md §57 "Print"; docs/analysis/reporting.md §10, Q21): the whole report up to the
 * download limit, with the file's header block, the totals and the notes, on A4 landscape. PRIME's provisional layout:
 * the LAM's official layouts come in as form content (CLAUDE.md §118). A BLGF report carries a line for the assessor's
 * certification. Each print is recorded as an EXPORT row.
 */
export function ReportPrintPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const code = params.get('report') ?? undefined;
  const run = useMemo(() => parseRun(params.get('run')), [params]);
  const { data: reports = [] } = useReports();
  const definition = reports.find((r) => r.code === code);
  const { data, error, isLoading } = useReportPrint(code, run);
  const groups = useMemo(() => columnGroups(data?.columns ?? []), [data]);
  usePrintAudit(data ? { tableName: 'Reports', what: `${data.title} (${data.parameterLines.join('; ')}), ${data.rows.length} rows` } : null);

  const back = () => navigate(code ? `/reports?report=${code}` : '/reports');
  if (!code || !run) {
    return <Alert type="error" showIcon title="Nothing to print" description="Open the print view from a report's Print button." />;
  }
  return (
    <div className="report-print">
      <Space className="no-print" style={{ marginBottom: 16 }} wrap>
        <Button icon={<ArrowLeftOutlined />} onClick={back}>Back to the report</Button>
        <Button type="primary" icon={<PrinterOutlined />} disabled={!data} onClick={() => window.print()}>Print</Button>
        {data && data.totalRows > data.rows.length && (
          <Typography.Text type="warning">Only the first {data.rows.length.toLocaleString('en-PH')} of {data.totalRows.toLocaleString('en-PH')} rows are shown.</Typography.Text>
        )}
      </Space>
      {isLoading && <Spin />}
      {error && (
        <Alert type="error" showIcon title="The report could not be produced"
          description={error instanceof ApiRequestError ? error.apiError.message : (error as Error).message} />
      )}
      {data && (
        <>
          <header className="report-print-header">
            {(data.headerLines ?? [data.title, ...data.parameterLines]).map((line, i) => (
              <div key={line} className={line === data.title ? 'report-print-title' : i === 0 ? 'report-print-lgu' : undefined}>{line}</div>
            ))}
          </header>
          <table className="report-print-table">
            <thead>
              {groups.some((g) => g.title) ? (
                <>
                  <tr>
                    {groups.map((g, i) => g.title
                      ? <th key={i} colSpan={g.columns.length} className="group">{g.title}</th>
                      : g.columns.map((c) => <th key={c.key} rowSpan={2}>{c.title}</th>))}
                  </tr>
                  <tr>
                    {groups.filter((g) => g.title).flatMap((g) => g.columns.map((c) => (
                      <th key={c.key} className="num">{capitalise(c.title.slice(g.title!.length + 2))}</th>
                    )))}
                  </tr>
                </>
              ) : (
                <tr>{data.columns.map((c) => <th key={c.key} className={numeric(c) ? 'num' : undefined}>{c.title}</th>)}</tr>
              )}
            </thead>
            <tbody>
              {data.rows.map((row, r) => (
                <tr key={r} className={isTotal(row) ? 'total' : undefined}>{data.columns.map((c, i) => <td key={c.key} className={numeric(c) ? 'num' : undefined}>{formatCell(c, row[i])}</td>)}</tr>
              ))}
            </tbody>
            {data.totals && (
              <tfoot>
                <tr>{data.columns.map((c, i) => <td key={c.key} className={numeric(c) ? 'num' : undefined}>{formatCell(c, data.totals![i])}</td>)}</tr>
              </tfoot>
            )}
          </table>
          {data.notes.map((note) => <p key={note} className="report-print-note">{note}</p>)}
          {definition?.group === 'BLGF' && (
            <div className="report-print-signature">
              <div>Prepared and certified correct by:</div>
              <div className="report-print-line" />
              <div>Assessor</div>
            </div>
          )}
          <p className="report-print-note">Provisional layout of PRIME; the official layout is loaded as form content.</p>
        </>
      )}
    </div>
  );
}
