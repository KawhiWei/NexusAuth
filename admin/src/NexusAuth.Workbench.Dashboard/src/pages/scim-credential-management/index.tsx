import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { AxiosError } from 'axios';
import { Button, Checkbox, DatePicker, Dialog, Drawer, Form, Input, MessagePlugin, Space, Switch, Table, Tag, Textarea, type TableProps } from 'tdesign-react';
import { AddIcon, CopyIcon, DeleteIcon, EditIcon, RefreshIcon } from 'tdesign-icons-react';
import {
  createScimCredential,
  getScimCredentials,
  revokeScimCredential,
  updateScimCredential,
  SCIM_CREDENTIAL_SCOPES,
  type ScimCredential,
  type ScimCredentialScope,
} from '../../api/scim-credential';
import './style.less';

type CredentialFormData = {
  name: string;
  scopes: ScimCredentialScope[];
  expiresAt: string;
  isActive: boolean;
};

const defaultFormData: CredentialFormData = {
  name: '',
  scopes: [...SCIM_CREDENTIAL_SCOPES],
  expiresAt: '',
  isActive: true,
};

const scopeOptions = [
  { label: '读取权限 (scim:read)', value: 'scim:read' },
  { label: '写入权限 (scim:write)', value: 'scim:write' },
];

const getRequestErrorMessage = (error: unknown, fallback: string) => {
  const axiosError = error as AxiosError<{ title?: string; detail?: string; message?: string }>;
  return axiosError.response?.data?.detail
    || axiosError.response?.data?.message
    || axiosError.response?.data?.title
    || fallback;
};

const formatDateTime = (value?: string | null) => {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString('zh-CN', { hour12: false });
};

const isExpired = (credential: ScimCredential) => {
  return Boolean(credential.expiresAt && new Date(credential.expiresAt).getTime() <= Date.now());
};

const getCredentialStatus = (credential: ScimCredential) => {
  if (credential.revokedAt) {
    return { label: '已吊销', theme: 'danger' as const };
  }
  if (isExpired(credential)) {
    return { label: '已过期', theme: 'warning' as const };
  }
  return credential.isActive
    ? { label: '启用', theme: 'success' as const }
    : { label: '禁用', theme: 'default' as const };
};

const toFormData = (credential: ScimCredential): CredentialFormData => ({
  name: credential.name,
  scopes: credential.scopes.filter((scope): scope is ScimCredentialScope => scope === 'scim:read' || scope === 'scim:write'),
  expiresAt: credential.expiresAt ?? '',
  isActive: credential.isActive,
});

