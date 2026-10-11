import { useState, useSyncExternalStore, type ReactNode } from 'react';
import { Alert, Badge, Button, Input, Layout, Menu, Select, Tag, Tooltip, type MenuProps } from 'antd';
import { DashboardOutlined, HomeOutlined, GlobalOutlined, BookOutlined, CheckSquareOutlined, LineChartOutlined, SettingOutlined, LogoutOutlined, UserOutlined } from '@ant-design/icons';
import { useLocation, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { devActAsAvailable, getDevActAs, setDevActAs, subscribeDevActAs } from '../lib/devActAs';
import { useCan, useCurrentUser, useDevUsers } from '../api/offices';
import { useSignUpRequests } from '../api/accounts';
import { signOut, useSupabaseSession } from '../lib/auth';
import { useApprovalQueue } from '../api/approvals';
import { brand } from '../theme';
import { PrimeLogo } from './PrimeLogo';

const { Header, Sider, Content } = Layout;

type NavLeaf = { needs: string; key: string; label: ReactNode; icon?: ReactNode };
type NavSection = { section: string; children: NavLeaf[] };
type NavEntry = NavLeaf | { key: string; label: string; icon: ReactNode; children: (NavLeaf | NavSection)[] };

/**
 * The sidebar: 26 screens in 7 entries (docs/analysis/ui-theme.md §4.2). Each screen shows only when the user's roles allow
 * its permission (workflow-security.md §4.1); a group left with one screen shows as that screen, and one with none is
 * hidden. The frozen treasury screens (/collection, /admin/collection) are not in the menu; their pages stay reachable by
 * URL (CLAUDE.md §0, ui-theme.md Q7).
 */
const navEntries: NavEntry[] = [
  { needs: 'prime.use', key: '/', icon: <DashboardOutlined />, label: 'Dashboard' },
  { needs: 'prime.use', key: '/approvals', icon: <CheckSquareOutlined />, label: <ApprovalsLabel /> },
  {
    key: 'registry', icon: <HomeOutlined />, label: 'Registry', children: [
      { needs: 'property.view', key: '/properties', label: 'Properties' },
      { needs: 'taxpayer.view', key: '/taxpayers', label: 'Owners & taxpayers' },
      { needs: 'property.view', key: '/sworn-statements', label: 'Sworn statements' },
      { needs: 'property.view', key: '/exemptions', label: 'Exemptions' },
    ],
  },
  { needs: 'gis.view', key: '/gis', icon: <GlobalOutlined />, label: 'Tax map' },
  {
    key: 'valuation', icon: <LineChartOutlined />, label: 'Valuation', children: [
      { needs: 'market.view', key: '/market-data', label: 'Market data' },
      { needs: 'smv.view', key: '/smv-preparation', label: 'SMV preparation' },
      { needs: 'smv.view', key: '/smv-testing', label: 'SMV testing' },
      { needs: 'smv.view', key: '/smv-impact', label: 'Tax impact study' },
      { needs: 'gr.view', key: '/general-revision', label: 'General revision' },
    ],
  },
  {
    key: 'records', icon: <BookOutlined />, label: 'Records', children: [
      { needs: 'records.view', key: '/registers', label: 'Registers' },
      { needs: 'records.view', key: '/submissions', label: 'Submissions' },
      { needs: 'records.view', key: '/reports', label: 'Reports' },
    ],
  },
  {
    key: 'admin', icon: <SettingOutlined />, label: 'Administration', children: [
      { section: 'People', children: [
        { needs: 'office.view', key: '/admin/offices', label: 'Offices' },
        { needs: 'office.view', key: '/admin/role-permissions', label: 'Role permissions' },
        { needs: 'users.signup', key: '/admin/sign-up-requests', label: <SignUpRequestsLabel /> },
      ] },
      { section: 'Configuration', children: [
        { needs: 'prime.use', key: '/admin/property-identification', label: 'Property identification' },
        { needs: 'prime.use', key: '/admin/valuation', label: 'Valuation rules' },
        { needs: 'prime.use', key: '/admin/forms', label: 'Forms & numbering' },
        { needs: 'config.edit', key: '/admin/content-packs', label: 'Content packs' },
      ] },
      { section: 'Oversight', children: [
        { needs: 'audit.view', key: '/admin/audit', label: 'Audit trail' },
        { needs: 'prime.use', key: '/health', label: 'System health' },
      ] },
    ],
  },
];

const isSection = (x: NavLeaf | NavSection): x is NavSection => 'section' in x;
const leavesOf = (e: NavEntry): NavLeaf[] =>
  'children' in e ? e.children.flatMap((c) => (isSection(c) ? c.children : [c])) : [e];
const allLeaves = navEntries.flatMap(leavesOf);
const groupOf = (key: string) => navEntries.find((e) => 'children' in e && leavesOf(e).some((l) => l.key === key))?.key;

type MenuItem = NonNullable<MenuProps['items']>[number];

/** The menu items the user's permissions allow. */
function menuItemsFor(can: (permission: string) => boolean): MenuItem[] {
  return navEntries.flatMap((e): MenuItem[] => {
    if (!('children' in e)) return can(e.needs) ? [{ key: e.key, icon: e.icon, label: e.label }] : [];
    const leaves = leavesOf(e).filter((l) => can(l.needs));
    if (leaves.length === 0) return [];
    if (leaves.length === 1) return [{ key: leaves[0].key, icon: e.icon, label: leaves[0].label }];
    const children = e.children.flatMap((c): MenuItem[] => {
      if (!isSection(c)) return can(c.needs) ? [{ key: c.key, label: c.label }] : [];
      const allowed = c.children.filter((l) => can(l.needs));
      return allowed.length
        ? [{ type: 'group', key: `${e.key}:${c.section}`, label: c.section, children: allowed.map((l) => ({ key: l.key, label: l.label })) }]
        : [];
    });
    return [{ key: e.key, icon: e.icon, label: e.label, children }];
  });
}

export function AppShell({ children }: { children: ReactNode }) {
  const [collapsed, setCollapsed] = useState(false);
  // Below the md breakpoint the sidebar collapses fully (antd shows a
  // trigger tab) so pages keep the screen width — CLAUDE.md §84.
  const [narrow, setNarrow] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const can = useCan();
  const menuItems = menuItemsFor(can);

  // Highlight the screen whose path is the longest prefix match of the
  // current location (so /properties/123 still highlights "Properties").
  const selectedKey =
    allLeaves
      .map((item) => item.key)
      .filter((key) => key === '/' ? location.pathname === '/' : location.pathname.startsWith(key))
      .sort((a, b) => b.length - a.length)[0] ?? '';

  // The current screen's group opens when the user arrives on it; the user may open or close others.
  const currentGroup = groupOf(selectedKey);
  const [openKeys, setOpenKeys] = useState<string[]>(currentGroup ? [currentGroup] : []);
  const [shownGroup, setShownGroup] = useState(currentGroup);
  if (shownGroup !== currentGroup) {
    setShownGroup(currentGroup);
    if (currentGroup && !openKeys.includes(currentGroup)) setOpenKeys([...openKeys, currentGroup]);
  }

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider
        collapsible
        collapsed={collapsed}
        onCollapse={setCollapsed}
        breakpoint="md"
        collapsedWidth={narrow ? 0 : 80}
        width={232}
        // Stays in view while the page scrolls, so the user block at its foot is always reachable.
        style={{ position: 'sticky', top: 0, height: '100vh', alignSelf: 'flex-start', zIndex: 10 }}
        // Sits in the header's left gutter instead of over page content.
        zeroWidthTriggerStyle={{ top: 12 }}
        onBreakpoint={(broken) => {
          setNarrow(broken);
          setCollapsed(broken);
        }}
      >
        <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
          <div style={{ height: 64, padding: collapsed ? 0 : '0 20px', display: 'flex', alignItems: 'center', justifyContent: collapsed ? 'center' : 'flex-start', flex: '0 0 auto' }}>
            <PrimeLogo collapsed={collapsed} />
          </div>
          <nav aria-label="Main" style={{ flex: '1 1 auto', overflowY: 'auto', overflowX: 'hidden' }}>
            <Menu
              theme="dark"
              mode="inline"
              selectedKeys={[selectedKey]}
              {...(collapsed ? {} : { openKeys, onOpenChange: setOpenKeys })}
              items={menuItems}
              onClick={({ key }) => navigate(key)}
            />
          </nav>
          <SidebarUser collapsed={collapsed} />
        </div>
      </Sider>
      <Layout style={{ minWidth: 0 }}>
        <Header style={{ background: '#fff', padding: narrow ? '0 16px 0 56px' : '0 24px', display: 'flex', alignItems: 'center', gap: 12, borderBottom: '1px solid #e3e8ee', minWidth: 0 }}>
          <div role="search" style={{ flex: '1 1 auto', minWidth: 0 }}>
            {can('property.view') && <HeaderSearch />}
          </div>
          {!narrow && <CurrentOffice />}
          {devActAsAvailable && <DevUserPicker />}
          {narrow && <SignOutButton narrow />}
        </Header>
        <Content style={{ margin: narrow ? 16 : 24, minWidth: 0 }}>
          {devActAsAvailable && <DevActAsBanner />}
          {children}
        </Content>
      </Layout>
    </Layout>
  );
}

