import { useState } from 'react';
import { Alert, Button, Card, Checkbox, Form, Input, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import { CheckOutlined, EditOutlined } from '@ant-design/icons';
import {
  useApproveRolePermissionChange, useProposeRolePermissions, useRejectRolePermissionChange, useRolePermissions,
  type PermissionDto, type PermissionMatrixDto, type RolePermissionChangeDto,
} from '../../api/rolePermissions';
import { useCan, useCurrentUser } from '../../api/offices';
import { errorText, statusTag, useToast } from './ruleHelpers';

/**
 * Which role may do what (docs/analysis/workflow-security.md §4.1, Q2). The default is provisional and is checked against
 * the province's positions; a change to a role takes effect only when a second user approves it (CLAUDE.md §46).
 */
export function RolePermissionsPage() {
  const { data, isLoading } = useRolePermissions();
  const can = useCan();
  const [editing, setEditing] = useState(false);
  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Typography.Title level={3} style={{ margin: 0 }}>Role Permissions</Typography.Title>
        {can('users.manage') && <Button icon={<EditOutlined />} onClick={() => setEditing(true)} disabled={!data}>Change a role</Button>}
      </Space>
      <Alert type="info" showIcon title="Provisional default"
        description="Each user's permissions come from the roles of their office assignment in force. The default matrix is PRIME's provisional proposal, to be checked against the province's positions and delegations. A change applies only when another user approves it." />
      {data && <Changes changes={data.changes} />}
      <Card title="Matrix">
        <Table<PermissionDto> rowKey="code" size="small" loading={isLoading} dataSource={data?.permissions ?? []} pagination={false}
          scroll={{ x: 'max-content' }}
          columns={[
            { title: 'Module', dataIndex: 'module', fixed: 'left', width: 130 },
            {
              title: 'Permission', fixed: 'left', width: 300,
              render: (_, p) => <><div>{p.name}</div><Typography.Text type="secondary" style={{ fontSize: 12 }}>{p.code}</Typography.Text></>,
            },
            ...(data?.roles ?? []).map((r) => ({
              title: <span title={r.roleName}>{r.roleCode.replaceAll('_', ' ')}</span>, key: r.roleCode, align: 'center' as const, width: 96,
              render: (_: unknown, p: PermissionDto) => (r.permissions.includes(p.code) ? <CheckOutlined style={{ color: '#389e0d' }} aria-label="granted" /> : null),
            })),
          ]} />
      </Card>
      {editing && data && <ProposeModal matrix={data} onClose={() => setEditing(false)} />}
    </Space>
  );
}

function Changes({ changes }: { changes: RolePermissionChangeDto[] }) {
  const can = useCan();
  const me = useCurrentUser();
  const approve = useApproveRolePermissionChange();
  const reject = useRejectRolePermissionChange();
  const { context, fail } = useToast();
  const [rejecting, setRejecting] = useState<RolePermissionChangeDto | null>(null);
  const [reason, setReason] = useState('');
  return (
    <Card title="Changes">
      {context}
      <Table<RolePermissionChangeDto> rowKey="id" size="small" dataSource={changes} pagination={false} scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'No change proposed' }}
        columns={[
          { title: 'Role', dataIndex: 'roleCode' },
          {
            title: 'Change', render: (_, c) => (
              <Space size={[4, 4]} wrap style={{ maxWidth: 420 }}>
                {c.grant.map((g) => <Tag key={g} color="green">+ {g}</Tag>)}
                {c.revoke.map((g) => <Tag key={g} color="red">− {g}</Tag>)}
              </Space>
            ),
          },
          { title: 'Reason', dataIndex: 'reason' },
          { title: 'Proposed', dataIndex: 'createdAt', render: (v: string) => v.slice(0, 10) },
          { title: 'Status', render: (_, c) => <Space orientation="vertical" size={0}>{statusTag(c.status)}{c.decisionReason && <Typography.Text type="secondary">{c.decisionReason}</Typography.Text>}</Space> },
          {
            // Maker-checker: another user decides (the API refuses the author anyway).
            title: '', render: (_, c) => c.status === 'Draft' && can('users.approve') && c.createdBy !== me.data?.userId && (
              <Space>
                <Button size="small" type="primary" loading={approve.isPending} onClick={() => approve.mutate(c.id, { onError: fail })}>Approve</Button>
                <Button size="small" danger onClick={() => { setReason(''); setRejecting(c); }}>Reject</Button>
              </Space>
            ),
          },
        ]} />
      {rejecting && (
        <Modal open title={`Reject the change to ${rejecting.roleCode}`} okText="Reject" okButtonProps={{ danger: true, disabled: !reason.trim(), loading: reject.isPending }}
          onCancel={() => setRejecting(null)}
          onOk={() => reject.mutate({ id: rejecting.id, reason }, { onSuccess: () => setRejecting(null), onError: fail })}>
          <Input.TextArea rows={3} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Why it is rejected" />
        </Modal>
      )}
    </Card>
  );
}

