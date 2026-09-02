import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { AxiosError } from 'axios';
import { Button, DatePicker, Dialog, Drawer, Form, Input, MessagePlugin, Select, Space, Switch, Table, Tag, Textarea, type FormInstanceFunctions, type TableProps } from 'tdesign-react';
import { AddIcon, CopyIcon, DeleteIcon, EditIcon, RefreshIcon } from 'tdesign-icons-react';
import {
  createOpenApiCredential,
  getOpenApiCredentials,
  OPEN_API_CREDENTIAL_TARGET_TYPES,
  revokeOpenApiCredential,
  updateOpenApiCredential,
  type OpenApiCredential,
  type OpenApiCredentialTargetType,
} from '../../api/open-api-credential';
import './style.less';

type CredentialFormData = {
  name: string;
  targetType: OpenApiCredentialTargetType;
  expiresAt: string;
  isActive: boolean;
};

const defaultFormData: CredentialFormData = {
  name: '',
  targetType: 'application',
  expiresAt: '',
  isActive: true,
};

const targetTypeOptions = OPEN_API_CREDENTIAL_TARGET_TYPES.map((value) => ({
  value,
  label: value === 'application' ? '应用管理' : '服务资源',
}));

const targetTypeLabels: Record<OpenApiCredentialTargetType, string> = {
  application: '应用管理',
  service_resource: '服务资源',
};

const scopeLabels: Record<string, string> = {
  'application:read': '应用读取',
  'service_resource:read': '服务资源读取',
};

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

const isExpired = (credential: OpenApiCredential) => {
  return Boolean(credential.expiresAt && new Date(credential.expiresAt).getTime() <= Date.now());
};

