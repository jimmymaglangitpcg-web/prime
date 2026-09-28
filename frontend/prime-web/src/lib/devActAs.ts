/**
 * Development only: act as one of the API's named DEMO users (for example the
 * DEMO municipal appraiser, or the "checker" who approves what the usual user
 * created), so offices and maker-checker can be tried on screen
 * (docs/analysis/province-wide-operation.md Q13). The API honours the header
 * only where its development login bypass is enabled; production builds never send it.
 */
export const devActAsAvailable = import.meta.env.DEV;

const storageKey = 'prime.devActAs';
const legacyKey = 'prime.devActAsChecker';
let actingAs: string | null = null;
try {
  if (devActAsAvailable) {
    actingAs = window.localStorage.getItem(storageKey) || (window.localStorage.getItem(legacyKey) === 'true' ? 'checker' : null);
  }
} catch {
  // Storage can be unavailable (private window, blocked site data): start as the usual user.
}

const listeners = new Set<() => void>();

/** The DEMO user key to act as; null for the usual development user. */
export function getDevActAs(): string | null {
  return devActAsAvailable ? actingAs : null;
}

export function setDevActAs(key: string | null): void {
  actingAs = devActAsAvailable ? key : null;
  try {
    if (actingAs) window.localStorage.setItem(storageKey, actingAs);
    else window.localStorage.removeItem(storageKey);
    window.localStorage.removeItem(legacyKey);
  } catch {
    // Remembering the choice is a convenience only.
  }
  listeners.forEach((listener) => listener());
}

export function subscribeDevActAs(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** The header the API's development login reads. */
export const devActAsHeader = 'X-Prime-Dev-Act-As';
