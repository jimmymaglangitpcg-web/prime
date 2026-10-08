import type { ReactNode } from 'react';
import { Card, Typography } from 'antd';
import { DevUserPicker } from '../../components/AppShell';
import { devActAsAvailable } from '../../lib/devActAs';

/** The centred card the sign-in screens share, outside the application shell. */
export function AuthLayout({ title, children, width = 420 }: { title: string; children: ReactNode; width?: number }) {
  return (
    <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 16, background: '#f5f7fb' }}>
      <div style={{ width: '100%', maxWidth: width }}>
        <div style={{ textAlign: 'center', marginBottom: 16 }}>
          <Typography.Title level={2} style={{ margin: 0, color: '#1d4ed8' }}>PRIME</Typography.Title>
          <Typography.Text type="secondary">Property Registry, Information, Mapping &amp; Evaluation System</Typography.Text>
        </div>
        <Card title={title}>{children}</Card>
        {/* Development only: switch back from a DEMO applicant to another DEMO user. */}
        {devActAsAvailable && <div style={{ marginTop: 12, textAlign: 'center' }}><DevUserPicker /></div>}
      </div>
    </div>
  );
}
