import { useEffect, useMemo, useRef, useState } from 'react';
import type { AxiosError } from 'axios';
import { Button, Card, DialogPlugin, Drawer, Dropdown, Form, Input, MessagePlugin, Pagination, Select, Space, Switch, Table, Tag, Textarea, Transfer, type TableProps } from 'tdesign-react';
import {
  createClient,
  deleteClient,
  generateClientCredential,
  getClient,
  getClients,
  resetClientCredential,
  updateClient,
  type Client,
  type CreateClientRequest,
  type GeneratedClientCredential,
  type UpdateClientRequest,
} from '../../api/client';
import { getAllApiResources, type ApiResource } from '../../api/api-resource';
import { getClientMetadata, type ClientMetadata, type ClientOption } from '../../api/client-metadata';

type FilterState = {
  keyword: string;
  isActive: '' | boolean;
};

const defaultFilters: FilterState = {
  keyword: '',
  isActive: true,
};

const statusOptions = [
  { label: '全部状态', value: '' },
  { label: '启用', value: true },
  { label: '禁用', value: false },
];

const authMethodOptions = [
  { label: 'client_secret_basic', value: 'client_secret_basic' },
  { label: 'client_secret_post', value: 'client_secret_post' },
  { label: 'client_secret_jwt', value: 'client_secret_jwt' },
  { label: 'private_key_jwt', value: 'private_key_jwt' },
];

const grantTypeOptions = [
  { label: 'authorization_code', value: 'authorization_code' },
  { label: 'client_credentials', value: 'client_credentials' },
  { label: 'refresh_token', value: 'refresh_token' },
];

const scopeOptions = [
  { label: 'openid', value: 'openid' },
  { label: 'profile', value: 'profile' },
  { label: 'email', value: 'email' },
  { label: 'offline_access', value: 'offline_access' },
];

type DialogFormData = {
  clientId: string;
  clientName: string;
  description: string;
  redirectUris: { id: string; value: string }[];
  postLogoutRedirectUris: { id: string; value: string }[];
  allowedScopes: string[];
  allowedGrantTypes: string[];
  requirePkce: boolean;
  tokenEndpointAuthMethods: string[];
  isActive: boolean;
  apiResourceIds: string[];
};

const defaultFormData: DialogFormData = {
  clientId: '',
  clientName: '',
  description: '',
  redirectUris: [{ id: '1', value: '' }],
  postLogoutRedirectUris: [{ id: '1', value: '' }],
  allowedScopes: [],
  allowedGrantTypes: [],
  requirePkce: false,
  tokenEndpointAuthMethods: ['client_secret_basic'],
  isActive: false,
  apiResourceIds: [],
};

const defaultClientMetadata: ClientMetadata = {
  scopes: [],
  grantTypes: [],
  tokenEndpointAuthMethods: [],
};

const getRequestErrorMessage = (error: unknown, fallback: string) => {
  const axiosError = error as AxiosError<{ title?: string; detail?: string; message?: string }>;
  return axiosError.response?.data?.detail
    || axiosError.response?.data?.message
    || axiosError.response?.data?.title
    || fallback;
};

const getCredentialConfirmLabel = (method: string) => method;
const getCredentialTypeLabel = (type: string) => type;

const toSelectOptions = (options: ClientOption[]) => options.map((option) => ({
  label: option.label,
  value: option.value,
}));