/**
 * The header's search (ui-theme.md §4.3, Q4): opens the property search with the typed text, which matches PIN, lot, title,
 * survey and tax-map numbers (CLAUDE.md §56). TD, owner and TIN search come in a later step.
 */
function HeaderSearch() {
  const navigate = useNavigate();
  const [text, setText] = useState('');
  return (
    <Input.Search
      value={text}
      onChange={(e) => setText(e.target.value)}
      allowClear
      aria-label="Search properties by PIN, lot, title, survey or tax map number"
      placeholder="Search properties: PIN, lot, title, survey or tax map no."
      style={{ maxWidth: 520 }}
      onSearch={(value) => {
        const q = value.trim();
        if (!q) return;
        navigate(`/properties?q=${encodeURIComponent(q)}`);
        setText('');
      }}
    />
  );
}

/** Records waiting for the user's signature, counted beside "Approvals" (province-wide-operation.md §3.4). */
function ApprovalsLabel() {
  const queue = useApprovalQueue();
  const count = queue.data?.length ?? 0;
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
      Approvals
      {count > 0 && <Badge count={count} size="small" color={brand.amber} style={{ color: brand.navy, boxShadow: 'none' }} title={`${count} awaiting your approval`} />}
    </span>
  );
}

/** The signed-in user at the sidebar's foot: name, office and roles, and sign out (ui-theme.md §4.3). */
function SidebarUser({ collapsed }: { collapsed: boolean }) {
  const me = useCurrentUser();
  if (!me.data) return null;
  const { displayName, officeName, provinceWide, assigned, roles } = me.data;
  const office = !assigned ? 'No office' : officeName ?? (provinceWide ? 'Province-wide' : '');
  const details = `${displayName ?? ''} · ${office}${roles.length ? ` · ${roles.join(', ')}` : ''}`;
  return (
    <div style={{ flex: '0 0 auto', borderTop: '1px solid rgba(255,255,255,0.12)', padding: collapsed ? '12px 0' : '12px 16px', marginBottom: 48,
      display: 'flex', alignItems: 'center', gap: 10, justifyContent: collapsed ? 'center' : 'flex-start', color: brand.sidebarText }}>
      <Tooltip title={details} placement="right">
        <span role="img" aria-label={details} tabIndex={0} style={{ display: 'inline-flex' }}>
          <UserOutlined style={{ fontSize: 18, color: '#fff' }} />
        </span>
      </Tooltip>
      {!collapsed && (
        <div style={{ minWidth: 0, flex: '1 1 auto', lineHeight: 1.3 }}>
          <div style={{ color: '#fff', fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{displayName ?? 'Signed in'}</div>
          <div style={{ fontSize: 12, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{roles.join(', ') || office}</div>
        </div>
      )}
      {!collapsed && <SignOutButton narrow onDark />}
    </div>
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
    ? <Alert className="no-print" type="warning" showIcon banner style={{ marginBottom: 16 }} title={`Acting as ${name} — development only; requests are made as that DEMO user.`} />
    : null;
}

/** Sign-up requests awaiting a decision, counted in the menu for those who decide them (workflow-security.md §4.2). */
function SignUpRequestsLabel() {
  const can = useCan();
  const waiting = useSignUpRequests('PendingReview', can('users.signup'));
  const count = waiting.data?.length ?? 0;
  return <span>Sign-up requests {count > 0 && <Badge count={count} size="small" style={{ marginInlineStart: 4 }} />}</span>;
}

/** Shown only for a real Supabase session; the Development bypass has nothing to sign out. */
function SignOutButton({ narrow, onDark = false }: { narrow: boolean; onDark?: boolean }) {
  const session = useSupabaseSession();
  if (!session) return null;
  return (
    <Tooltip title={`Sign out ${session.user.email ?? ''}`}>
      <Button size="small" type={onDark ? 'text' : 'default'} aria-label="Sign out" icon={<LogoutOutlined style={onDark ? { color: '#fff' } : undefined} />}
        onClick={() => void signOut()}>{narrow ? null : 'Sign out'}</Button>
    </Tooltip>
  );
}
