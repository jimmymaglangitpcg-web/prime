/**
 * Development only: act as the second development user ("Local Dev Checker"),
 * so maker-checker approvals can be tried on screen
 * (docs/analysis/value-and-assess.md §4). The API honours the header only
 * where its development login bypass is enabled; production builds never send it.
 */
export const devActAsAvailable = import.meta.env.DEV;

const storageKey = 'prime.devActAsChecker';
let actingAsChecker = false;
try {
  actingAsChecker = devActAsAvailable && window.localStorage.getItem(storageKey) === 'true';
} catch {
  // Storage can be unavailable (private window, blocked site data): start as the usual user.
}

const listeners = new Set<() => void>();

export function isActingAsChecker(): boolean {
  return devActAsAvailable && actingAsChecker;
}

export function setActingAsChecker(value: boolean): void {
  actingAsChecker = devActAsAvailable && value;
  try {
    window.localStorage.setItem(storageKey, String(actingAsChecker));
  } catch {
    // Remembering the choice is a convenience only.
  }
  listeners.forEach((listener) => listener());
}

export function subscribeActingAsChecker(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** The header the API's development login reads. */
export const devActAsHeader = 'X-Prime-Dev-Act-As';
