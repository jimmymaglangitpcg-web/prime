import { useRef, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { Alert, Button, Skeleton, Space, Tag, Tooltip, Typography } from 'antd';
import { ArrowLeftOutlined, PrinterOutlined } from '@ant-design/icons';
import { useFormPreview, useIssuedForm } from '../../api/forms';
import { ApiRequestError } from '../../lib/apiClient';
import type { FormAuthority } from '../../lib/types';

const authorityLabel: Record<FormAuthority, string> = {
  PrimeProvisional: 'PRIME provisional — not official',
  Lam: 'LAM',
  Blgf: 'BLGF',
  LguOrdinance: 'LGU ordinance',
  Other: 'Other',
  Mrpaao: 'MRPAAO 2004 reference layout (superseded manual)',
};

const errorMessage = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message ?? 'Not found');

/**
 * Shows an issued form (/documents/:id) or an unsaved preview
 * (/documents/preview?form=CODE&subject=ID). The HTML was rendered by the
 * server from a frozen snapshot (docs/FORMS-REVISION-PLAN.md §4.3). It is
 * shown in a sandboxed frame with scripts disabled; Print prints only the form.
 */
export function FormDocumentPage() {
  const { id } = useParams<{ id: string }>();
  const [params] = useSearchParams();
  const isPreview = !id;
  const issued = useIssuedForm(id);
  const preview = useFormPreview(isPreview ? params.get('form') ?? undefined : undefined, isPreview ? params.get('subject') ?? undefined : undefined);
  const navigate = useNavigate();
  const frame = useRef<HTMLIFrameElement>(null);
  const [height, setHeight] = useState(1100);

  const query = isPreview ? preview : issued;
  if (query.isLoading) {
    return <Skeleton active />;
  }
  if (query.isError || !query.data) {
    return <Alert type="error" showIcon title="Could not load the form" description={errorMessage(query.error)} />;
  }

  const html = (isPreview ? preview.data?.html : issued.data?.html) ?? '';
  const meta = isPreview ? preview.data! : issued.data!;
  const version = isPreview ? preview.data!.formVersion : issued.data!.formVersion;

  return (
    <div>
      <Space className="no-print" wrap style={{ marginBottom: 12 }}>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(-1)}>
          Back
        </Button>
        <Button type="primary" icon={<PrinterOutlined />} onClick={() => frame.current?.contentWindow?.print()}>
          Print
        </Button>
        <Typography.Text strong>
          {meta.title} · v{version}
        </Typography.Text>
        <Tag color={meta.authority === 'PrimeProvisional' ? 'red' : 'blue'}>{authorityLabel[meta.authority]}</Tag>
        {isPreview ? (
          <Tag color="orange">PREVIEW — not issued</Tag>
        ) : (
          <>
            <Tag color={issued.data!.status === 'Posted' ? 'green' : 'red'}>{issued.data!.status === 'Posted' ? 'Issued' : issued.data!.status}</Tag>
            {issued.data!.documentNumber && <Tag>No. {issued.data!.documentNumber}</Tag>}
            <Tooltip title={`SHA-256 of the issued HTML: ${issued.data!.renderedHtmlSha256}`}>
              <Tag>Issued {new Date(issued.data!.issuedAt).toLocaleString('en-PH')}</Tag>
            </Tooltip>
          </>
        )}
      </Space>

      {isPreview && preview.data!.issueBlocker && (
        <Alert className="no-print" type="warning" showIcon style={{ marginBottom: 12 }} title="Preview only" description={preview.data!.issueBlocker} />
      )}
      {!isPreview && issued.data!.cancellationReason && (
        <Alert className="no-print" type="error" showIcon style={{ marginBottom: 12 }} title="This issued form was cancelled" description={issued.data!.cancellationReason} />
      )}

      <iframe
        ref={frame}
        title={meta.title}
        srcDoc={html}
        // No allow-scripts: nothing in the form can run. allow-same-origin lets Print and sizing reach the frame.
        sandbox="allow-same-origin allow-modals"
        onLoad={() => setHeight((frame.current?.contentDocument?.documentElement.scrollHeight ?? 1080) + 20)}
        style={{ width: '100%', maxWidth: 900, height, border: '1px solid #d9d9d9', background: '#fff', display: 'block' }}
      />
    </div>
  );
}
