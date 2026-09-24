import { useNavigate } from 'react-router-dom';
import { Button, message } from 'antd';
import { PrinterOutlined } from '@ant-design/icons';
import { useIssueForm } from '../api/forms';
import { ApiRequestError } from '../lib/apiClient';

/**
 * Prints a record through its configured form (docs/FORMS-REVISION-PLAN.md).
 * An issuable record is issued (idempotent — reprints return the same frozen
 * issue) and shown; otherwise the form opens as an unsaved preview.
 */
export function PrintFormButton({ formCode, subjectId, issuable, label = 'Print' }: {
  formCode: string;
  subjectId: string;
  issuable: boolean;
  label?: string;
}) {
  const navigate = useNavigate();
  const issue = useIssueForm();
  const [toast, toastContext] = message.useMessage();

  function handleClick() {
    if (!issuable) {
      navigate(`/documents/preview?form=${encodeURIComponent(formCode)}&subject=${subjectId}`);
      return;
    }
    issue.mutate(
      { formCode, subjectId },
      {
        onSuccess: (issued) => navigate(`/documents/${issued.id}`),
        onError: (e) => toast.error(e instanceof ApiRequestError ? e.apiError.message : (e as Error).message),
      },
    );
  }

  return (
    <>
      {toastContext}
      <Button size="small" icon={<PrinterOutlined />} loading={issue.isPending} onClick={handleClick}>
        {issuable ? label : `Preview`}
      </Button>
    </>
  );
}
