import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { MutationCache, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import '@fontsource/ibm-plex-sans/latin-400.css';
import '@fontsource/ibm-plex-sans/latin-500.css';
import '@fontsource/ibm-plex-sans/latin-600.css';
import '@fontsource/ibm-plex-mono/latin-400.css';
import '@fontsource/ibm-plex-mono/latin-500.css';
import './index.css';
import { primeTheme } from './theme';
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

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ConfigProvider theme={primeTheme}>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </QueryClientProvider>
    </ConfigProvider>
  </StrictMode>,
);
