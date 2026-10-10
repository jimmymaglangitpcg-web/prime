import { useState } from 'react';
import { Alert, Button, Form, Input, Segmented, Typography } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { supabase } from '../../lib/supabaseClient';
import { redirectUrl, signInWithPassword, takeSignedOutReason } from '../../lib/auth';
import { AuthLayout } from './AuthLayout';
import { confirmRule } from './passwordRules';

type Mode = 'sign-in' | 'sign-up' | 'reset';
const headings: Record<Mode, string> = { 'sign-in': 'Sign in', 'sign-up': 'Create an account', reset: 'Reset your password' };

type Notice = { type: 'success' | 'error' | 'info'; text: string } | null;

/**
 * Sign in, create an account or ask for a password reset (docs/analysis/workflow-security.md §4.2). A new account
 * confirms its e-mail, then waits for a system administrator to approve it with an office and roles.
 */
export function SignInPage() {
  const [tab, setTab] = useState<Mode>('sign-in');
  const [notice, setNotice] = useState<Notice>(() =>
    takeSignedOutReason() === 'idle' ? { type: 'info', text: 'You were signed out after a period without activity.' } : null);
  const [busy, setBusy] = useState(false);
  const queryClient = useQueryClient();

  const run = async (work: () => Promise<Notice>) => {
    setBusy(true);
    setNotice(null);
    try {
      setNotice(await work());
    } catch (e) {
      setNotice({ type: 'error', text: (e as Error).message });
    } finally {
      setBusy(false);
    }
  };

  const signIn = (v: { email: string; password: string }) => run(async () => {
    const error = await signInWithPassword(v.email.trim(), v.password);
    if (error) return { type: 'error', text: error };
    await queryClient.invalidateQueries();
    return null;
  });

  const signUp = (v: { email: string; password: string }) => run(async () => {
    const { data, error } = await supabase.auth.signUp({ email: v.email.trim(), password: v.password, options: { emailRedirectTo: redirectUrl('/') } });
    if (error) return { type: 'error', text: error.message };
    if (data.session) {
      await queryClient.invalidateQueries();
      return null;
    }
    setTab('sign-in');
    return { type: 'success', text: 'Check your e-mail and open the confirmation link, then sign in to ask for access.' };
  });

  const reset = (v: { email: string }) => run(async () => {
    const { error } = await supabase.auth.resetPasswordForEmail(v.email.trim(), { redirectTo: redirectUrl('/reset-password') });
    if (error) return { type: 'error', text: error.message };
    return { type: 'success', text: 'If the address has an account, an e-mail with a link to set a new password is on its way.' };
  });

  const email = (
    <Form.Item name="email" label="E-mail" rules={[{ required: true, type: 'email', message: 'Enter your e-mail address' }]}>
      <Input autoComplete="email" />
    </Form.Item>
  );

  return (
    <AuthLayout title={headings[tab]}>
      <Segmented<Mode> block value={tab} onChange={(k) => { setTab(k); setNotice(null); }} style={{ marginBottom: 20 }} aria-label="Sign in, create an account or reset a password"
        options={[{ value: 'sign-in', label: 'Sign in' }, { value: 'sign-up', label: 'New account' }, { value: 'reset', label: 'Forgot password' }]} />
      {notice && <Alert type={notice.type} showIcon title={notice.text} style={{ marginBottom: 16 }} />}
      {tab === 'sign-in' && (
        <Form layout="vertical" onFinish={signIn} requiredMark={false}>
          {email}
          <Form.Item name="password" label="Password" rules={[{ required: true, message: 'Enter your password' }]}>
            <Input.Password autoComplete="current-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={busy}>Sign in</Button>
        </Form>
      )}
      {tab === 'sign-up' && (
        <Form layout="vertical" onFinish={signUp} requiredMark={false}>
          <Typography.Paragraph type="secondary">
            For staff of the assessors&apos; offices. After you confirm your e-mail and sign in, you ask for access; a system administrator
            approves it with your office and roles.
          </Typography.Paragraph>
          {email}
          <Form.Item name="password" label="Password" rules={[{ required: true, min: 12, message: 'At least 12 characters' }]}>
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Form.Item name="confirm" label="Confirm password" dependencies={['password']}
            rules={[{ required: true, message: 'Confirm the password' }, confirmRule]}>
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={busy}>Create account</Button>
        </Form>
      )}
      {tab === 'reset' && (
        <Form layout="vertical" onFinish={reset} requiredMark={false}>
          <Typography.Paragraph type="secondary">We will e-mail you a link to set a new password.</Typography.Paragraph>
          {email}
          <Button htmlType="submit" block loading={busy}>Send reset link</Button>
        </Form>
      )}
    </AuthLayout>
  );
}

