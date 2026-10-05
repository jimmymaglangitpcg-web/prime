import { useState, useSyncExternalStore, type ReactNode } from 'react';
import { Alert, Layout, Menu, Select, Tag, Tooltip, Typography } from 'antd';
import { DashboardOutlined, HomeOutlined, TeamOutlined, HeartOutlined, GlobalOutlined, FileTextOutlined, BookOutlined, AuditOutlined, CalculatorOutlined, DollarOutlined, BankOutlined, NumberOutlined, CloudUploadOutlined, ApartmentOutlined, CheckSquareOutlined, SendOutlined, SafetyCertificateOutlined, LineChartOutlined, SyncOutlined, ExperimentOutlined, ScheduleOutlined, FundOutlined } from '@ant-design/icons';
import { useLocation, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { devActAsAvailable, getDevActAs, setDevActAs, subscribeDevActAs } from '../lib/devActAs';
import { useCurrentUser, useDevUsers } from '../api/offices';

const { Header, Sider, Content } = Layout;

const navItems = [
  { key: '/', icon: <DashboardOutlined />, label: 'Dashboard' },
  { key: '/approvals', icon: <CheckSquareOutlined />, label: 'Awaiting my approval' },
  { key: '/submissions', icon: <SendOutlined />, label: 'Submissions' },
  { key: '/properties', icon: <HomeOutlined />, label: 'Properties' },
  { key: '/taxpayers', icon: <TeamOutlined />, label: 'Taxpayers' },
  { key: '/gis', icon: <GlobalOutlined />, label: 'Tax Map' },
  { key: '/sworn-statements', icon: <AuditOutlined />, label: 'Sworn Statements' },
  { key: '/exemptions', icon: <SafetyCertificateOutlined />, label: 'Exemptions' },
  { key: '/market-data', icon: <LineChartOutlined />, label: 'Market Data' },
  { key: '/smv-preparation', icon: <ScheduleOutlined />, label: 'SMV Preparation' },
  { key: '/smv-testing', icon: <ExperimentOutlined />, label: 'SMV Testing' },
  { key: '/smv-impact', icon: <FundOutlined />, label: 'Tax Impact Study' },
  { key: '/general-revision', icon: <SyncOutlined />, label: 'General Revision' },
  { key: '/registers', icon: <BookOutlined />, label: 'Registers' },
  { key: '/collection', icon: <DollarOutlined />, label: 'Collection' },
  { key: '/admin/offices', icon: <ApartmentOutlined />, label: 'Offices' },
  { key: '/admin/property-identification', icon: <NumberOutlined />, label: 'Property Identification' },
  { key: '/admin/valuation', icon: <CalculatorOutlined />, label: 'Valuation Rules' },
  { key: '/admin/forms', icon: <FileTextOutlined />, label: 'Forms & Numbering' },
  { key: '/admin/collection', icon: <BankOutlined />, label: 'Collection Setup' },
  { key: '/admin/content-packs', icon: <CloudUploadOutlined />, label: 'Content Packs' },
  { key: '/health', icon: <HeartOutlined />, label: 'System Health' },
];

export function AppShell({ children }: { children: ReactNode }) {
  const [collapsed, setCollapsed] = useState(false);
  // Below the md breakpoint the sidebar collapses fully (antd shows a
  // trigger tab) so pages keep the screen width — CLAUDE.md §84.
  const [narrow, setNarrow] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();

  // Highlight the nav item whose path is the longest prefix match of the
  // current location (so /properties/123 still highlights "Properties").
  const selectedKey =
    navItems
      .map((item) => item.key)
      .filter((key) => key === '/' ? location.pathname === '/' : location.pathname.startsWith(key))
      .sort((a, b) => b.length - a.length)[0] ?? '';

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider
        collapsible
        collapsed={collapsed}
        onCollapse={setCollapsed}
        breakpoint="md"
        collapsedWidth={narrow ? 0 : 80}
        // Sits in the header's left gutter instead of over page content.
        zeroWidthTriggerStyle={{ top: 12 }}
        onBreakpoint={(broken) => {
          setNarrow(broken);
          setCollapsed(broken);
        }}
      >
        <div style={{ height: 48, margin: 12, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <Typography.Text strong style={{ color: '#fff', fontSize: collapsed ? 16 : 20 }}>
            {collapsed ? 'P' : 'PRIME'}
          </Typography.Text>
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[selectedKey]}
          items={navItems}
          onClick={({ key }) => navigate(key)}
        />
      </Sider>
      <Layout>
        <Header style={{ background: '#fff', padding: narrow ? '0 16px 0 56px' : '0 24px', display: 'flex', alignItems: 'center', gap: 12, borderBottom: '1px solid #f0f0f0', minWidth: 0 }}>
          <Typography.Title level={4} ellipsis={{ tooltip: true }} style={{ margin: 0, minWidth: 0, flex: '1 1 auto' }}>
            {narrow ? 'PRIME' : 'Property Registry, Information, Mapping & Evaluation System'}
          </Typography.Title>
          {!narrow && <CurrentOffice />}
          {devActAsAvailable && <DevUserPicker />}
        </Header>
        <Content style={{ margin: narrow ? 16 : 24, minWidth: 0 }}>
          {devActAsAvailable && <DevActAsBanner />}
          {children}
        </Content>
      </Layout>
    </Layout>
  );
}

/** The signed-in user's office (docs/analysis/province-wide-operation.md §3.2). */
function CurrentOffice() {
  const me = useCurrentUser();
  if (!me.data) return null;
  const { displayName, officeName, provinceWide, assigned } = me.data;
  const label = !assigned ? 'No office' : officeName ?? (provinceWide ? 'Province-wide' : '');
  return (
    <Tooltip title={`${displayName ?? ''}${me.data.roles.length ? ` · ${me.data.roles.join(', ')}` : ''}`}>
      <Tag color={assigned ? 'blue' : 'orange'} style={{ marginInlineEnd: 0, whiteSpace: 'nowrap', maxWidth: 260, overflow: 'hidden', textOverflow: 'ellipsis' }}>
        {label}
      </Tag>
    </Tooltip>
  );
}

const useDevActAs = () => useSyncExternalStore(subscribeDevActAs, getDevActAs);

/** Development only: act as one of the API's DEMO users, e.g. a municipal appraiser or the approving checker (Q13). */
function DevUserPicker() {
  const actingAs = useDevActAs();
  const users = useDevUsers(true);
  const queryClient = useQueryClient();
  return (
    <Tooltip title="Development only: act as another DEMO user to try offices and approvals.">
      <Select size="small" style={{ width: 190 }} aria-label="Act as DEMO user" value={actingAs ?? ''}
        onChange={(v: string) => { setDevActAs(v || null); queryClient.invalidateQueries(); }}
        options={[
          { value: '', label: 'Dev: usual user' },
          ...(users.data ?? []).filter((u) => u.key !== 'admin').map((u) => ({ value: u.key, label: `Dev: ${u.displayName}` })),
        ]} />
    </Tooltip>
  );
}

function DevActAsBanner() {
  const actingAs = useDevActAs();
  const users = useDevUsers(!!actingAs);
  const name = users.data?.find((u) => u.key === actingAs)?.displayName ?? actingAs;
  return actingAs
    ? <Alert type="warning" showIcon banner style={{ marginBottom: 16 }} title={`Acting as ${name} — development only; requests are made as that DEMO user.`} />
    : null;
}
