import { Button, Space, message } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import { useCan } from '../api/offices';
import { useRunDownload, type ReportFileFormat, type RunExportKind } from '../api/reports';
import { ApiRequestError } from '../lib/apiClient';

/**
 * CSV and Excel downloads of a register run or a sales report run (docs/analysis/reporting.md, step R3), for users with
 * records.export. An issued run downloads what was printed; one not issued is read from the records now.
 */
export function RunDownloadButtons({ kind, id }: { kind: RunExportKind; id: string }) {
  const can = useCan();
  const download = useRunDownload();
  const [toast, toastContext] = message.useMessage();
  if (!can('records.export')) return null;
  return (
    <Space size={4}>
      {toastContext}
      {(['csv', 'xlsx'] as ReportFileFormat[]).map((format) => (
        <Button key={format} size="small" icon={<DownloadOutlined />}
          loading={download.isPending && download.variables?.format === format && download.variables.id === id}
          onClick={() => download.mutate({ kind, id, format }, {
            onError: (e) => toast.error(e instanceof ApiRequestError ? e.apiError.message : (e as Error).message),
          })}>
          {format === 'csv' ? 'CSV' : 'Excel'}
        </Button>
      ))}
    </Space>
  );
}