const toDatePickerValue = (value: string) => {
  if (!value) {
    return undefined;
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date;
};

const ScimCredentialManagementPage = () => {
  const [credentials, setCredentials] = useState<ScimCredential[]>([]);
  const [loading, setLoading] = useState(false);
  const [formVisible, setFormVisible] = useState(false);
  const [editingCredential, setEditingCredential] = useState<ScimCredential | null>(null);
  const [formData, setFormData] = useState<CredentialFormData>(defaultFormData);
  const [submitting, setSubmitting] = useState(false);
  const [revokeTarget, setRevokeTarget] = useState<ScimCredential | null>(null);
  const [revoking, setRevoking] = useState(false);
  const [tokenVisible, setTokenVisible] = useState(false);
  const [createdToken, setCreatedToken] = useState('');
  const [createdCredentialName, setCreatedCredentialName] = useState('');
  const formRef = useRef<any>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      setCredentials(await getScimCredentials());
    } catch (error) {
      console.error('Failed to fetch SCIM credentials:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '加载 SCIM 凭证失败'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchData();
  }, [fetchData]);

  const handleShowCreate = () => {
    setEditingCredential(null);
    setFormData(defaultFormData);
    setFormVisible(true);
  };

  const handleShowEdit = (credential: ScimCredential) => {
    setEditingCredential(credential);
    setFormData(toFormData(credential));
    setFormVisible(true);
  };

  const handleCloseForm = () => {
    if (submitting) {
      return;
    }
    setFormVisible(false);
    setEditingCredential(null);
    setFormData(defaultFormData);
  };

  const handleScopeChange = (value: Array<string | number | boolean>) => {
    const scopes = value.filter((item): item is ScimCredentialScope => item === 'scim:read' || item === 'scim:write');
    setFormData((previous) => ({ ...previous, scopes }));
  };

  const handleExpiryChange = (value: string | number | Date | Array<string | number | Date>) => {
    const next = Array.isArray(value) ? value[0] : value;
    if (!next) {
      setFormData((previous) => ({ ...previous, expiresAt: '' }));
      return;
    }

    const date = next instanceof Date ? next : new Date(next);
    setFormData((previous) => ({
      ...previous,
      expiresAt: Number.isNaN(date.getTime()) ? '' : date.toISOString(),
    }));
  };

  const handleSubmit = async () => {
    if (!formRef.current) {
      return;
    }

    const validation = await formRef.current.validate();
    if (validation.errors && Object.keys(validation.errors).length > 0) {
      return;
    }
    if (formData.scopes.length === 0) {
      MessagePlugin.warning('至少选择一项凭证权限');
      return;
    }

    const name = formData.name.trim();
    if (!name) {
      MessagePlugin.warning('请输入凭证名称');
      return;
    }

    try {
      setSubmitting(true);
      if (editingCredential) {
        await updateScimCredential(editingCredential.id, {
          name,
          scopes: formData.scopes,
          expiresAt: formData.expiresAt || null,
          isActive: formData.isActive,
        });
        setFormVisible(false);
        setEditingCredential(null);
        setFormData(defaultFormData);
        await fetchData();
        MessagePlugin.success('SCIM 凭证已更新');
      } else {
        const result = await createScimCredential({
          name,
          scopes: formData.scopes,
          expiresAt: formData.expiresAt || null,
        });
        setFormVisible(false);
        setEditingCredential(null);
        setFormData(defaultFormData);
        setCreatedToken(result.token);
        setCreatedCredentialName(result.credential.name);
        setTokenVisible(true);
        await fetchData();
        MessagePlugin.success('SCIM 凭证已创建');
      }
    } catch (error) {
      console.error('Failed to save SCIM credential:', error);
      MessagePlugin.error(getRequestErrorMessage(error, editingCredential ? '更新 SCIM 凭证失败' : '创建 SCIM 凭证失败'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleConfirmRevoke = async () => {
    if (!revokeTarget) {
      return;
    }

    try {
      setRevoking(true);
      await revokeScimCredential(revokeTarget.id);
      setRevokeTarget(null);
      await fetchData();
      MessagePlugin.success('SCIM 凭证已吊销');
    } catch (error) {
      console.error('Failed to revoke SCIM credential:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '吊销 SCIM 凭证失败'));
    } finally {
      setRevoking(false);
    }
  };

  const handleCopyToken = async () => {
    if (!createdToken) {
      return;
    }

    try {
      await navigator.clipboard.writeText(createdToken);
      MessagePlugin.success('Token 已复制');
    } catch (error) {
      console.error('Failed to copy SCIM token:', error);
      MessagePlugin.error('复制失败，请手动复制');
    }
  };

  const handleCloseToken = () => {
    setTokenVisible(false);
    setCreatedToken('');
    setCreatedCredentialName('');
  };

  const columns: TableProps<ScimCredential>['columns'] = useMemo(
    () => [
      { colKey: 'name', title: '凭证名称', minWidth: 190, ellipsis: true, cell: ({ row }) => <strong className="scim-credential-name">{row.name}</strong> },
      {
        colKey: 'scopes',
        title: '权限',
        minWidth: 220,
        cell: ({ row }) => (
          <Space size={4} breakLine>
            {row.scopes.map((scope) => <Tag key={scope} theme="primary" variant="light">{scope}</Tag>)}
          </Space>
        ),
      },
      {
        colKey: 'status',
        title: '状态',
        width: 105,
        cell: ({ row }) => {
          const status = getCredentialStatus(row);
          return <Tag theme={status.theme} variant="outline">{status.label}</Tag>;
        },
      },
      { colKey: 'expiresAt', title: '过期时间', width: 180, cell: ({ row }) => formatDateTime(row.expiresAt) },
      { colKey: 'lastUsedAt', title: '最后使用', width: 180, cell: ({ row }) => formatDateTime(row.lastUsedAt) },
      { colKey: 'createdAt', title: '创建时间', width: 180, cell: ({ row }) => formatDateTime(row.createdAt) },
      {
        colKey: 'operation',
        title: '操作',
        width: 160,
        fixed: 'right',
        cell: ({ row }) => (
          <Space direction="vertical" size={4} style={{ alignItems: 'flex-start' }}>
            <Button variant="text" theme="primary" icon={<EditIcon />} disabled={Boolean(row.revokedAt)} onClick={() => handleShowEdit(row)}>
              编辑
            </Button>
            <Button variant="text" theme="danger" icon={<DeleteIcon />} disabled={Boolean(row.revokedAt)} onClick={() => setRevokeTarget(row)}>
              吊销
            </Button>
          </Space>
        ),
      },
    ],
    [],
  );

  return (
    <div className="scim-credential-page">
      <Dialog
        visible={Boolean(revokeTarget)}
        header="吊销 SCIM 凭证"
        theme="danger"
        confirmBtn={{ content: '吊销', theme: 'danger' }}
        cancelBtn="取消"
        confirmLoading={revoking}
        onClose={() => {
          if (!revoking) {
            setRevokeTarget(null);
          }
        }}
        onConfirm={() => void handleConfirmRevoke()}
      >
        <div className="scim-credential-confirm-body">
          <p>确定要吊销这个 SCIM 凭证吗？吊销后，使用该 Token 的同步请求将立即失效。</p>
          <strong>{revokeTarget?.name}</strong>
        </div>
      </Dialog>

      <Dialog
        visible={tokenVisible}
        header="SCIM Token 已生成"
        width="560px"
        confirmBtn="关闭"
        cancelBtn={null}
        onClose={handleCloseToken}
        onConfirm={handleCloseToken}
      >
        <div className="scim-credential-token-body">
          <div className="scim-credential-token-body__title">{createdCredentialName}</div>
          <div className="scim-credential-token-body__hint">Token 只会展示一次，请立即复制并安全保存。关闭此窗口后无法再次查看。</div>
          <Textarea readonly value={createdToken} autosize={{ minRows: 3, maxRows: 6 }} />
          <Button theme="primary" variant="outline" icon={<CopyIcon />} onClick={() => void handleCopyToken()}>复制 Token</Button>
        </div>
      </Dialog>

      <Drawer
        visible={formVisible}
        className="scim-credential-drawer"
        header={editingCredential ? '编辑 SCIM 凭证' : '新增 SCIM 凭证'}
        onClose={handleCloseForm}
        footer={false}
        size="min(520px, 100vw)"
        destroyOnClose
      >
        <Form
          className="scim-credential-form"
          key={editingCredential?.id ?? 'new-scim-credential'}
          ref={formRef}
          layout="vertical"
          labelAlign="top"
          colon
          initialData={formData}
        >
          <Form.FormItem label="凭证名称" name="name" rules={[{ required: true, message: '请输入凭证名称', type: 'error' }]}>
            <Input value={formData.name} placeholder="如: Okta Directory Sync" onChange={(value) => setFormData((previous) => ({ ...previous, name: value }))} />
          </Form.FormItem>
          <Form.FormItem label="权限" name="scopes" help="只授予同步方实际需要的权限。">
            <Checkbox.Group
              options={scopeOptions}
              value={formData.scopes}
              onChange={handleScopeChange}
            />
          </Form.FormItem>
          <Form.FormItem label="过期时间" name="expiresAt" help="留空表示不过期。">
            <DatePicker
              clearable
              enableTimePicker
              format="YYYY-MM-DD HH:mm:ss"
              valueType="Date"
              value={toDatePickerValue(formData.expiresAt)}
              placeholder="请选择过期时间"
              onChange={(value) => handleExpiryChange(value as string | number | Date | Array<string | number | Date>)}
            />
          </Form.FormItem>
          {editingCredential && (
            <Form.FormItem label="状态" name="isActive">
              <Switch
                value={formData.isActive}
                label={({ value }) => value ? '启用' : '禁用'}
                onChange={(value) => setFormData((previous) => ({ ...previous, isActive: Boolean(value) }))}
              />
            </Form.FormItem>
          )}
        </Form>
        <div className="scim-credential-drawer__footer">
          <Button variant="base" onClick={handleCloseForm} disabled={submitting}>取消</Button>
          <Button theme="primary" loading={submitting} onClick={() => void handleSubmit()}>{editingCredential ? '保存' : '创建'}</Button>
        </div>
      </Drawer>

      <div className="page-filter-bar">
        <Space>
          <Button theme="primary" icon={<AddIcon />} onClick={handleShowCreate}>新增凭证</Button>
          <Button variant="base" icon={<RefreshIcon />} loading={loading} onClick={() => void fetchData()}>刷新</Button>
        </Space>
      </div>

      <div className="page-table-section">
        <Table
          rowKey="id"
          columns={columns}
          data={credentials}
          verticalAlign="middle"
          tableLayout="fixed"
          resizable
          loading={loading}
          maxHeight={Math.max(window.innerHeight - 200, 260)}
        />
      </div>
    </div>
  );
};

export default ScimCredentialManagementPage;
