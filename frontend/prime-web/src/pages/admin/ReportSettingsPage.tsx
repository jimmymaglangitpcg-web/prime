import { Alert, Space, Tabs, Typography } from 'antd';
import { LevyRatesTab } from './LevyRatesTab';
import { ReportRowMapsTab, SystemParametersTab } from './ReportConfigurationTabs';

/**
 * The configuration the BLGF reports read (docs/analysis/reporting.md §10, Q15–Q18): the levy rates for the QRRPA's
 * collectibles, the QRRPA's rows and dated parameters such as its building threshold. All of it is LGU or LAM content,
 * entered or loaded and approved by a second user; none is built in.
 */
export function ReportSettingsPage() {
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Typography.Title level={3} style={{ margin: 0 }}>Report Settings</Typography.Title>
      <Alert type="warning" showIcon title="Enter only figures from the ordinances and issuances"
        description="PRIME has no built-in rates, rows or thresholds. Settings are never edited: a change is a new version that takes over from its effective date. Another user approves what you create." />
      <Tabs items={[
        { key: 'levies', label: 'Levy rates', children: <LevyRatesTab /> },
        { key: 'qrrpa-rows', label: 'QRRPA rows', children: <ReportRowMapsTab /> },
        { key: 'parameters', label: 'Parameters', children: <SystemParametersTab /> },
      ]} />
    </Space>
  );
}
