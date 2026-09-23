import { createClient } from '@supabase/supabase-js';

const supabaseUrl = import.meta.env.VITE_SUPABASE_URL;
const supabaseAnonKey = import.meta.env.VITE_SUPABASE_ANON_KEY;

if (!supabaseUrl || !supabaseAnonKey) {
  // Loud, not silent: an unconfigured Supabase project must not fail
  // invisibly later when a sign-in attempt mysteriously does nothing.
  console.warn(
    'VITE_SUPABASE_URL / VITE_SUPABASE_ANON_KEY are not set. ' +
      'Auth will not work until frontend/prime-web/.env.local is configured — see .env.example at the repo root.',
  );
}

export const supabase = createClient(supabaseUrl ?? '', supabaseAnonKey ?? '');
