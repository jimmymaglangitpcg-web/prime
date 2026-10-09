import { test as base, expect, type APIRequestContext, type Browser, type Page } from '@playwright/test';

/**
 * Shared helpers of the browser end-to-end suite (docs/analysis/production-hardening.md §4.7).
 * A test acts as one of the API's DEMO users (appsettings.Development.json, DevAuth:Users) by the key the
 * Development build keeps in localStorage and sends as X-Prime-Dev-Act-As; setup and checks that are not the
 * subject of a test go through the API as the same users. Every record a test makes is DEMO, tagged per run.
 */
export type DemoUser = 'admin' | 'checker' | 'mun-appraiser' | 'mun-assessor' | 'applicant' | 'viewer'
  /** A new applicant who has never signed in (DevelopmentAuthenticationHandler.FreshApplicant). */
  | `applicant-${string}`;

export const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5221';

/** A short tag that keeps this run's PINs, names and numbers apart from earlier runs. */
export const runTag = () => Math.random().toString(16).slice(2, 8).toUpperCase();

/** Today in the LGU's time zone (the API's Lgu:TimeZone), which values and assesses as of that date, not UTC's. */
const lguTimeZone = process.env.E2E_LGU_TIME_ZONE ?? 'Asia/Manila';
export const today = () => new Intl.DateTimeFormat('en-CA', { timeZone: lguTimeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());

/** A page acting as a DEMO user, in its own browser context so two users can work side by side. */
export async function pageAs(browser: Browser, user: DemoUser): Promise<Page> {
  const context = await browser.newContext();
  await context.addInitScript((key) => window.localStorage.setItem('prime.devActAs', key), user);
  return context.newPage();
}

export interface Api {
  get<T>(path: string): Promise<T>;
  post<T>(path: string, body?: unknown): Promise<T>;
  /** The response status of a write, for refusals. */
  postStatus(path: string, body?: unknown): Promise<{ status: number; code?: string }>;
}

export function apiAs(request: APIRequestContext, user: DemoUser): Api {
  const headers = { 'X-Prime-Dev-Act-As': user };
  const read = async <T>(response: Awaited<ReturnType<APIRequestContext['get']>>, what: string): Promise<T> => {
    if (!response.ok()) throw new Error(`${what} → ${response.status()} ${(await response.text()).slice(0, 400)}`);
    const text = await response.text();
    return (text ? JSON.parse(text) : null) as T;
  };
  return {
    get: async (path) => read(await request.get(apiUrl + path, { headers }), `GET ${path}`),
    post: async (path, body = {}) => read(await request.post(apiUrl + path, { headers, data: body }), `POST ${path}`),
    postStatus: async (path, body = {}) => {
      const response = await request.post(apiUrl + path, { headers, data: body });
      const text = await response.text();
      let code: string | undefined;
      try {
        code = text ? (JSON.parse(text) as { code?: string }).code : undefined;
      } catch {
        code = undefined;
      }
      return { status: response.status(), code };
    },
  };
}

interface Named { id: string; name: string }

/** The DEMO E2E set written by `seed-e2e`, looked up by its names. */
export interface E2eSet {
  provinceId: string;
  municipality: Named;
  otherMunicipality: Named;
  barangay: Named;
  otherBarangay: Named;
  classification: Named;
  actualUse: Named;
  ownershipType: Named;
}

export async function e2eSet(api: Api): Promise<E2eSet> {
  const one = <T extends Named>(list: T[], name: string): T => {
    const found = list.filter((x) => x.name === name);
    if (found.length !== 1) {
      throw new Error(`Expected one "${name}", found ${found.length}. Run: dotnet run --project src/Prime.WebApi -- seed-e2e, then restart the API.`);
    }
    return found[0];
  };
  const province = one(await api.get<Named[]>('/api/reference/provinces'), 'DEMO E2E Province');
  const municipalities = await api.get<Named[]>(`/api/reference/municipalities?provinceId=${province.id}`);
  const municipality = one(municipalities, 'DEMO E2E Municipality');
  const otherMunicipality = one(municipalities, 'DEMO E2E Other Municipality');
  return {
    provinceId: province.id,
    municipality,
    otherMunicipality,
    barangay: one(await api.get<Named[]>(`/api/reference/barangays?municipalityId=${municipality.id}`), 'DEMO E2E Barangay'),
    otherBarangay: one(await api.get<Named[]>(`/api/reference/barangays?municipalityId=${otherMunicipality.id}`), 'DEMO E2E Other Barangay'),
    classification: one(await api.get<Named[]>('/api/reference/classifications'), 'DEMO E2E Residential'),
    actualUse: one(await api.get<Named[]>('/api/reference/actual-uses'), 'DEMO E2E Residential use'),
    ownershipType: one(await api.get<Named[]>('/api/reference/ownership-types'), 'DEMO E2E Sole owner'),
  };
}

/** Picks an option of an Ant Design select by its visible text, typing to filter when the select allows it. */
export async function choose(page: Page, label: string | RegExp, option: string) {
  // The whole label (a required field's name starts with "*"), so "Classification" is not "Sub-Classification".
  const name = typeof label === 'string' ? new RegExp(`^\\*?\\s*${label.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}$`) : label;
  const field = page.getByRole('combobox', { name });
  const shown = field.locator('xpath=ancestor::div[contains(concat(" ", @class, " "), " ant-select ")][1]');
  // Already chosen (a dialog may propose the only option): nothing to do.
  if ((await shown.textContent())?.includes(option)) return;
  // Until the select shows the choice: an option list that re-renders as its data arrives can swallow a click.
  await expect(async () => {
    await field.click();
    if ((await field.getAttribute('readonly')) === null) {
      await field.fill(option);
    }
    await page.locator('.ant-select-item-option:visible').filter({ hasText: option }).first().click({ timeout: 5_000 });
    await expect(shown).toContainText(option, { timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
}

/** An issued or previewed form: the document page renders it in an iframe. */
export const formDocument = (page: Page) => page.frameLocator('iframe').first();

export const test = base;
export { expect };
