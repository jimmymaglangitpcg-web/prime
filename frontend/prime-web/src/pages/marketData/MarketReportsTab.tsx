import { useState } from 'react';
import { Alert, Button, DatePicker, Select, Space, Table, Typography } from 'antd';
import dayjs from 'dayjs';
import { useCreateMarketReport, useMarketReports } from '../../api/marketData';
import { useAllMunicipalities } from '../../api/referenceData';
import { ApiRequestError } from '../../lib/apiClient';
import { marketDataReportKindLabel, type MarketDataReportKind, type MarketDataReportRunDto } from '../../lib/marketDataTypes';
import { PrintFormButton } from '../../components/PrintFormButton';
import { RunDownloadButtons } from '../../components/RunDownloadButtons';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);

/**
 * The abstracts and the sales report for a city/municipality and a period (LAM 2025 Book I pp.22–25). Printing
 * issues a frozen copy of the rows as they are then.
 */
export function MarketReportsTab({ municipalityId }: { municipalityId?: string }) {
  const { data: runs = [], isLoading } = useMarketReports();
  const { data: municipalities = [] } = useAllMunicipalities();
  const create = useCreateMarketReport();
  const [kind, setKind] = useState<MarketDataReportKind>('TransactionsAbstract');
  const [municipality, setMunicipality] = useState<string | undefined>(municipalityId);
  const [period, setPeriod] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([dayjs().startOf('year'), dayjs()]);

  return (
    <>
      <Space wrap align="end" style={{ marginBottom: 12 }}>
        <Select aria-label="Report" value={kind} onChange={setKind} style={{ width: 300 }}
          options={(Object.keys(marketDataReportKindLabel) as MarketDataReportKind[]).map((k) => ({ value: k, label: marketDataReportKindLabel[k] }))} />
        <Select aria-label="City/municipality of the report" showSearch optionFilterProp="label" value={municipality} onChange={setMunicipality}
          placeholder="City/municipality" style={{ width: 220 }} options={municipalities.map((m) => ({ value: m.id, label: m.name }))} />
        <DatePicker.RangePicker value={period} onChange={(v) => v?.[0] && v[1] && setPeriod([v[0], v[1]])} />
        <Button type="primary" disabled={!municipality} loading={create.isPending}
          onClick={() => municipality && create.mutate({ kind, municipalityId: municipality, fromDate: period[0].format('YYYY-MM-DD'), toDate: period[1].format('YYYY-MM-DD') })}>
          Prepare
        </Button>
      </Space>
      <Typography.Paragraph type="secondary">
        The sales report lists accepted sales only. Its frequency (yearly or every three years) awaits the Provincial Assessor's answer (E6).
      </Typography.Paragraph>
      {create.isError && <Alert type="error" showIcon closable style={{ marginBottom: 8 }} title="Could not prepare" description={errorText(create.error)} />}
      <Table<MarketDataReportRunDto> scroll={{ x: true }} rowKey="id" size="small" loading={isLoading} dataSource={runs} pagination={{ pageSize: 10 }}
        locale={{ emptyText: 'Nothing prepared yet' }}
        columns={[
          { title: 'Report', dataIndex: 'kind', render: (k: MarketDataReportKind) => marketDataReportKindLabel[k] },
          { title: 'City/municipality', dataIndex: 'municipalityName' },
          { title: 'Period', render: (_, r) => `${r.fromDate} to ${r.toDate}` },
          { title: 'Prepared', dataIndex: 'createdAt', render: (v: string) => dayjs(v).format('YYYY-MM-DD HH:mm') },
          {
            title: '', render: (_, r) => (
              <Space size={4} wrap>
                <PrintFormButton formCode={r.formCode} subjectId={r.id} issuable />
                {r.kind === 'SalesReport' && <RunDownloadButtons kind="sales-report-runs" id={r.id} />}
              </Space>
            ),
          },
        ]} />
    </>
  );
}