const getCredentialStatus = (credential: OpenApiCredential) => {
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

const toFormData = (credential: OpenApiCredential): CredentialFormData => ({
  name: credential.name,
  targetType: credential.targetType,
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

const OpenApiCredentialManagementPage = () => {
  const [credentials, setCredentials] = useState<OpenApiCredential[]>([]);
  const [loading, setLoading] = useState(false);
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);
  const [formVisible, setFormVisible] = useState(false);
  const [editingCredential, setEditingCredential] = useState<OpenApiCredential | null>(null);
  const [formData, setFormData] = useState<CredentialFormData>(defaultFormData);
  const [submitting, setSubmitting] = useState(false);
  const [revokeTarget, setRevokeTarget] = useState<OpenApiCredential | null>(null);
  const [revoking, setRevoking] = useState(false);
  const [tokenVisible, setTokenVisible] = useState(false);
  const [createdToken, setCreatedToken] = useState('');
  const [createdCredentialName, setCreatedCredentialName] = useState('');
  const formRef = useRef<FormInstanceFunctions<CredentialFormData> | null>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      setCredentials(await getOpenApiCredentials());
    } catch (error) {
      console.error('Failed to fetch Open API credentials:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '加载开放 API 凭证失败'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchData();
  }, [fetchData]);

  useEffect(() => {
    const updateTableMaxHeight = () => {
      const top = tableWrapRef.current?.getBoundingClientRect().top;
      setTableMaxHeight(top
        ? Math.max(Math.floor(window.innerHeight - top - 110), 260)
        : Math.max(window.innerHeight - 200, 260));
    };

    updateTableMaxHeight();
    const frame = window.requestAnimationFrame(updateTableMaxHeight);
    window.addEventListener('resize', updateTableMaxHeight);
    return () => {
      window.cancelAnimationFrame(frame);
      window.removeEventListener('resize', updateTableMaxHeight);
    };
  }, []);

  const handleShowCreate = () => {
    setEditingCredential(null);
    setFormData(defaultFormData);
    setFormVisible(true);
  };

  const handleShowEdit = (credential: OpenApiCredential) => {
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
    if (validation !== true && Object.keys(validation).length > 0) {
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
        await updateOpenApiCredential(editingCredential.id, {
          name,
          expiresAt: formData.expiresAt || null,
          isActive: formData.isActive,
        });
        setFormVisible(false);
        setEditingCredential(null);
        setFormData(defaultFormData);
        await fetchData();
        MessagePlugin.success('开放 API 凭证已更新');
      } else {
        const result = await createOpenApiCredential({
          name,
          targetType: formData.targetType,
          expiresAt: formData.expiresAt || null,
        });
        setFormVisible(false);
        setEditingCredential(null);
        setFormData(defaultFormData);
        setCreatedToken(result.token);
        setCreatedCredentialName(result.credential.name);
        setTokenVisible(true);
        await fetchData();
        MessagePlugin.success('开放 API 凭证已创建');
      }
    } catch (error) {
      console.error('Failed to save Open API credential:', error);
      MessagePlugin.error(getRequestErrorMessage(error, editingCredential ? '更新开放 API 凭证失败' : '创建开放 API 凭证失败'));
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
      await revokeOpenApiCredential(revokeTarget.id);
      setRevokeTarget(null);
      await fetchData();
      MessagePlugin.success('开放 API 凭证已吊销');
    } catch (error) {
      console.error('Failed to revoke Open API credential:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '吊销开放 API 凭证失败'));
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
      console.error('Failed to copy Open API token:', error);
      MessagePlugin.error('复制失败，请手动复制');
    }
  };

  const handleCloseToken = () => {
    setTokenVisible(false);
    setCreatedToken('');
    setCreatedCredentialName('');
  };

  const columns: TableProps<OpenApiCredential>['columns'] = useMemo(
    () => [
      {
        colKey: 'name',
        title: '凭证名称',
        minWidth: 190,
        ellipsis: true,
        cell: ({ row }) => <strong className="open-api-credential-name">{row.name}</strong>,
      },
      {
        colKey: 'targetType',
        title: '用途',
        width: 125,
        cell: ({ row }) => <Tag theme="primary" variant="light">{targetTypeLabels[row.targetType]}</Tag>,
      },
      {
        colKey: 'scopes',
        title: '权限',
        minWidth: 190,
        cell: ({ row }) => (
          <Space size={4} breakLine>
            {row.scopes.map((scope) => <Tag key={scope} theme="primary" variant="light-outline">{scopeLabels[scope] || scope}</Tag>)}
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
    <div className="open-api-credential-page">
      <Dialog
        visible={Boolean(revokeTarget)}
        header="吊销开放 API 凭证"
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
        <div className="open-api-credential-confirm-body">
          <p>确定要吊销这个开放 API 凭证吗？吊销后，使用该 Token 的接口请求将立即失效。</p>
          <strong>{revokeTarget?.name}</strong>
        </div>
      </Dialog>

      <Dialog
        visible={tokenVisible}
        header="开放 API Token 已生成"
        width="560px"
        confirmBtn="关闭"
        cancelBtn={null}
        onClose={handleCloseToken}
        onConfirm={handleCloseToken}
      >
        <div className="open-api-credential-token-body">
          <div className="open-api-credential-token-body__title">{createdCredentialName}</div>
          <div className="open-api-credential-token-body__hint">Token 只会展示一次，请立即复制并安全保存。关闭此窗口后无法再次查看。</div>
          <Textarea readonly value={createdToken} autosize={{ minRows: 3, maxRows: 6 }} />
          <Button theme="primary" variant="outline" icon={<CopyIcon />} onClick={() => void handleCopyToken()}>复制 Token</Button>
        </div>
      </Dialog>

      <Drawer
        visible={formVisible}
        className="open-api-credential-drawer"
        header={editingCredential ? '编辑开放 API 凭证' : '新增开放 API 凭证'}
        onClose={handleCloseForm}
        footer={false}
        size="min(520px, 100vw)"
        destroyOnClose
      >
        <Form
          className="open-api-credential-form"
          key={editingCredential?.id ?? 'new-open-api-credential'}
          ref={formRef}
          layout="vertical"
          labelAlign="top"
          colon
          initialData={formData}
        >
          <Form.FormItem label="凭证名称" name="name" rules={[{ required: true, message: '请输入凭证名称', type: 'error' }]}>
            <Input value={formData.name} placeholder="如: Permission Center" onChange={(value) => setFormData((previous) => ({ ...previous, name: value }))} />
          </Form.FormItem>
          <Form.FormItem label="用途" name="targetType" help={editingCredential ? '凭证用途创建后不可修改。' : '创建后用途不可修改，请按调用方需要选择。'} rules={[{ required: true, message: '请选择凭证用途', type: 'error' }]}>
            <Select
              value={formData.targetType}
              options={targetTypeOptions}
              disabled={Boolean(editingCredential)}
              onChange={(value) => {
                if (value === 'application' || value === 'service_resource') {
                  setFormData((previous) => ({ ...previous, targetType: value }));
                }
              }}
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
        <div className="open-api-credential-drawer__footer">
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
        <div ref={tableWrapRef} className="open-api-credential-table-wrap">
          <Table
            rowKey="id"
            columns={columns}
            data={credentials}
            verticalAlign="middle"
            tableLayout="fixed"
            resizable
            loading={loading}
            maxHeight={tableMaxHeight}
          />
        </div>
      </div>
    </div>
  );
};

export default OpenApiCredentialManagementPage;
