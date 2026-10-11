import { Route, Routes } from 'react-router-dom';
import { AppShell } from './components/AppShell';
import { AuthGate } from './components/AuthGate';
import { SignUpRequestsPage } from './pages/admin/SignUpRequestsPage';
import { HealthPage } from './pages/HealthPage';
import { DashboardPage } from './pages/dashboard/DashboardPage';
import { PropertySearchPage } from './pages/properties/PropertySearchPage';
import { PropertyRegisterPage } from './pages/properties/PropertyRegisterPage';
import { PropertyProfilePage } from './pages/properties/PropertyProfilePage';
import { TaxpayerSearchPage } from './pages/taxpayers/TaxpayerSearchPage';
import { TaxpayerRegisterPage } from './pages/taxpayers/TaxpayerRegisterPage';
import { GisWorkspacePage } from './pages/gis/GisWorkspacePage';
import { GisPrintPage } from './pages/gis/GisPrintPage';
import { TaxMapSheetPage } from './pages/gis/TaxMapSheetPage';
import { StatementOfAccountPage } from './pages/billing/StatementOfAccountPage';
import { FormDocumentPage } from './pages/documents/FormDocumentPage';
import { SwornStatementPage } from './pages/swornStatements/SwornStatementPage';
import { SwornStatementsPage } from './pages/swornStatements/SwornStatementsPage';
import { RegistersPage } from './pages/registers/RegistersPage';
import { ReportPrintPage } from './pages/reports/ReportPrintPage';
import { ReportsPage } from './pages/reports/ReportsPage';
import { ExemptionClaimsPage } from './pages/exemptions/ExemptionClaimsPage';
import { MarketDataPage } from './pages/marketData/MarketDataPage';
import { GeneralRevisionsPage } from './pages/generalRevision/GeneralRevisionsPage';
import { GeneralRevisionPage } from './pages/generalRevision/GeneralRevisionPage';
import { SmvTestingPage } from './pages/smvTesting/SmvTestingPage';
import { ImpactStudiesPage } from './pages/smvTesting/ImpactStudiesPage';
import { ImpactStudyPage } from './pages/smvTesting/ImpactStudyPage';
import { SmvPreparationsPage } from './pages/smvPreparation/SmvPreparationsPage';
import { SmvPreparationPage } from './pages/smvPreparation/SmvPreparationPage';
import { SalesAnalysisPage } from './pages/smvPreparation/SalesAnalysisPage';
import { ValuationRulesPage } from './pages/admin/ValuationRulesPage';
import { FormsAdminPage } from './pages/admin/FormsAdminPage';
import { CollectionSetupPage } from './pages/admin/CollectionSetupPage';
import { PropertyIdentificationPage } from './pages/admin/PropertyIdentificationPage';
import { ContentPacksPage } from './pages/admin/ContentPacksPage';
import { OfficesPage } from './pages/admin/OfficesPage';
import { RolePermissionsPage } from './pages/admin/RolePermissionsPage';
import { AuditTrailPage } from './pages/admin/AuditTrailPage';
import { ApprovalsPage } from './pages/approvals/ApprovalsPage';
import { SubmissionsPage } from './pages/submissions/SubmissionsPage';
import { CollectionPage } from './pages/collection/CollectionPage';
import { PaymentWorkspacePage } from './pages/collection/PaymentWorkspacePage';

export default function App() {
  return (
    <AuthGate>
    <AppShell>
      <Routes>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/health" element={<HealthPage />} />
        <Route path="/properties" element={<PropertySearchPage />} />
        <Route path="/properties/new" element={<PropertyRegisterPage />} />
        <Route path="/properties/:id" element={<PropertyProfilePage />} />
        <Route path="/properties/:id/statement-of-account" element={<StatementOfAccountPage />} />
        <Route path="/taxpayers" element={<TaxpayerSearchPage />} />
        <Route path="/taxpayers/new" element={<TaxpayerRegisterPage />} />
        <Route path="/gis" element={<GisWorkspacePage />} />
        <Route path="/gis/print" element={<GisPrintPage />} />
        <Route path="/gis/sheet" element={<TaxMapSheetPage />} />
        <Route path="/documents/preview" element={<FormDocumentPage />} />
        <Route path="/documents/:id" element={<FormDocumentPage />} />
        <Route path="/registers" element={<RegistersPage />} />
        <Route path="/reports" element={<ReportsPage />} />
        <Route path="/reports/print" element={<ReportPrintPage />} />
        <Route path="/exemptions" element={<ExemptionClaimsPage />} />
        <Route path="/market-data" element={<MarketDataPage />} />
        <Route path="/general-revision" element={<GeneralRevisionsPage />} />
        <Route path="/general-revision/:id" element={<GeneralRevisionPage />} />
        <Route path="/smv-testing" element={<SmvTestingPage />} />
        <Route path="/smv-impact" element={<ImpactStudiesPage />} />
        <Route path="/smv-impact/:id" element={<ImpactStudyPage />} />
        <Route path="/smv-preparation" element={<SmvPreparationsPage />} />
        <Route path="/smv-preparation/:id" element={<SmvPreparationPage />} />
        <Route path="/smv-preparation/analyses/:id" element={<SalesAnalysisPage />} />
        <Route path="/sworn-statements" element={<SwornStatementsPage />} />
        <Route path="/sworn-statements/:id" element={<SwornStatementPage />} />
        <Route path="/admin/forms" element={<FormsAdminPage />} />
        <Route path="/admin/valuation" element={<ValuationRulesPage />} />
        <Route path="/admin/collection" element={<CollectionSetupPage />} />
        <Route path="/admin/property-identification" element={<PropertyIdentificationPage />} />
        <Route path="/admin/content-packs" element={<ContentPacksPage />} />
        <Route path="/admin/offices" element={<OfficesPage />} />
        <Route path="/admin/role-permissions" element={<RolePermissionsPage />} />
        <Route path="/admin/audit" element={<AuditTrailPage />} />
        <Route path="/admin/sign-up-requests" element={<SignUpRequestsPage />} />
        <Route path="/approvals" element={<ApprovalsPage />} />
        <Route path="/submissions" element={<SubmissionsPage />} />
        <Route path="/collection" element={<CollectionPage />} />
        <Route path="/collection/pay" element={<PaymentWorkspacePage />} />
      </Routes>
    </AppShell>
    </AuthGate>
  );
}
