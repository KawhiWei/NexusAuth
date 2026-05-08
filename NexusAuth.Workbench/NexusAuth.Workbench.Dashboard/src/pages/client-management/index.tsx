import { useEffect, useMemo, useRef, useState } from 'react';
import type { AxiosError } from 'axios';
import { Button, Card, Dialog, Drawer, Form, Input, MessagePlugin, Pagination, Select, Space, Switch, Table, Tag, Textarea, type TableProps } from 'tdesign-react';
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

const keyMaterialSourceOptions = [
  { label: 'JWKS', value: 'jwks' },
  { label: 'JWKS URI', value: 'jwks_uri' },
];

const jwksInputModeOptions = [
  { label: '自动生成', value: 'auto_generate' },
  { label: '手动录入', value: 'manual' },
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
  tokenEndpointAuthMethod: string;
  keyMaterialSource: 'jwks' | 'jwks_uri';
  jwksInputMode: 'auto_generate' | 'manual';
  jwks: string;
  jwksUri: string;
  isActive: boolean;
};

type CredentialActionDialogState = {
  visible: boolean;
  client: Client | null;
  method: string;
  mutation: 'generate' | 'reset';
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
  tokenEndpointAuthMethod: 'client_secret_basic',
  keyMaterialSource: 'jwks',
  jwksInputMode: 'manual',
  jwks: '',
  jwksUri: '',
  isActive: false,
};

const defaultClientMetadata: ClientMetadata = {
  scopes: [],
  grantTypes: [],
  tokenEndpointAuthMethods: [],
};

const defaultCredentialActionDialogState: CredentialActionDialogState = {
  visible: false,
  client: null,
  method: '',
  mutation: 'generate',
};

