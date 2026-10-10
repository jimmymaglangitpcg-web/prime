import type { ReactNode } from 'react';

/** A PIN, TD number or other document number, set in IBM Plex Mono so its digits and dashes line up (ui-theme.md §4.5). */
export function DocNumber({ children }: { children: ReactNode }) {
  return <span className="doc-number">{children}</span>;
}