const ClientManagementPage = () => {
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(false);
  const [sourceData, setSourceData] = useState<Client[]>([]);
  const [total, setTotal] = useState(0);
  const [apiResources, setApiResources] = useState<ApiResource[]>([]);
  const [clientMetadata, setClientMetadata] = useState<ClientMetadata>(defaultClientMetadata);
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);

  const [dialogVisible, setDialogVisible] = useState(false);
  const [editingClient, setEditingClient] = useState<Client | null>(null);
  const [formData, setFormData] = useState<DialogFormData>(defaultFormData);
  const formRef = useRef<any>(null);
  const [submitting, setSubmitting] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);

  const [expandedRowKeys, setExpandedRowKeys] = useState<Array<string | number>>([]);
  const [credentialDetailLoadingMap, setCredentialDetailLoadingMap] = useState<Record<string, boolean>>({});
  const [credentialDetailCache, setCredentialDetailCache] = useState<Record<string, Client>>({});
  const [credentialDrawerVisible, setCredentialDrawerVisible] = useState(false);
  const [generatedCredential, setGeneratedCredential] = useState<GeneratedClientCredential | null>(null);

  const authMethodSelectOptions = useMemo(
    () => (clientMetadata.tokenEndpointAuthMethods.length > 0 ? toSelectOptions(clientMetadata.tokenEndpointAuthMethods) : authMethodOptions),
    [clientMetadata.tokenEndpointAuthMethods]
  );

  const grantTypeSelectOptions = useMemo(
    () => (clientMetadata.grantTypes.length > 0 ? toSelectOptions(clientMetadata.grantTypes) : grantTypeOptions),
    [clientMetadata.grantTypes]
  );

  const scopeSelectOptions = useMemo(
    () => (clientMetadata.scopes.length > 0 ? toSelectOptions(clientMetadata.scopes) : scopeOptions),
    [clientMetadata.scopes]
  );

  const addRedirectUri = (type: 'redirectUris' | 'postLogoutRedirectUris') => {
    const newId = String(Date.now());
    setFormData((prev) => ({
      ...prev,
      [type]: [...prev[type], { id: newId, value: '' }],
    }));
  };

  const removeRedirectUri = (type: 'redirectUris' | 'postLogoutRedirectUris', id: string) => {
    setFormData((prev) => ({
      ...prev,
      [type]: prev[type].filter((r) => r.id !== id),
    }));
  };

  const updateRedirectUri = (type: 'redirectUris' | 'postLogoutRedirectUris', id: string, value: string) => {
    setFormData((prev) => ({
      ...prev,
      [type]: prev[type].map((r) => (r.id === id ? { ...r, value } : r)),
    }));
  };

  const fetchData = async () => {
    try {
      setLoading(true);
      const filter: { keyword?: string; isActive?: boolean; page: number; pageSize: number } = { page: current, pageSize };
      if (appliedFilters.keyword) filter.keyword = appliedFilters.keyword;
      if (appliedFilters.isActive !== '') filter.isActive = appliedFilters.isActive;
      const result = await getClients(filter);
      setSourceData(result.items);
      setTotal(result.total);
    } catch (error) {
      console.error('Failed to fetch clients:', error);
    } finally {
      setLoading(false);
    }
  };

  const fetchApiResources = async () => {
    try {
      const resources = await getAllApiResources();
      setApiResources(resources);
    } catch (error) {
      console.error('Failed to fetch api resources:', error);
    }
  };

  const fetchClientMetadata = async () => {
    try {
      const metadata = await getClientMetadata();
      setClientMetadata(metadata);
    } catch (error) {
      console.error('Failed to fetch client metadata:', error);
    }
  };

  useEffect(() => {
    fetchData();
  }, [appliedFilters, current, pageSize]);

  useEffect(() => {
    fetchApiResources();
    fetchClientMetadata();
  }, []);

  useEffect(() => {
    if (!dialogVisible || loadingDetail) {
      return;
    }

    const form = formRef.current;
    if (!form) {
      return;
    }

    form.setFieldsValue({
      clientId: formData.clientId,
      clientName: formData.clientName,
      tokenEndpointAuthMethods: formData.tokenEndpointAuthMethods,
      allowedScopes: formData.allowedScopes,
      allowedGrantTypes: formData.allowedGrantTypes,
      requirePkce: formData.requirePkce,
      isActive: formData.isActive,
    });
  }, [dialogVisible, loadingDetail, formData]);

  useEffect(() => {
    const updateTableMaxHeight = () => {
      const baseHeight = Math.max(window.innerHeight - 200, 260);
      if (!tableWrapRef.current) {
        setTableMaxHeight(baseHeight);
        return;
      }
      const top = tableWrapRef.current.getBoundingClientRect().top;
      const next = Math.max(Math.floor(window.innerHeight - top - 110), 260);
      setTableMaxHeight(next);
    };

    updateTableMaxHeight();
    const frame = window.requestAnimationFrame(updateTableMaxHeight);
    window.addEventListener('resize', updateTableMaxHeight);

    return () => {
      window.cancelAnimationFrame(frame);
      window.removeEventListener('resize', updateTableMaxHeight);
    };
  }, []);

  useEffect(() => {
    expandedRowKeys.forEach((key) => {
      const clientId = String(key);
      if (!credentialDetailCache[clientId] && !credentialDetailLoadingMap[clientId]) {
        void loadClientCredentials(clientId);
      }
    });
  }, [expandedRowKeys, credentialDetailCache, credentialDetailLoadingMap]);

  const showGeneratedCredential = (credential?: GeneratedClientCredential) => {
    if (!credential) {
      return;
    }

    setGeneratedCredential(credential);
    setCredentialDrawerVisible(true);
  };

  const copyCredentialValue = async (value?: string) => {
    if (!value) {
      return;
    }

    await navigator.clipboard.writeText(value);
    MessagePlugin.success('已复制');
  };

  const getClientAuthMethods = (client: Client) => {
    const methods = client.tokenEndpointAuthMethods?.length ? client.tokenEndpointAuthMethods : [client.tokenEndpointAuthMethod];
    return methods.filter(Boolean);
  };

  const getResettableAuthMethod = (client: Client) => {
    return getClientAuthMethods(client).find((method) => method !== 'private_key_jwt');
  };

  const loadClientCredentials = async (clientId: string) => {
    if (credentialDetailLoadingMap[clientId]) {
      return;
    }

    setCredentialDetailLoadingMap((prev) => ({ ...prev, [clientId]: true }));
    try {
      const detail = await getClient(clientId);
      setCredentialDetailCache((prev) => ({ ...prev, [clientId]: detail }));
    } catch (error) {
      console.error('Failed to fetch client credentials:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '加载凭据明细失败'));
    } finally {
      setCredentialDetailLoadingMap((prev) => ({ ...prev, [clientId]: false }));
    }
  };

  const submitCredentialMutation = async (
    client: Client,
    tokenEndpointAuthMethod: string,
    mutation: 'generate' | 'reset'
  ) => {
    try {
      setSubmitting(true);
      const result = mutation === 'generate'
        ? await generateClientCredential(client.id, { tokenEndpointAuthMethod })
        : await resetClientCredential(client.id, { tokenEndpointAuthMethod });

      if (editingClient?.id === client.id) {
        setEditingClient(result.client);
      }

      setCredentialDetailCache((prev) => ({ ...prev, [client.id]: result.client }));
      showGeneratedCredential(result.generatedCredential);
      await fetchData();

      if (expandedRowKeys.includes(client.id)) {
        await loadClientCredentials(client.id);
      }
    } catch (error) {
      console.error(`Failed to ${mutation} client credential:`, error);
      MessagePlugin.error(getRequestErrorMessage(error, mutation === 'generate' ? '生成客户端凭据失败' : '重置客户端凭据失败'));
    } finally {
      setSubmitting(false);
    }
  };

  const generateCredentialForMethod = async (client: Client, tokenEndpointAuthMethod?: string) => {
    const method = tokenEndpointAuthMethod?.trim() || getClientAuthMethods(client)[0];
    if (!method) {
      MessagePlugin.error('该客户端没有可用的认证方式');
      return;
    }

    if (method !== 'private_key_jwt') {
      DialogPlugin.confirm({
        header: getCredentialConfirmLabel(method),
        body: `确定要为客户端 "${client.clientName}" 生成 ${method} 凭据吗？新明文只显示一次。`,
        confirmBtn: '生成',
        cancelBtn: '取消',
        onConfirm: () => submitCredentialMutation(client, method, 'generate'),
      });
      return;
    }

    await submitCredentialMutation(client, method, 'generate');
  };

  const handleResetCredential = async (client: Client) => {
    const method = getResettableAuthMethod(client);
    if (!method) {
      MessagePlugin.error('当前客户端没有可重置的共享凭据');
      return;
    }

    DialogPlugin.confirm({
      header: '确认重置凭据',
      body: `确定要为客户端 "${client.clientName}" 重置 ${method} 凭据吗？旧明文将失效，新明文只显示一次。`,
      confirmBtn: '重置',
      cancelBtn: '取消',
      onConfirm: () => submitCredentialMutation(client, method, 'reset'),
    });
  };

  const handleQuery = () => {
    setAppliedFilters(filters);
    setCurrent(1);
  };

  const handleDelete = async (row: Client) => {
    const confirmed = window.confirm(`确定要删除客户端 "${row.clientName}" 吗？`);
    if (confirmed) {
      try {
        await deleteClient(row.id);
        fetchData();
      } catch (error) {
        console.error('Failed to delete client:', error);
        MessagePlugin.error(getRequestErrorMessage(error, '删除客户端失败'));
      }
    }
  };

  const handleEdit = async (row: Client) => {
    try {
      setLoadingDetail(true);
      const detail = await getClient(row.id);
      const nextFormData = {
        clientId: detail.clientId,
        clientName: detail.clientName,
        description: detail.description ?? '',
        redirectUris: detail.redirectUris?.map((uri, i) => ({ id: String(i + 1), value: uri })) ?? [{ id: '1', value: '' }],
        postLogoutRedirectUris: detail.postLogoutRedirectUris?.map((uri, i) => ({ id: String(i + 1), value: uri })) ?? [{ id: '1', value: '' }],
        allowedScopes: detail.allowedScopes ?? ['openid'],
        allowedGrantTypes: detail.allowedGrantTypes ?? ['authorization_code'],
        requirePkce: detail.requirePkce,
        tokenEndpointAuthMethods: getClientAuthMethods(detail),
        isActive: detail.isActive,
        apiResourceIds: detail.apiResourceIds ?? [],
      };

      setEditingClient(detail);
      setFormData(nextFormData);
    } catch (error) {
      console.error('Failed to fetch client detail:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '加载客户端详情失败'));
    } finally {
      setLoadingDetail(false);
    }
  };

  useEffect(() => {
    if (!loadingDetail && editingClient) {
      setDialogVisible(true);
    }
  }, [loadingDetail, editingClient]);

  const handleAdd = () => {
    setEditingClient(null);
    setFormData(defaultFormData);
    setDialogVisible(true);
  };

  const handleClose = () => {
    setDialogVisible(false);
    setEditingClient(null);
    setFormData(defaultFormData);
    const form = formRef.current;
    if (form) {
      form.reset();
    }
  };

  const handleSubmit = async () => {
    const form = formRef.current;
    if (!form) return;

    const results = await form.validate();
    if (results.errors && Object.keys(results.errors).length > 0) {
      return;
    }

    const redirectUris = formData.redirectUris.map((r) => r.value).filter((v) => v.trim());
    const postLogoutRedirectUris = formData.postLogoutRedirectUris.map((r) => r.value).filter((v) => v.trim());
    const requirePkce = Boolean(form.getFieldValue('requirePkce'));
    const isActive = Boolean(form.getFieldValue('isActive'));

    try {
      setSubmitting(true);
      if (editingClient) {
        const request: UpdateClientRequest = {
          clientName: formData.clientName,
          description: formData.description || undefined,
          redirectUris: redirectUris.length > 0 ? redirectUris : undefined,
          postLogoutRedirectUris: postLogoutRedirectUris.length > 0 ? postLogoutRedirectUris : undefined,
          allowedScopes: formData.allowedScopes,
          allowedGrantTypes: formData.allowedGrantTypes,
          requirePkce,
          isActive,
          tokenEndpointAuthMethod: formData.tokenEndpointAuthMethods[0],
          tokenEndpointAuthMethods: formData.tokenEndpointAuthMethods,
          apiResourceIds: formData.apiResourceIds,
        };
        await updateClient(editingClient.id, request);
      } else {
        const request: CreateClientRequest = {
          clientId: formData.clientId,
          clientName: formData.clientName,
          description: formData.description || undefined,
          redirectUris: redirectUris.length > 0 ? redirectUris : undefined,
          postLogoutRedirectUris: postLogoutRedirectUris.length > 0 ? postLogoutRedirectUris : undefined,
          allowedScopes: formData.allowedScopes,
          allowedGrantTypes: formData.allowedGrantTypes,
          requirePkce,
          tokenEndpointAuthMethod: formData.tokenEndpointAuthMethods[0],
          tokenEndpointAuthMethods: formData.tokenEndpointAuthMethods,
          apiResourceIds: formData.apiResourceIds,
        };
        await createClient(request);
      }

      setDialogVisible(false);
      await fetchData();
    } catch (error) {
      console.error('Failed to save client:', error);
      MessagePlugin.error(getRequestErrorMessage(error, editingClient ? '更新客户端失败' : '创建客户端失败'));
    } finally {
      setSubmitting(false);
    }
  };

  const columns: TableProps<Client>['columns'] = [
    { colKey: 'clientId', title: 'Client ID', width: 180 },
    { colKey: 'clientName', title: '名称', minWidth: 150, ellipsis: true },
    {
      colKey: 'isActive',
      title: '状态',
      width: 100,
      cell: ({ row }) => <Tag theme={row.isActive ? 'success' : 'default'}>{row.isActive ? '启用' : '禁用'}</Tag>,
    },
    {
      colKey: 'requirePkce',
      title: 'PKCE',
      width: 80,
      cell: ({ row }) => (row.requirePkce ? '是' : '否'),
    },
    {
      colKey: 'tokenEndpointAuthMethods',
      title: '认证方式',
      minWidth: 220,
      ellipsis: true,
      cell: ({ row }) => getClientAuthMethods(row).join(', ') || '-',
    },
    { colKey: 'redirectUris', title: '回调地址', minWidth: 200, ellipsis: true, cell: ({ row }) => row.redirectUris?.join(', ') || '-' },
    {
      colKey: 'apiResourceIds',
      title: 'API资源',
      width: 120,
      cell: ({ row }) => {
        const count = row.apiResourceIds?.length ?? 0;
        return count > 0 ? <Tag theme="primary">{count} 个</Tag> : '-';
      },
    },
    {
      colKey: 'action',
      title: '操作',
      width: 140,
      cell: ({ row }) => (
        <Space direction="vertical" size={4} style={{ alignItems: 'flex-start' }}>
          <Button size="small" variant="text" theme="warning" onClick={() => handleEdit(row)}>
            编辑
          </Button>
          <Dropdown
            trigger="click"
            hideAfterItemClick
            placement="bottom-right"
            minColumnWidth="170px"
            options={getClientAuthMethods(row).map((method) => ({
              content: method,
              value: method,
            }))}
            onClick={(dropdownItem) => generateCredentialForMethod(row, String(dropdownItem.value))}
          >
            <Button size="small" variant="text" theme="primary">
              生成凭证
            </Button>
          </Dropdown>
          <Button size="small" variant="text" theme="danger" onClick={() => handleDelete(row)}>
            删除
          </Button>
        </Space>
      ),
    },
  ];

  const transferData = useMemo(
    () => apiResources.map((r) => ({ value: r.id, label: r.displayName || r.name })),
    [apiResources]
  );

  return (
    <div>
      <Drawer
        visible={dialogVisible}
        header={editingClient ? '编辑客户端' : '新增客户端'}
        onClose={handleClose}
        footer={false}
        size="85%"
      >
        <Form
          key={editingClient?.id ?? 'new-client'}
          ref={formRef}
          layout="vertical"
          labelAlign="right"
          labelWidth={200}
          colon
          initialData={formData}
        >
          <Form.FormItem label="Client ID" name="clientId" rules={[{ required: true, message: '请输入 Client ID', type: 'error' }]}>
            <Input
              value={formData.clientId}
              disabled={!!editingClient}
              placeholder="请输入 Client ID"
              onChange={(value) => setFormData((prev) => ({ ...prev, clientId: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="名称" name="clientName" rules={[{ required: true, message: '请输入名称', type: 'error' }]}>
            <Input
              value={formData.clientName}
              placeholder="请输入名称"
              onChange={(value) => setFormData((prev) => ({ ...prev, clientName: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="认证方式" name="tokenEndpointAuthMethods" rules={[{ required: true, message: '请选择认证方式', type: 'error' }]}>
            <Select
              value={formData.tokenEndpointAuthMethods}
              multiple
              placeholder="请选择"
              options={authMethodSelectOptions}
              onChange={(value) => setFormData((prev) => ({ ...prev, tokenEndpointAuthMethods: value as string[] }))}
            />
          </Form.FormItem>
          <Form.FormItem label="允许的 Scope" name="allowedScopes" rules={[{ required: true, message: '请选择 Scope', type: 'error' }]}>
            <Select
              value={formData.allowedScopes}
              multiple
              placeholder="请选择"
              options={scopeSelectOptions}
              onChange={(value) => setFormData((prev) => ({ ...prev, allowedScopes: value as string[] }))}
            />
          </Form.FormItem>
          <Form.FormItem label="允许的 Grant Type" name="allowedGrantTypes" rules={[{ required: true, message: '请选择 Grant Type', type: 'error' }]}>
            <Select
              value={formData.allowedGrantTypes}
              multiple
              placeholder="请选择"
              options={grantTypeSelectOptions}
              onChange={(value) => setFormData((prev) => ({ ...prev, allowedGrantTypes: value as string[] }))}
            />
          </Form.FormItem>
          <Form.FormItem label="启用 PKCE" name="requirePkce">
            <Switch
              key={`client-pkce-${editingClient?.id ?? 'new'}-${String(formData.requirePkce)}`}
              value={Boolean(formData.requirePkce)}
              onChange={(value) => setFormData((prev) => ({ ...prev, requirePkce: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="状态" name="isActive">
            <Switch
              key={`client-active-${editingClient?.id ?? 'new'}-${String(formData.isActive)}`}
              value={Boolean(formData.isActive)}
              onChange={(value) => setFormData((prev) => ({ ...prev, isActive: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="回调地址">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {formData.redirectUris.map((uri, index) => (
                <div key={uri.id} style={{ display: 'flex', gap: 8 }}>
                  <Input
                    value={uri.value}
                    placeholder="请输入回调地址"
                    style={{ flex: 1, minWidth: 600 }}
                    onChange={(value) => updateRedirectUri('redirectUris', uri.id, value)}
                  />
                  {index === 0 ? (
                    <Button variant="outline" onClick={() => addRedirectUri('redirectUris')}>
                      +
                    </Button>
                  ) : (
                    <Button variant="outline" onClick={() => removeRedirectUri('redirectUris', uri.id)}>
                      -
                    </Button>
                  )}
                </div>
              ))}
            </div>
          </Form.FormItem>
          <Form.FormItem label="登出回调地址">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {formData.postLogoutRedirectUris.map((uri, index) => (
                <div key={uri.id} style={{ display: 'flex', gap: 8 }}>
                  <Input
                    value={uri.value}
                    placeholder="请输入登出回调地址"
                    style={{ flex: 1, minWidth: 600 }}
                    onChange={(value) => updateRedirectUri('postLogoutRedirectUris', uri.id, value)}
                  />
                  {index === 0 ? (
                    <Button variant="outline" onClick={() => addRedirectUri('postLogoutRedirectUris')}>
                      +
                    </Button>
                  ) : (
                    <Button variant="outline" onClick={() => removeRedirectUri('postLogoutRedirectUris', uri.id)}>
                      -
                    </Button>
                  )}
                </div>
              ))}
            </div>
          </Form.FormItem>
          <Form.FormItem label="描述">
            <Textarea
              value={formData.description}
              placeholder="请输入描述"
              onChange={(value) => setFormData((prev) => ({ ...prev, description: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="关联 API 资源">
            <Transfer
              data={transferData}
              value={formData.apiResourceIds}
              direction="both"
              onChange={(value) => setFormData((prev) => ({ ...prev, apiResourceIds: value as string[] }))}
            />
          </Form.FormItem>
        </Form>
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 24 }}>
          <Button variant="base" onClick={handleClose} disabled={submitting || loadingDetail}>
            取消
          </Button>
          <Button theme="primary" loading={submitting} disabled={loadingDetail} onClick={handleSubmit}>
            {editingClient ? '保存' : '创建'}
          </Button>
        </div>
      </Drawer>

      <Drawer
        visible={credentialDrawerVisible}
        header="一次性客户端凭据"
        onClose={() => setCredentialDrawerVisible(false)}
        footer={false}
        size="640px"
      >
        <Card bordered>
          <div style={{ marginBottom: 16, color: 'var(--td-text-color-secondary)' }}>
            请立即复制并安全保存。关闭后无法再次查看明文，只能重新轮换生成。
            {generatedCredential?.type === 'jwks' ? ' 这是 jwks 登记结果，私钥由 BFF 自行管理。' : ''}
          </div>
          <Form.FormItem label="类型">
            <Input readonly value={generatedCredential?.type ?? '-'} />
          </Form.FormItem>
          {generatedCredential?.clientSecret && (
            <Form.FormItem label="Client Secret">
              <Space direction="vertical" style={{ width: '100%' }}>
                <Input readonly value={generatedCredential.clientSecret} />
                <Button variant="outline" onClick={() => copyCredentialValue(generatedCredential.clientSecret)}>复制 Client Secret</Button>
              </Space>
            </Form.FormItem>
          )}
        </Card>
      </Drawer>

      <Card bordered>
        <Form layout="inline">
          <Form.FormItem label="关键词">
            <Input
              clearable
              value={filters.keyword}
              placeholder="请输入 Client ID 或名称"
              onChange={(value) => setFilters((prev) => ({ ...prev, keyword: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="状态">
            <Select
              value={filters.isActive}
              options={statusOptions}
              onChange={(value) =>
                setFilters((prev) => ({
                  ...prev,
                  isActive: value === true || value === false ? value : '',
                }))
              }
            />
          </Form.FormItem>
          <Form.FormItem>
            <Space>
              <Button theme="primary" onClick={handleQuery}>
                查询
              </Button>
              <Button theme="primary" onClick={handleAdd}>
                新增
              </Button>
            </Space>
          </Form.FormItem>
        </Form>
      </Card>

      <Card bordered style={{ marginTop: 16 }}>
        <div ref={tableWrapRef}>
          <Table
            rowKey="id"
            columns={columns}
            data={sourceData}
            verticalAlign="middle"
            maxHeight={tableMaxHeight}
            tableLayout="fixed"
            loading={loading}
            expandedRowKeys={expandedRowKeys}
            onExpandChange={(keys) => setExpandedRowKeys(keys)}
            expandedRow={({ row }) => {
              const clientId = String(row.id);
              const detail = credentialDetailCache[clientId] ?? row;
              const credentials = detail.credentials ?? [];
              const credentialLoading = credentialDetailLoadingMap[clientId];

              return (
                <div style={{ padding: '8px 0 8px 24px' }}>
                  {credentialLoading ? (
                    <div style={{ color: 'var(--td-text-color-secondary)' }}>正在加载凭据...</div>
                  ) : credentials.length > 0 ? (
                    <Table
                      rowKey="id"
                      data={credentials}
                      columns={[
                        {
                          colKey: 'type',
                          title: 'type',
                          width: 180,
                          cell: ({ row: credential }) => getCredentialTypeLabel(credential.type),
                        },
                        {
                          colKey: 'isActive',
                          title: '状态',
                          width: 100,
                          cell: ({ row: credential }) => (
                            <Tag theme={credential.isActive ? 'success' : 'default'}>
                              {credential.isActive ? '启用' : '禁用'}
                            </Tag>
                          ),
                        },
                        {
                          colKey: 'createdAt',
                          title: '创建时间',
                          minWidth: 180,
                          cell: ({ row: credential }) => new Date(credential.createdAt).toLocaleString(),
                        },
                        {
                          colKey: 'action',
                          title: '操作',
                          width: 120,
                          cell: () => (
                            getResettableAuthMethod(row) ? (
                              <Button size="small" variant="text" theme="warning" onClick={() => handleResetCredential(row)}>
                                reset
                              </Button>
                            ) : '-'
                          ),
                        },
                      ]}
                    />
                  ) : (
                    <div style={{ color: 'var(--td-text-color-secondary)' }}>暂无凭据</div>
                  )}
                </div>
              );
            }}
          />
        </div>

        <div style={{ marginTop: 16, display: 'flex', justifyContent: 'flex-end' }}>
          <Pagination
            total={total}
            current={current}
            pageSize={pageSize}
            pageSizeOptions={[10, 20, 50]}
            showPageSize
            showJumper
            onCurrentChange={(next) => setCurrent(next)}
            onPageSizeChange={(size) => {
              setPageSize(Number(size));
              setCurrent(1);
            }}
          />
        </div>
      </Card>
    </div>
  );
};

export default ClientManagementPage;
