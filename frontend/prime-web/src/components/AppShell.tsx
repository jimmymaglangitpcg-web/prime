import { useState, useSyncExternalStore, type ReactNode } from 'react';
import { Alert, Layout, Menu, Switch, Tooltip, Typography } from 'antd';
import { DashboardOutlined, HomeOutlined, TeamOutlined, HeartOutlined, GlobalOutlined, FileTextOutlined, BookOutlined, AuditOutlined, CalculatorOutlined, DollarOutlined, BankOutlined } from '@ant-design/icons';
import { useLocation, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { devActAsAvailable, isActingAsChecker, setActingAsChecker, subscribeActingAsChecker } from '../lib/devActAs';

const { Header, Sider, Content } = Layout;

const navItems = [
  { key: '/', icon: <DashboardOutlined />, label: 'Dashboard' },
  { key: '/properties', icon: <HomeOutlined />, label: 'Properties' },
  { key: '/taxpayers', icon: <TeamOutlined />, label: 'Taxpayers' },
  { key: '/gis', icon: <GlobalOutlined />, label: 'Tax Map' },
  { key: '/sworn-statements', icon: <AuditOutlined />, label: 'Sworn Statements' },
  { key: '/registers', icon: <BookOutlined />, label: 'Registers' },
  { key: '/collection', icon: <DollarOutlined />, label: 'Collection' },
  { key: '/admin/valuation', icon: <CalculatorOutlined />, label: 'Valuation Rules' },
  { key: '/admin/forms', icon: <FileTextOutlined />, label: 'Forms & Numbering' },
  { key: '/admin/collection', icon: <BankOutlined />, label: 'Collection Setup' },
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
        <Header style={{ background: '#fff', padding: narrow ? '0 16px 0 56px' : '0 24px', display: 'flex', alignItems: 'center', borderBottom: '1px solid #f0f0f0', minWidth: 0 }}>
          <Typography.Title level={4} ellipsis={{ tooltip: true }} style={{ margin: 0, minWidth: 0 }}>
            {narrow ? 'PRIME' : 'Property Registry, Information, Mapping & Evaluation System'}
          </Typography.Title>
          {devActAsAvailable && <DevCheckerSwitch />}
        </Header>
        <Content style={{ margin: narrow ? 16 : 24, minWidth: 0 }}>
          {devActAsAvailable && <DevCheckerBanner />}
          {children}
        </Content>
      </Layout>
    </Layout>
  );
}

const useActingAsChecker = () => useSyncExternalStore(subscribeActingAsChecker, isActingAsChecker);

/** Development only: switch to the second development user to try approvals (docs/analysis/value-and-assess.md §4). */
function DevCheckerSwitch() {
  const acting = useActingAsChecker();
  const queryClient = useQueryClient();
  return (
    <Tooltip title="Development only: act as the second development user, to approve what the usual user created.">
      <span style={{ marginLeft: 'auto', whiteSpace: 'nowrap', fontSize: 12 }}>
        <Switch size="small" checked={acting} aria-label="Act as checker"
          onChange={(v) => { setActingAsChecker(v); queryClient.invalidateQueries(); }} /> Act as checker (dev)
      </span>
    </Tooltip>
  );
}

function DevCheckerBanner() {
  return useActingAsChecker()
    ? <Alert type="warning" showIcon banner style={{ marginBottom: 16 }} title="Acting as DEV CHECKER — development only; requests are made as the second development user." />
    : null;
}

