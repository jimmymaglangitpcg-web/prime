import { useEffect, useState } from 'react';
import type { Session } from '@supabase/supabase-js';
import { supabase } from './supabaseClient';
import { apiPost } from './apiClient';

/**
 * Sign-in with Supabase Auth (docs/analysis/workflow-security.md §4.2). Supabase holds the passwords, lockout and
 * second factors; PRIME's API decides what the signed-in user may do. Sign-in and sign-out are reported to the API,
 * which logs them as LOGIN and LOGOUT.
 */

/** The current Supabase session; undefined while it is being read. */
export function useSupabaseSession(): Session | null | undefined {
  const [session, setSession] = useState<Session | null | undefined>(undefined);
  useEffect(() => {
    let live = true;
    supabase.auth.getSession().then(({ data }) => live && setSession(data.session));
    const { data } = supabase.auth.onAuthStateChange((_event, next) => setSession(next));
    return () => {
      live = false;
      data.subscription.unsubscribe();
    };
  }, []);
  return session;
}

/** Where Supabase's e-mail links (confirmation, password reset) return to. */
export const redirectUrl = (path: string) => `${window.location.origin}${path}`;

const reportSession = (event: 'SignIn' | 'SignOut', reason?: string) =>
  apiPost<boolean>('/api/session', { event, reason: reason ?? null }).catch(() => {
    // The sign-in or sign-out itself stands; Supabase keeps its own auth log.
  });

export async function signInWithPassword(email: string, password: string): Promise<string | null> {
  const { error } = await supabase.auth.signInWithPassword({ email, password });
  if (error) return error.message;
  await reportSession('SignIn');
  return null;
}

let signedOutReason: string | null = null;

/** Why the last sign-out happened, once (e.g. after inactivity), for the sign-in screen. */
export function takeSignedOutReason(): string | null {
  const reason = signedOutReason;
  signedOutReason = null;
  return reason;
}

export async function signOut(reason?: 'idle'): Promise<void> {
  await reportSession('SignOut', reason);
  signedOutReason = reason ?? null;
  await supabase.auth.signOut();
}

/** Signs the user out after the given minutes without keyboard, mouse or touch activity (Q9). */
export function useIdleSignOut(minutes: number, active: boolean) {
  useEffect(() => {
    if (!active || minutes <= 0) return;
    let last = Date.now();
    const touch = () => { last = Date.now(); };
    const events = ['mousemove', 'mousedown', 'keydown', 'scroll', 'touchstart', 'wheel'] as const;
    events.forEach((e) => window.addEventListener(e, touch, { passive: true }));
    const timer = window.setInterval(() => {
      if (Date.now() - last >= minutes * 60_000) {
        window.clearInterval(timer);
        void signOut('idle');
      }
    }, 15_000);
    return () => {
      window.clearInterval(timer);
      events.forEach((e) => window.removeEventListener(e, touch));
    };
  }, [minutes, active]);
}
