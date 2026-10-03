import { Alert, Card, Space, Typography } from 'antd';
import { useOpenExemptions } from '../../api/exemptions';
import { ExemptionClaimsTable } from '../properties/sections/ExemptionsSection';

/**
 * The exemption worklist (docs/analysis/assessment-listing-exemptions.md Q5): open claims, overdue proof first. An
 * overdue claim is not rejected by PRIME: the unit simply stays listed as taxable until proof is filed (LGC §206).
 */
export function ExemptionClaimsPage() {
  const { data = [], isLoading } = useOpenExemptions();
  const overdue = data.filter((c) => c.proofOverdue).length;
  const awaiting = data.filter((c) => c.status === 'ProofFiled').length;
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Title level={3} style={{ margin: 0 }}>Exemption claims</Typography.Title>
      {overdue > 0 && (
        <Alert type="warning" showIcon title={`${overdue} claim${overdue === 1 ? '' : 's'} without proof past the due date`}
          description="These units stay listed as taxable in the Assessment Roll until their proof is filed and the exemption approved." />
      )}
      <Card title={`Open claims (${data.length}; ${awaiting} awaiting a decision)`}>
        <ExemptionClaimsTable claims={data} loading={isLoading} showProperty />
      </Card>
    </Space>
  );
}
