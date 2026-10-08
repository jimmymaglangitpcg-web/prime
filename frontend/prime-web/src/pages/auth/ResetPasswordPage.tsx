import { useState } from 'react';
import { Alert, Button, Form, Input, Spin } from 'antd';
import { useNavigate } from 'react-router-dom';
import { supabase } from '../../lib/supabaseClient';
import { useSupabaseSession } from '../../lib/auth';
import { AuthLayout } from './AuthLayout';
import { confirmRule } from './passwordRules';

/** Sets a new password from the e-mailed reset link, which signs the user in for this purpose (§4.2). */
export function ResetPasswordPage() {
  const session = useSupabaseSession();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const save = async (v: { password: string }) => {
    setBusy(true);
    setError(null);
    const { error: failed } = await supabase.auth.updateUser({ password: v.password });
    setBusy(false);
    if (failed) setError(failed.message);
    else navigate('/', { replace: true });
  };

  return (
    <AuthLayout title="Set a new password">
      {session === undefined ? <Spin /> : session === null ? (
        <Alert type="warning" showIcon title="This link is not valid or has expired."
          description={<Button type="link" style={{ padding: 0 }} onClick={() => navigate('/', { replace: true })}>Back to sign in</Button>} />
      ) : (
        <Form layout="vertical" onFinish={save} requiredMark={false}>
          {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 16 }} />}
          <Form.Item name="password" label="New password" rules={[{ required: true, min: 12, message: 'At least 12 characters' }]}>
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Form.Item name="confirm" label="Confirm password" dependencies={['password']}
            rules={[{ required: true, message: 'Confirm the password' }, confirmRule]}>
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={busy}>Save password</Button>
        </Form>
      )}
    </AuthLayout>
  );
}
