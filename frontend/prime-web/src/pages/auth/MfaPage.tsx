import { useEffect, useState } from 'react';
import { Alert, Button, Input, Space, Spin, Typography } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { supabase } from '../../lib/supabaseClient';
import { signOut } from '../../lib/auth';
import { AuthLayout } from './AuthLayout';

type Step =
  | { kind: 'loading' }
  | { kind: 'enrol'; factorId: string; qrCode: string; secret: string }
  | { kind: 'challenge'; factorId: string }
  | { kind: 'error'; message: string };

/**
 * The second factor some roles need (docs/analysis/workflow-security.md §4.2, Q7): enrol an authenticator app the
 * first time, then enter its code at each sign-in. Supabase Auth keeps the factor; the API refuses those roles until
 * the session carries it.
 */
export function MfaPage() {
  const [step, setStep] = useState<Step>({ kind: 'loading' });
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const queryClient = useQueryClient();

  useEffect(() => {
    let live = true;
    (async () => {
      const factors = await supabase.auth.mfa.listFactors();
      if (factors.error) throw factors.error;
      const verified = factors.data.totp.find((f) => f.status === 'verified');
      if (verified) {
        if (live) setStep({ kind: 'challenge', factorId: verified.id });
        return;
      }
      // An enrolment left unfinished would block a new one.
      for (const f of factors.data.all.filter((x) => x.factor_type === 'totp' && x.status === 'unverified')) {
        await supabase.auth.mfa.unenroll({ factorId: f.id });
      }
      const enrolled = await supabase.auth.mfa.enroll({ factorType: 'totp', friendlyName: 'PRIME' });
      if (enrolled.error) throw enrolled.error;
      if (live) setStep({ kind: 'enrol', factorId: enrolled.data.id, qrCode: enrolled.data.totp.qr_code, secret: enrolled.data.totp.secret });
    })().catch((e: Error) => live && setStep({ kind: 'error', message: e.message }));
    return () => { live = false; };
  }, []);

  const verify = async () => {
    if (step.kind !== 'enrol' && step.kind !== 'challenge') return;
    setBusy(true);
    setError(null);
    const { error: failed } = await supabase.auth.mfa.challengeAndVerify({ factorId: step.factorId, code: code.trim() });
    setBusy(false);
    if (failed) {
      setError(failed.message);
      return;
    }
    await queryClient.invalidateQueries();
  };

  const codeInput = (
    <Space.Compact style={{ width: '100%' }}>
      <Input inputMode="numeric" autoComplete="one-time-code" maxLength={6} placeholder="6-digit code" value={code}
        onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))} onPressEnter={verify} aria-label="Authenticator code" />
      <Button type="primary" onClick={verify} loading={busy} disabled={code.length !== 6}>Verify</Button>
    </Space.Compact>
  );

  return (
    <AuthLayout title="Second sign-in step">
      <Typography.Paragraph type="secondary">
        One of your roles needs a code from an authenticator app at each sign-in, in addition to your password.
      </Typography.Paragraph>
      {step.kind === 'loading' && <Spin />}
      {step.kind === 'error' && <Alert type="error" showIcon title={step.message} />}
      {step.kind === 'enrol' && (
        <Space orientation="vertical" style={{ width: '100%' }}>
          <Typography.Text>Scan this code with an authenticator app, then enter the code it shows.</Typography.Text>
          <img src={step.qrCode} alt="QR code to add PRIME to an authenticator app" width={180} height={180} style={{ alignSelf: 'center' }} />
          <Typography.Text type="secondary">Or enter this key by hand: <Typography.Text code copyable>{step.secret}</Typography.Text></Typography.Text>
          {codeInput}
        </Space>
      )}
      {step.kind === 'challenge' && (
        <Space orientation="vertical" style={{ width: '100%' }}>
          <Typography.Text>Enter the code your authenticator app shows for PRIME.</Typography.Text>
          {codeInput}
        </Space>
      )}
      {error && <Alert type="error" showIcon title={error} style={{ marginTop: 16 }} />}
      <Button type="link" style={{ padding: 0, marginTop: 16 }} onClick={() => void signOut()}>Sign out</Button>
    </AuthLayout>
  );
}
