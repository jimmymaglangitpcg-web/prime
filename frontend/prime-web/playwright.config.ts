import { defineConfig, devices } from '@playwright/test';

// Browser end-to-end suite (docs/analysis/production-hardening.md §4.7, Q8; how to run it: docs/TESTING.md §4).
// It drives the Development build against the local API with its sign-in bypass, acting as the DEMO users of
// appsettings.Development.json, on the DEMO E2E set written by `dotnet run --project src/Prime.WebApi -- seed-e2e`.
// The tests share one database and act as maker and checker in turn, so they run one at a time.
const api = process.env.E2E_API_URL ?? 'http://localhost:5221';
const web = process.env.E2E_WEB_URL ?? 'http://localhost:5173';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 120_000,
  expect: { timeout: 15_000 },
  forbidOnly: !!process.env.CI,
  // On GitHub the errors also become annotations of the run, readable without downloading the report.
  reporter: process.env.CI ? [['list'], ['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: web,
    // The office's browsers run in the LGU's time zone, as the API's Lgu:TimeZone does; a CI runner's would be UTC.
    timezoneId: process.env.E2E_BROWSER_TIME_ZONE ?? 'Asia/Manila',
    viewport: { width: 1400, height: 900 },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1400, height: 900 } } }],
  webServer: [
    {
      // The seed is idempotent; the office seeder then gives its DEMO municipalities their offices as the API starts.
      command: 'dotnet run --project ../../src/Prime.WebApi --launch-profile http -- seed-e2e && dotnet run --project ../../src/Prime.WebApi --launch-profile http',
      url: `${api}/health`,
      reuseExistingServer: !process.env.CI,
      timeout: 300_000,
    },
    {
      command: 'npm run dev -- --port 5173 --strictPort',
      url: web,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      // Sign-in goes through the API's Development bypass, so Supabase is never called; the client only needs a URL to load.
      env: {
        VITE_API_BASE_URL: api,
        VITE_SUPABASE_URL: process.env.VITE_SUPABASE_URL ?? 'http://localhost:54321',
        VITE_SUPABASE_ANON_KEY: process.env.VITE_SUPABASE_ANON_KEY ?? 'e2e-placeholder',
      },
    },
  ],
});