function ProposeModal({ matrix, onClose }: { matrix: PermissionMatrixDto; onClose: () => void }) {
  const propose = useProposeRolePermissions();
  const [roleCode, setRoleCode] = useState(matrix.roles[0]?.roleCode ?? '');
  const role = matrix.roles.find((r) => r.roleCode === roleCode);
  const modules = [...new Set(matrix.permissions.map((p) => p.module))];
  return (
    <Modal open title="Change a role's permissions" footer={null} width={760} destroyOnHidden onCancel={onClose}>
      {propose.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Not proposed" description={errorText(propose.error)} />}
      <Form layout="vertical" style={{ marginBottom: 12 }}>
        <Form.Item label="Role">
          <Select value={roleCode} onChange={setRoleCode} options={matrix.roles.map((r) => ({ value: r.roleCode, label: `${r.roleCode} — ${r.roleName}` }))} />
        </Form.Item>
      </Form>
      {/* Remounted per role so the boxes start from that role's current permissions. */}
      {role && <RoleForm key={role.roleCode} initial={role.permissions} modules={modules} permissions={matrix.permissions}
        pending={propose.isPending} onSubmit={(permissions, reason) => propose.mutate({ roleCode, permissions, reason }, { onSuccess: onClose })} />}
    </Modal>
  );
}

function RoleForm({ initial, modules, permissions, pending, onSubmit }: {
  initial: string[]; modules: string[]; permissions: PermissionDto[]; pending: boolean; onSubmit: (permissions: string[], reason: string) => void;
}) {
  const [checked, setChecked] = useState<string[]>(initial);
  const [reason, setReason] = useState('');
  const changed = checked.length !== initial.length || checked.some((c) => !initial.includes(c));
  return (
    <>
      <div style={{ maxHeight: 380, overflowY: 'auto', marginBottom: 12 }}>
        {modules.map((m) => (
          <div key={m} style={{ marginBottom: 8 }}>
            <Typography.Text strong>{m}</Typography.Text>
            <Checkbox.Group style={{ display: 'flex', flexDirection: 'column', marginLeft: 12 }}
              value={checked.filter((c) => permissions.some((p) => p.code === c && p.module === m))}
              onChange={(v) => setChecked([...checked.filter((c) => !permissions.some((p) => p.code === c && p.module === m)), ...(v as string[])])}
              options={permissions.filter((p) => p.module === m).map((p) => ({ value: p.code, label: `${p.name} (${p.code})` }))} />
          </div>
        ))}
      </div>
      <Input.TextArea rows={2} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Reason for the change (required)"
        style={{ marginBottom: 12 }} />
      <Button type="primary" disabled={!changed || !reason.trim()} loading={pending} onClick={() => onSubmit(checked, reason)}>Propose</Button>
    </>
  );
}
