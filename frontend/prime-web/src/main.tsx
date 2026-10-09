import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { MutationCache, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import './index.css';
import App from './App.tsx';
import { ApiRequestError, isConcurrencyConflict } from './lib/apiClient';

// A client error (404 not found — including a record outside the user's jurisdiction — 403, 400) will not
// change on retry, so it is shown at once; network and server errors keep the default three retries.
// A write refused because the record changed since it was loaded (409 CONCURRENCY_CONFLICT) reloads every query, so
// the screen shows the current record; the error's own message tells the user to review it and try again
// (docs/analysis/production-hardening.md §4.4).
const queryClient: QueryClient = new QueryClient({
  mutationCache: new MutationCache({
    onError: (error) => {
      if (isConcurrencyConflict(error)) {
        void queryClient.invalidateQueries();
      }
    },
  }),
  defaultOptions: {
    queries: {
      retry: (failureCount, error) =>
        !(error instanceof ApiRequestError && error.status >= 400 && error.status < 500) && failureCount < 3,
    },
  },
});

// WCAG 2.1 AA contrast (docs/analysis/production-hardening.md Q10; docs/TESTING.md §5): Ant Design's default secondary text
// (45 % black) and placeholders (25 %) fall below 4.5:1 on white; these keep at least that ratio.
const theme = {
  colorPrimary: '#1d4ed8',
  colorLink: '#1d4ed8',
  colorTextSecondary: 'rgba(0, 0, 0, 0.65)',
  colorTextTertiary: 'rgba(0, 0, 0, 0.6)',
  colorTextDescription: 'rgba(0, 0, 0, 0.65)',
  colorTextPlaceholder: 'rgba(0, 0, 0, 0.56)',
};

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ConfigProvider theme={{ token: theme }}>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </QueryClientProvider>
    </ConfigProvider>
  </StrictMode>,
);
