import { Alert, Button, Descriptions, Form, Input, Result, Select, Space, Spin, Tag, Typography } from 'antd';
import { useMySignUpRequests, useSignUpOptions, useSubmitSignUp, type SignUpInput } from '../../api/accounts';
import { ApiRequestError } from '../../lib/apiClient';
import { signOut, useSupabaseSession } from '../../lib/auth';
import { AuthLayout } from './AuthLayout';

const PROVINCE_WIDE = '__province-wide__';
const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);

/**
 * A signed-up user waiting for access (docs/analysis/workflow-security.md §4.2): they say who they are and the office
 * and roles they ask for; a system administrator approves the request with an office and roles, or rejects it with a
 * reason, after which they may ask again. Nothing else in PRIME is open to them until then.
 */
export function PendingApprovalPage() {
  const mine = useMySignUpRequests();
  const options = useSignUpOptions();
  const submit = useSubmitSignUp();
  const session = useSupabaseSession();
  const [form] = Form.useForm();
  const officeValue = Form.useWatch('officeId', form);

  const latest = mine.data?.[0];
  const waiting = latest?.status === 'PendingReview';
  const provinceWide = officeValue === PROVINCE_WIDE;
  const roleOptions = (options.data?.roles ?? [])
    .filter((r) => !provinceWide || options.data?.provinceWideRoles.includes(r.code))
    .map((r) => ({ value: r.code, label: r.name }));

  const send = async () => {
    const v = await form.validateFields();
    const input: SignUpInput = {
      fullName: v.fullName, position: v.position, officeId: v.officeId === PROVINCE_WIDE ? null : v.officeId, roles: v.roles, note: v.note || null,
    };
    submit.mutate(input);
  };

  const signOutButton = session ? <Button onClick={() => void signOut()}>Sign out</Button> : null;

  if (mine.isLoading || options.isLoading) {
    return <AuthLayout title="Access to PRIME"><Spin /></AuthLayout>;
  }

  if (waiting && latest) {
    return (
      <AuthLayout title="Access to PRIME" width={560}>
        <Result status="info" title="Awaiting approval" style={{ padding: '8px 0 16px' }}
          subTitle="A system administrator will review your request and give you an office and roles. You can then use PRIME at once." />
        <Descriptions size="small" column={1} bordered items={[
          { key: 'name', label: 'Name', children: latest.fullName },
          { key: 'position', label: 'Position', children: latest.position },
          { key: 'office', label: 'Office', children: latest.requestedOfficeName ?? 'Province-wide' },
          { key: 'roles', label: 'Roles asked for', children: <Space size={4} wrap>{latest.requestedRoles.map((r) => <Tag key={r}>{r}</Tag>)}</Space> },
          { key: 'sent', label: 'Sent', children: new Date(latest.createdAt).toLocaleString() },
        ]} />
        <Space style={{ marginTop: 16 }}>
          <Button onClick={() => mine.refetch()} loading={mine.isFetching}>Check again</Button>
          {signOutButton}
        </Space>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title="Ask for access to PRIME" width={560}>
      {latest?.status === 'Rejected' && (
        <Alert type="warning" showIcon style={{ marginBottom: 16 }} title="Your last request was not approved"
          description={latest.decisionReason ?? undefined} />
      )}
      {options.isError && <Alert type="error" showIcon style={{ marginBottom: 16 }} title={errorText(options.error)} />}
      <Typography.Paragraph type="secondary">
        Your account is set up. Tell us who you are and where you work; a system administrator approves it with your office and roles.
      </Typography.Paragraph>
      <Form form={form} layout="vertical" requiredMark={false}
        initialValues={latest ? {
          fullName: latest.fullName, position: latest.position, officeId: latest.requestedOfficeId ?? PROVINCE_WIDE, roles: latest.requestedRoles,
        } : undefined}>
        <Form.Item name="fullName" label="Full name" rules={[{ required: true, whitespace: true, message: 'Enter your full name' }, { max: 200 }]}>
          <Input autoComplete="name" />
        </Form.Item>
        <Form.Item name="position" label="Position" rules={[{ required: true, whitespace: true, message: 'Enter your position' }, { max: 200 }]}>
          <Input placeholder="e.g. Assessment Clerk II" />
        </Form.Item>
        <Form.Item name="officeId" label="Office" rules={[{ required: true, message: 'Choose your office' }]}>
          <Select showSearch optionFilterProp="label" onChange={() => form.setFieldValue('roles', [])} options={[
            ...(options.data?.offices ?? []).map((o) => ({ value: o.id, label: o.name })),
            { value: PROVINCE_WIDE, label: 'Province-wide (system administration or audit)' },
          ]} />
        </Form.Item>
        <Form.Item name="roles" label="Roles asked for" rules={[{ required: true, message: 'Choose at least one role' }]}
          extra={provinceWide ? 'Only these roles are held province-wide; the others belong to an office.' : undefined}>
          <Select mode="multiple" options={roleOptions} />
        </Form.Item>
        <Form.Item name="note" label="Note for the administrator" rules={[{ max: 1000 }]}>
          <Input.TextArea rows={2} />
        </Form.Item>
        {submit.isError && <Alert type="error" showIcon style={{ marginBottom: 16 }} title={errorText(submit.error)} />}
        <Space>
          <Button type="primary" onClick={send} loading={submit.isPending}>Send request</Button>
          {signOutButton}
        </Space>
      </Form>
    </AuthLayout>
  );
}
