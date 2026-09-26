import { Route, Routes } from 'react-router-dom';
import { Typography } from 'antd';
import { AppShell } from './components/AppShell';
import { HealthPage } from './pages/HealthPage';
import { PropertySearchPage } from './pages/properties/PropertySearchPage';
import { PropertyRegisterPage } from './pages/properties/PropertyRegisterPage';
import { PropertyProfilePage } from './pages/properties/PropertyProfilePage';
import { TaxpayerSearchPage } from './pages/taxpayers/TaxpayerSearchPage';
import { TaxpayerRegisterPage } from './pages/taxpayers/TaxpayerRegisterPage';
import { GisWorkspacePage } from './pages/gis/GisWorkspacePage';
import { GisPrintPage } from './pages/gis/GisPrintPage';
import { StatementOfAccountPage } from './pages/billing/StatementOfAccountPage';
import { FormDocumentPage } from './pages/documents/FormDocumentPage';
import { SwornStatementPage } from './pages/swornStatements/SwornStatementPage';
import { SwornStatementsPage } from './pages/swornStatements/SwornStatementsPage';
import { RegistersPage } from './pages/registers/RegistersPage';
import { FormsAdminPage } from './pages/admin/FormsAdminPage';

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
        <Route path="/properties/:id/statement-of-account" element={<StatementOfAccountPage />} />
        <Route path="/taxpayers" element={<TaxpayerSearchPage />} />
        <Route path="/taxpayers/new" element={<TaxpayerRegisterPage />} />
        <Route path="/gis" element={<GisWorkspacePage />} />
        <Route path="/gis/print" element={<GisPrintPage />} />
        <Route path="/documents/preview" element={<FormDocumentPage />} />
        <Route path="/documents/:id" element={<FormDocumentPage />} />
        <Route path="/registers" element={<RegistersPage />} />
        <Route path="/sworn-statements" element={<SwornStatementsPage />} />
        <Route path="/sworn-statements/:id" element={<SwornStatementPage />} />
        <Route path="/admin/forms" element={<FormsAdminPage />} />
      </Routes>
    </AppShell>
  );
}