const getRequestErrorMessage = (error: unknown, fallback: string) => {
  const axiosError = error as AxiosError<{ title?: string; detail?: string; message?: string }>;
  return axiosError.response?.data?.detail
    || axiosError.response?.data?.message
    || axiosError.response?.data?.title
    || fallback;
};

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
  const [clientMetadata, setClientMetadata] = useState<ClientMetadata>(defaultClientMetadata);
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);

  const [dialogVisible, setDialogVisible] = useState(false);
  const [editingClient, setEditingClient] = useState<Client | null>(null);
  const [formData, setFormData] = useState<DialogFormData>(defaultFormData);
  const formRef = useRef<any>(null);
  const [submitting, setSubmitting] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [credentialResultDialogVisible, setCredentialResultDialogVisible] = useState(false);
  const [generatedCredential, setGeneratedCredential] = useState<GeneratedClientCredential | null>(null);
  const [credentialActionDialog, setCredentialActionDialog] = useState<CredentialActionDialogState>(defaultCredentialActionDialogState);

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
      tokenEndpointAuthMethod: formData.tokenEndpointAuthMethod,
      keyMaterialSource: formData.keyMaterialSource,
      jwksInputMode: formData.jwksInputMode,
      allowedScopes: formData.allowedScopes,
      allowedGrantTypes: formData.allowedGrantTypes,
      requirePkce: formData.requirePkce,
      isActive: formData.isActive,
    });
  }, [dialogVisible, loadingDetail, formData]);

  const handleAuthMethodChange = (value: string) => {
    setFormData((prev) => ({
      ...prev,
      tokenEndpointAuthMethod: value,
      keyMaterialSource: value === 'private_key_jwt' ? prev.keyMaterialSource : 'jwks',
      jwksInputMode: value === 'private_key_jwt' ? prev.jwksInputMode : 'manual',
      jwks: value === 'private_key_jwt' ? prev.jwks : '',
      jwksUri: value === 'private_key_jwt' ? prev.jwksUri : '',
    }));
  };

  const handleKeyMaterialSourceChange = (value: 'jwks' | 'jwks_uri') => {
    setFormData((prev) => ({
      ...prev,
      keyMaterialSource: value,
      jwks: value === 'jwks' ? prev.jwks : '',
      jwksUri: value === 'jwks_uri' ? prev.jwksUri : '',
    }));
  };

  const handleJwksInputModeChange = (value: 'auto_generate' | 'manual') => {
    setFormData((prev) => ({
      ...prev,
      jwksInputMode: value,
      jwks: value === 'manual' ? prev.jwks : '',
    }));
  };

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

  const showGeneratedCredential = (credential?: GeneratedClientCredential) => {
    if (!credential) {
      return;
    }

    setGeneratedCredential(credential);
    setCredentialResultDialogVisible(true);
  };

  const copyCredentialValue = async (value?: string) => {
    if (!value) {
      return;
    }

    await navigator.clipboard.writeText(value);
    MessagePlugin.success('已复制');
  };

  const getResettableAuthMethod = (client: Client) => {
    return client.tokenEndpointAuthMethod === 'private_key_jwt' ? undefined : client.tokenEndpointAuthMethod;
  };

  const submitCredentialMutation = async (
    client: Client,
    tokenEndpointAuthMethod: string,
    mutation: 'generate' | 'reset'
  ) => {
    try {
      setSubmitting(true);
      const result = mutation === 'generate'
        ? await generateClientCredential(client.id, {
          tokenEndpointAuthMethod,
          autoGenerateJwks: tokenEndpointAuthMethod === 'private_key_jwt',
        })
        : await resetClientCredential(client.id, { tokenEndpointAuthMethod });

      if (editingClient?.id === client.id) {
        setEditingClient(result.client);
      }

      showGeneratedCredential(result.generatedCredential);
      await fetchData();
    } catch (error) {
      console.error(`Failed to ${mutation} client credential:`, error);
      MessagePlugin.error(getRequestErrorMessage(error, mutation === 'generate' ? '生成客户端凭据失败' : '重置客户端凭据失败'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleResetCredential = async (client: Client) => {
    const method = getResettableAuthMethod(client);
    if (!method) {
      MessagePlugin.error('当前客户端没有可重置的共享凭据');
      return;
    }

    setCredentialActionDialog({
      visible: true,
      client,
      method,
      mutation: 'reset',
    });
  };

  const handleCloseCredentialActionDialog = () => {
    if (submitting) {
      return;
    }

    setCredentialActionDialog(defaultCredentialActionDialogState);
  };

  const handleConfirmCredentialAction = async () => {
    if (!credentialActionDialog.client || !credentialActionDialog.method) {
      return;
    }

    await submitCredentialMutation(
      credentialActionDialog.client,
      credentialActionDialog.method,
      credentialActionDialog.mutation
    );
    setCredentialActionDialog(defaultCredentialActionDialogState);
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
        const nextFormData: DialogFormData = {
          clientId: detail.clientId,
          clientName: detail.clientName,
          description: detail.description ?? '',
        redirectUris: detail.redirectUris?.map((uri, i) => ({ id: String(i + 1), value: uri })) ?? [{ id: '1', value: '' }],
        postLogoutRedirectUris: detail.postLogoutRedirectUris?.map((uri, i) => ({ id: String(i + 1), value: uri })) ?? [{ id: '1', value: '' }],
          allowedScopes: detail.allowedScopes ?? ['openid'],
          allowedGrantTypes: detail.allowedGrantTypes ?? ['authorization_code'],
          requirePkce: detail.requirePkce,
          tokenEndpointAuthMethod: detail.tokenEndpointAuthMethod,
          keyMaterialSource: detail.jwksUri ? 'jwks_uri' : 'jwks',
          jwksInputMode: 'manual',
          jwks: detail.jwks ?? '',
          jwksUri: detail.jwksUri ?? '',
          isActive: detail.isActive,
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
      const isPrivateKeyJwt = formData.tokenEndpointAuthMethod === 'private_key_jwt';
      const jwks = isPrivateKeyJwt && formData.keyMaterialSource === 'jwks' && formData.jwksInputMode === 'manual'
        ? formData.jwks || undefined
        : undefined;
      const jwksUri = isPrivateKeyJwt && formData.keyMaterialSource === 'jwks_uri'
        ? formData.jwksUri || undefined
        : undefined;

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
          tokenEndpointAuthMethod: formData.tokenEndpointAuthMethod,
          jwks,
          jwksUri,
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
          tokenEndpointAuthMethod: formData.tokenEndpointAuthMethod,
          autoGenerateJwks: isPrivateKeyJwt && formData.keyMaterialSource === 'jwks' && formData.jwksInputMode === 'auto_generate',
          jwks,
          jwksUri,
        };
        const result = await createClient(request);
        showGeneratedCredential(result.generatedCredential);
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

  const renderArrayTags = (
    values?: string[],
    theme: 'default' | 'primary' | 'success' | 'warning' | 'danger' = 'default'
  ) => {
    if (!values?.length) {
      return '-';
    }

    return (
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
        {values.map((value) => (
          <Tag key={value} theme={theme} variant="light-outline">
            {value}
          </Tag>
        ))}
      </div>
    );
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
      colKey: 'tokenEndpointAuthMethod',
      title: '认证方式',
      minWidth: 180,
      cell: ({ row }) => <Tag theme="primary" variant="light-outline">{row.tokenEndpointAuthMethod}</Tag>,
    },
    {
      colKey: 'redirectUris',
      title: '回调地址',
      minWidth: 260,
      cell: ({ row }) => renderArrayTags(row.redirectUris),
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
          {getResettableAuthMethod(row) && (
            <Button size="small" variant="text" theme="primary" onClick={() => handleResetCredential(row)}>
              重置 Secret
            </Button>
          )}
          <Button size="small" variant="text" theme="danger" onClick={() => handleDelete(row)}>
            删除
          </Button>
        </Space>
      ),
    },
  ];

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
          <Form.FormItem label="认证方式" name="tokenEndpointAuthMethod" rules={[{ required: true, message: '请选择认证方式', type: 'error' }]}>
            <Select
              value={formData.tokenEndpointAuthMethod}
              placeholder="请选择"
              options={authMethodSelectOptions}
              onChange={(value) => handleAuthMethodChange(String(value))}
            />
          </Form.FormItem>
          {formData.tokenEndpointAuthMethod === 'private_key_jwt' && (
            <>
              <Form.FormItem label="公钥来源" name="keyMaterialSource">
                <Select
                  value={formData.keyMaterialSource}
                  options={keyMaterialSourceOptions}
                  onChange={(value) => handleKeyMaterialSourceChange(value as 'jwks' | 'jwks_uri')}
                />
              </Form.FormItem>

              {formData.keyMaterialSource === 'jwks' ? (
                <>
                  <Form.FormItem label="JWKS 配置方式" name="jwksInputMode">
                    <Select
                      value={formData.jwksInputMode}
                      options={jwksInputModeOptions}
                      onChange={(value) => handleJwksInputModeChange(value as 'auto_generate' | 'manual')}
                    />
                  </Form.FormItem>

                  {formData.jwksInputMode === 'manual' ? (
                    <Form.FormItem label="JWKS">
                      <Textarea
                        value={formData.jwks}
                        placeholder="请输入客户端公钥 JWKS JSON"
                        autosize={{ minRows: 4, maxRows: 10 }}
                        onChange={(value) => setFormData((prev) => ({ ...prev, jwks: value }))}
                      />
                    </Form.FormItem>
                  ) : (
                    <Form.FormItem label="自动生成说明">
                      <div style={{ color: 'var(--td-text-color-secondary)', lineHeight: 1.7 }}>
                        自动生成模式下，创建客户端时会由后端直接生成 RSA 密钥对并登记 JWKS，创建成功后会一次性展示私钥明文。
                      </div>
                    </Form.FormItem>
                  )}
                </>
              ) : (
                <Form.FormItem label="JWKS URI">
                  <Input
                    value={formData.jwksUri}
                    placeholder="请输入客户端 jwks_uri，例如 https://client.example.com/.well-known/jwks.json"
                    onChange={(value) => setFormData((prev) => ({ ...prev, jwksUri: value }))}
                  />
                </Form.FormItem>
              )}
            </>
          )}
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

      <Dialog
        visible={credentialActionDialog.visible}
        header={"Client Secret 生成提示"}
        body={credentialActionDialog.client ? (` 此类型的 client secret 只会展示一次，后续如需再次查看，只能通过重置重新生成并显示。`
        ) : ''}
        confirmBtn={credentialActionDialog.mutation === 'generate' ? '生成' : '重置'}
        cancelBtn="取消"
        confirmLoading={submitting}
        onClose={handleCloseCredentialActionDialog}
        onCancel={handleCloseCredentialActionDialog}
        onConfirm={handleConfirmCredentialAction}
      />

      <Dialog
        visible={credentialResultDialogVisible}
        header={generatedCredential?.privateKeyPem ? 'Private Key' : 'Client Secret'}
        confirmBtn="关闭"
        cancelBtn={null}
        onClose={() => setCredentialResultDialogVisible(false)}
        onConfirm={() => setCredentialResultDialogVisible(false)}
        body={(
          <div>
            {generatedCredential?.clientSecret && (
              <Space direction="vertical" size={16} style={{ width: '100%' }}>
                <Tag theme="primary" variant="light" size="large">
                  {generatedCredential.clientSecret}
                </Tag>
                <Button variant="outline" onClick={() => copyCredentialValue(generatedCredential.clientSecret)}>复制 Client Secret</Button>
              </Space>
            )}
            {generatedCredential?.privateKeyPem && (
              <Space direction="vertical" size={16} style={{ width: '100%' }}>
                <div style={{ color: 'var(--td-text-color-secondary)', lineHeight: 1.7 }}>
                  请立即复制并安全保存私钥。关闭后无法再次查看，后续如需重新获取，请重新生成新的密钥对。
                </div>
                <Textarea readonly autosize={{ minRows: 10, maxRows: 16 }} value={generatedCredential.privateKeyPem} />
                <Button variant="outline" onClick={() => copyCredentialValue(generatedCredential.privateKeyPem)}>复制 Private Key</Button>
              </Space>
            )}
          </div>
        )}
      />

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
