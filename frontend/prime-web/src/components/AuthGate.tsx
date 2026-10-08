import { useEffect, type ReactNode } from 'react';
import { Button, Result, Spin } from 'antd';
import { useLocation, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useCurrentUser } from '../api/offices';
import { ApiRequestError } from '../lib/apiClient';
import { signOut, useIdleSignOut, useSupabaseSession } from '../lib/auth';
import { supabase } from '../lib/supabaseClient';
import { AuthLayout } from '../pages/auth/AuthLayout';
import { MfaPage } from '../pages/auth/MfaPage';
import { PendingApprovalPage } from '../pages/auth/PendingApprovalPage';
import { ResetPasswordPage } from '../pages/auth/ResetPasswordPage';
import { SignInPage } from '../pages/auth/SignInPage';

/**
 * What a visitor sees before the application (docs/analysis/workflow-security.md §4.2), decided by the API's answer to
 * "who am I": not signed in → sign-in; disabled → a notice; pending → the sign-up request; a role needing a second
 * factor without one → the MFA step; otherwise PRIME. In Development the API's bypass answers for a DEMO user, so the
 * application opens directly.
 */
export function AuthGate({ children }: { children: ReactNode }) {
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const session = useSupabaseSession();
  const me = useCurrentUser();

  useEffect(() => {
    const { data } = supabase.auth.onAuthStateChange((event) => {
      if (event === 'PASSWORD_RECOVERY') navigate('/reset-password', { replace: true });
      // Signed out: nothing read under the previous user stays cached.
      if (event === 'SIGNED_OUT') queryClient.clear();
      if (event === 'SIGNED_IN' || event === 'MFA_CHALLENGE_VERIFIED' || event === 'SIGNED_OUT') void queryClient.invalidateQueries({ queryKey: ['me'] });
    });
    return () => data.subscription.unsubscribe();
  }, [navigate, queryClient]);

  // Only a real Supabase session can be signed out; the Development bypass has none.
  useIdleSignOut(me.data?.idleMinutes ?? 0, !!session && me.data?.status === 'Active');

  if (location.pathname === '/reset-password') return <ResetPasswordPage />;
  if (location.pathname === '/sign-in' && !session) return <SignInPage />;

  if (me.isLoading) {
    return <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center' }}><Spin size="large" /></div>;
  }
  if (me.error) {
    const error = me.error instanceof ApiRequestError ? me.error : null;
    if (error?.apiError.code === 'USER_DISABLED') {
      return (
        <AuthLayout title="Account disabled">
          <Result status="warning" title="Your PRIME account has been disabled" subTitle="Ask a system administrator if you think this is a mistake."
            extra={session ? <Button onClick={() => void signOut()}>Sign out</Button> : undefined} />
        </AuthLayout>
      );
    }
    if (error?.status === 401) return <SignInPage />;
    return (
      <AuthLayout title="PRIME is not reachable">
        <Result status="error" title="Could not reach the PRIME server" subTitle={error?.apiError.message ?? (me.error as Error).message}
          extra={<Button onClick={() => me.refetch()}>Try again</Button>} />
      </AuthLayout>
    );
  }
  if (me.data?.status === 'Pending') return <PendingApprovalPage />;
  if (me.data?.mfaRequired && !me.data.mfaSatisfied) return <MfaPage />;
  return <>{children}</>;
}
