import { useState, useSyncExternalStore, type ReactNode } from 'react';
import { Alert, Badge, Button, Layout, Menu, Select, Tag, Tooltip, Typography } from 'antd';
import { DashboardOutlined, HomeOutlined, TeamOutlined, HeartOutlined, GlobalOutlined, FileTextOutlined, BookOutlined, AuditOutlined, CalculatorOutlined, DollarOutlined, BankOutlined, NumberOutlined, CloudUploadOutlined, ApartmentOutlined, CheckSquareOutlined, SendOutlined, SafetyCertificateOutlined, LineChartOutlined, SyncOutlined, ExperimentOutlined, ScheduleOutlined, FundOutlined, LockOutlined, UserAddOutlined, LogoutOutlined, HistoryOutlined, BarChartOutlined } from '@ant-design/icons';
import { useLocation, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { devActAsAvailable, getDevActAs, setDevActAs, subscribeDevActAs } from '../lib/devActAs';
import { useCan, useCurrentUser, useDevUsers } from '../api/offices';
import { useSignUpRequests } from '../api/accounts';
import { signOut, useSupabaseSession } from '../lib/auth';

const { Header, Sider, Content } = Layout;

const navItems = [
  { needs: 'prime.use', key: '/', icon: <DashboardOutlined />, label: 'Dashboard' },
  { needs: 'prime.use', key: '/approvals', icon: <CheckSquareOutlined />, label: 'Awaiting my approval' },
  { needs: 'records.view', key: '/submissions', icon: <SendOutlined />, label: 'Submissions' },
  { needs: 'property.view', key: '/properties', icon: <HomeOutlined />, label: 'Properties' },
  { needs: 'taxpayer.view', key: '/taxpayers', icon: <TeamOutlined />, label: 'Taxpayers' },
  { needs: 'gis.view', key: '/gis', icon: <GlobalOutlined />, label: 'Tax Map' },
  { needs: 'property.view', key: '/sworn-statements', icon: <AuditOutlined />, label: 'Sworn Statements' },
  { needs: 'property.view', key: '/exemptions', icon: <SafetyCertificateOutlined />, label: 'Exemptions' },
  { needs: 'market.view', key: '/market-data', icon: <LineChartOutlined />, label: 'Market Data' },
  { needs: 'smv.view', key: '/smv-preparation', icon: <ScheduleOutlined />, label: 'SMV Preparation' },
  { needs: 'smv.view', key: '/smv-testing', icon: <ExperimentOutlined />, label: 'SMV Testing' },
  { needs: 'smv.view', key: '/smv-impact', icon: <FundOutlined />, label: 'Tax Impact Study' },
  { needs: 'gr.view', key: '/general-revision', icon: <SyncOutlined />, label: 'General Revision' },
  { needs: 'records.view', key: '/registers', icon: <BookOutlined />, label: 'Registers' },
  { needs: 'records.view', key: '/reports', icon: <BarChartOutlined />, label: 'Reports' },
  { needs: 'treasury.legacy', key: '/collection', icon: <DollarOutlined />, label: 'Collection' },
  { needs: 'office.view', key: '/admin/offices', icon: <ApartmentOutlined />, label: 'Offices' },
  { needs: 'prime.use', key: '/admin/property-identification', icon: <NumberOutlined />, label: 'Property Identification' },
  { needs: 'prime.use', key: '/admin/valuation', icon: <CalculatorOutlined />, label: 'Valuation Rules' },
  { needs: 'prime.use', key: '/admin/forms', icon: <FileTextOutlined />, label: 'Forms & Numbering' },
  { needs: 'treasury.legacy', key: '/admin/collection', icon: <BankOutlined />, label: 'Collection Setup' },
  { needs: 'office.view', key: '/admin/role-permissions', icon: <LockOutlined />, label: 'Role Permissions' },
  { needs: 'users.signup', key: '/admin/sign-up-requests', icon: <UserAddOutlined />, label: <SignUpRequestsLabel /> },
  { needs: 'audit.view', key: '/admin/audit', icon: <HistoryOutlined />, label: 'Audit Trail' },
  { needs: 'config.edit', key: '/admin/content-packs', icon: <CloudUploadOutlined />, label: 'Content Packs' },
  { needs: 'prime.use', key: '/health', icon: <HeartOutlined />, label: 'System Health' },
];

export function AppShell({ children }: { children: ReactNode }) {
  const [collapsed, setCollapsed] = useState(false);
  // Below the md breakpoint the sidebar collapses fully (antd shows a
  // trigger tab) so pages keep the screen width — CLAUDE.md §84.
  const [narrow, setNarrow] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  // Each entry shows only when the user's roles allow its permission (docs/analysis/workflow-security.md §4.1).
  const can = useCan();
  const menuItems = navItems.filter((item) => can(item.needs)).map(({ needs: _needs, ...item }) => item);

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
          items={menuItems}
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
          <SignOutButton narrow={narrow} />
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
export function DevUserPicker() {
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

/** Sign-up requests awaiting a decision, counted in the menu for those who decide them (workflow-security.md §4.2). */
function SignUpRequestsLabel() {
  const can = useCan();
  const waiting = useSignUpRequests('PendingReview', can('users.signup'));
  const count = waiting.data?.length ?? 0;
  return <span>Sign-up Requests {count > 0 && <Badge count={count} size="small" style={{ marginInlineStart: 4 }} />}</span>;
}

/** Shown only for a real Supabase session; the Development bypass has nothing to sign out. */
function SignOutButton({ narrow }: { narrow: boolean }) {
  const session = useSupabaseSession();
  if (!session) return null;
  return (
    <Tooltip title={session.user.email}>
      <Button size="small" icon={<LogoutOutlined />} onClick={() => void signOut()}>{narrow ? null : 'Sign out'}</Button>
    </Tooltip>
  );
}
