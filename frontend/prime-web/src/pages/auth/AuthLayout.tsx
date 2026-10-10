import type { ReactNode } from 'react';
import { Typography } from 'antd';
import { DevUserPicker } from '../../components/AppShell';
import { PrimeLogo } from '../../components/PrimeLogo';
import { devActAsAvailable } from '../../lib/devActAs';

/** The office and LGU named on the sign-in panel, from the deployment's configuration (CLAUDE.md §85); none by default. */
const lguOffice = import.meta.env.VITE_LGU_OFFICE?.trim();
const lguName = import.meta.env.VITE_LGU_NAME?.trim();

/** A faint tax-map sheet behind the panel: parcels and a road, drawn, not a real map. */
function ParcelPattern() {
  return (
    <svg className="auth-pattern" viewBox="0 0 600 600" preserveAspectRatio="xMidYMid slice" aria-hidden="true" focusable="false">
      <g fill="none" stroke="#fff" strokeWidth="1.5" strokeLinejoin="round">
        <path d="M40 60 220 30 250 190 70 220Z" />
        <path d="M220 30 400 50 410 200 250 190Z" />
        <path d="M400 50 570 80 560 230 410 200Z" />
        <path d="M70 220 250 190 270 360 90 390Z" />
        <path d="M250 190 410 200 430 350 270 360Z" />
        <path d="M410 200 560 230 580 380 430 350Z" />
        <path d="M90 390 270 360 280 540 100 570Z" />
        <path d="M270 360 430 350 450 520 280 540Z" />
        <path d="M430 350 580 380 590 560 450 520Z" />
        <path d="M150 205 165 375" strokeDasharray="6 6" />
        <path d="M330 195 350 355" strokeDasharray="6 6" />
      </g>
      <path d="M0 470 C150 450 300 490 600 460" fill="none" stroke="#fff" strokeWidth="10" opacity="0.5" />
      <circle cx="250" cy="190" r="6" fill="#F2A33A" />
      <circle cx="430" cy="350" r="6" fill="#F2A33A" />
    </svg>
  );
}

/**
 * The sign-in screens' page (docs/analysis/ui-theme.md §4.6): a navy panel with PRIME's mark, its full name and the office
 * on the left; the form on the right. At phone width the panel is a band above the form.
 */
export function AuthLayout({ title, children, width = 420 }: { title: string; children: ReactNode; width?: number }) {
  return (
    <div className="auth-page">
      <aside className="auth-panel">
        <ParcelPattern />
        <div className="auth-panel-content">
          <PrimeLogo subtitle />
          {(lguOffice || lguName) && (
            <div className="auth-office">
              {lguOffice && <div style={{ fontWeight: 500 }}>{lguOffice}</div>}
              {lguName && <div>{lguName}</div>}
            </div>
          )}
          <p className="auth-tagline">Real property appraisal and assessment</p>
        </div>
      </aside>
      <main className="auth-main">
        <div style={{ width: '100%', maxWidth: width }}>
          <Typography.Title level={2} style={{ marginTop: 0, marginBottom: 20 }}>{title}</Typography.Title>
          {children}
          {/* Development only: switch back from a DEMO applicant to another DEMO user. */}
          {devActAsAvailable && <div style={{ marginTop: 24 }}><DevUserPicker /></div>}
        </div>
      </main>
    </div>
  );
}
