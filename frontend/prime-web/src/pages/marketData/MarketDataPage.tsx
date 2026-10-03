import { useState } from 'react';
import { Card, Select, Space, Tabs, Typography } from 'antd';
import { useAllMunicipalities } from '../../api/referenceData';
import { MarketTransactionsTab } from './MarketTransactionsTab';
import { BuildingPermitsTab, DiscoveryLeadsTab, MachineryRegistrationsTab } from './AbstractsTabs';
import { MarketReportsTab } from './MarketReportsTab';

/**
 * Market data for the Schedule of Market Values (LAM 2025 Book I pp.22–25; Book IV pp.104, 113;
 * docs/analysis/smv-preparation-general-revision.md §4.1): sales and other transactions with their review, the
 * abstracts of building permits and machinery registrations, the discovery leads they give, and the printed lists.
 * Prices and parties are personal data; each office sees its own jurisdiction.
 */
export function MarketDataPage() {
  const { data: municipalities = [] } = useAllMunicipalities();
  const [municipalityId, setMunicipalityId] = useState<string>();
  const [tab, setTab] = useState('transactions');

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Typography.Title level={3} style={{ margin: 0 }}>Market Data</Typography.Title>
        <Select allowClear showSearch optionFilterProp="label" placeholder="All cities/municipalities in your jurisdiction" style={{ width: 320 }}
          aria-label="City/municipality" value={municipalityId} onChange={setMunicipalityId}
          options={municipalities.map((m) => ({ value: m.id, label: m.name }))} />
      </Space>
      <Card>
        <Tabs activeKey={tab} onChange={setTab} destroyOnHidden items={[
          { key: 'transactions', label: 'Sales & transactions', children: <MarketTransactionsTab municipalityId={municipalityId} /> },
          { key: 'permits', label: 'Building permits', children: <BuildingPermitsTab municipalityId={municipalityId} /> },
          { key: 'machinery', label: 'Machinery registrations', children: <MachineryRegistrationsTab municipalityId={municipalityId} /> },
          { key: 'leads', label: 'Discovery leads', children: <DiscoveryLeadsTab municipalityId={municipalityId} /> },
          { key: 'reports', label: 'Abstracts & reports', children: <MarketReportsTab municipalityId={municipalityId} /> },
        ]} />
      </Card>
    </Space>
  );
}
