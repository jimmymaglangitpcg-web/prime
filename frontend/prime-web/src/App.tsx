import { Route, Routes } from 'react-router-dom';
import { Typography } from 'antd';
import { AppShell } from './components/AppShell';
import { HealthPage } from './pages/HealthPage';
import { PropertySearchPage } from './pages/properties/PropertySearchPage';
import { PropertyRegisterPage } from './pages/properties/PropertyRegisterPage';
import { PropertyProfilePage } from './pages/properties/PropertyProfilePage';
import { TaxpayerSearchPage } from './pages/taxpayers/TaxpayerSearchPage';
import { TaxpayerRegisterPage } from './pages/taxpayers/TaxpayerRegisterPage';

function DashboardPlaceholder() {
  return (
    <div>
      <Typography.Title level={3}>Dashboard</Typography.Title>
      <Typography.Paragraph type="secondary">
        The real dashboard (CLAUDE.md §55 — totals, collection, delinquency,
        pending approvals) needs Assessment, Billing, and Collection data
        that don't exist until Phases 5–9. Use the Properties or Taxpayers
        sections in the meantime.
      </Typography.Paragraph>
    </div>
  );
}

export default function App() {
  return (
    <AppShell>
      <Routes>
        <Route path="/" element={<DashboardPlaceholder />} />
        <Route path="/health" element={<HealthPage />} />
        <Route path="/properties" element={<PropertySearchPage />} />
        <Route path="/properties/new" element={<PropertyRegisterPage />} />
        <Route path="/properties/:id" element={<PropertyProfilePage />} />
        <Route path="/taxpayers" element={<TaxpayerSearchPage />} />
        <Route path="/taxpayers/new" element={<TaxpayerRegisterPage />} />
      </Routes>
    </AppShell>
  );
}
