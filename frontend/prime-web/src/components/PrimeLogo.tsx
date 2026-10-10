import { brand } from '../theme';

/**
 * PRIME's product mark (docs/analysis/ui-theme.md §4.1): a tax-map parcel with a dashed subdivision line and an amber
 * survey-monument dot on a navy tile. Not an LGU seal (CLAUDE.md §85); an office's own logo goes on its documents.
 * The same drawing is `public/favicon.svg`.
 */
export function PrimeMark({ size = 32 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" focusable="false" style={{ flex: '0 0 auto', display: 'block' }}>
      <rect width="32" height="32" rx="7" fill={brand.navy} stroke="rgba(255,255,255,0.18)" />
      <path d="M8 10.5 22 7.5 25 22 10 25Z" fill="rgba(255,255,255,0.08)" stroke="#fff" strokeWidth="1.8" strokeLinejoin="round" />
      <path d="M15 9 17.5 23.5" stroke="#fff" strokeWidth="1.4" strokeDasharray="2 2" opacity="0.85" />
      <circle cx="22" cy="7.5" r="2.7" fill={brand.amber} stroke={brand.navy} strokeWidth="1.2" />
    </svg>
  );
}

/** The mark with the word PRIME; `subtitle` adds the full name below it (sign-in screens). */
export function PrimeLogo({ collapsed = false, subtitle = false, color = '#fff' }: { collapsed?: boolean; subtitle?: boolean; color?: string }) {
  if (collapsed) {
    return <span role="img" aria-label="PRIME"><PrimeMark /></span>;
  }
  return (
    <span role="img" aria-label="PRIME" style={{ display: 'inline-flex', alignItems: 'center', gap: 10 }}>
      <PrimeMark size={subtitle ? 44 : 32} />
      <span aria-hidden="true" style={{ display: 'flex', flexDirection: 'column', lineHeight: 1.15, color }}>
        <span style={{ fontWeight: 600, fontSize: subtitle ? 26 : 19, letterSpacing: '0.08em' }}>PRIME</span>
        {subtitle && <span style={{ fontSize: 13, opacity: 0.85 }}>Property Registry, Information, Mapping &amp; Evaluation System</span>}
      </span>
    </span>
  );
}
