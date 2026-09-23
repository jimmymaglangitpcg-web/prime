import { useQuery } from '@tanstack/react-query';
import { apiFetch } from '../lib/apiClient';

interface HealthCheckEntry {
  name: string;
  status: string;
  description: string | null;
}

interface HealthReport {
  status: string;
  checks: HealthCheckEntry[];
}

export function HealthPage() {
  const { data, error, isLoading } = useQuery({
    queryKey: ['health'],
    queryFn: () => apiFetch<HealthReport>('/health'),
    retry: false,
  });

  return (
    <section>
      <h1>PRIME — System Health</h1>
      <p>
        Verifies Prime.WebApi is reachable and can see PostgreSQL and
        PostGIS. This page exists as the Phase 2 foundation proof — it is
        not the production dashboard (see CLAUDE.md §55, Phase 11).
      </p>

      {isLoading && <p>Checking…</p>}

      {error && (
        <p role="alert" style={{ color: 'crimson' }}>
          Could not reach the API at the configured VITE_API_BASE_URL. Is
          Prime.WebApi running? ({(error as Error).message})
        </p>
      )}

      {data && (
        <>
          <p>
            Overall status: <strong>{data.status}</strong>
          </p>
          <ul>
            {data.checks.map((check) => (
              <li key={check.name}>
                <strong>{check.name}</strong>: {check.status}
                {check.description ? ` — ${check.description}` : ''}
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
