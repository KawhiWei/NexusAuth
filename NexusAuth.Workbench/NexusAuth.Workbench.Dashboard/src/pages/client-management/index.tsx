import { useEffect, useMemo, useRef, useState } from 'react';
import type { AxiosError } from 'axios';
import { Button, Card, Drawer, Form, Input, MessagePlugin, Pagination, Select, Space, Switch, Table, Tag, Textarea, Transfer, type TableProps } from 'tdesign-react';
import { getClients, getClient, deleteClient, createClient, updateClient, type Client, type CreateClientRequest, type UpdateClientRequest } from '../../api/client';
import { getAllApiResources, type ApiResource } from '../../api/api-resource';
import { MinusCircleIcon } from 'tdesign-icons-react';

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

type ClientSecretItem = {
  value: string;
  description: string;
};

const getSecretTypeByAuthMethod = (tokenEndpointAuthMethod: string) => {
  return tokenEndpointAuthMethod === 'private_key_jwt' ? 'jwks' : 'shared_secret';
};

const getSecretLabelByAuthMethod = (tokenEndpointAuthMethod: string) => {
  return tokenEndpointAuthMethod === 'private_key_jwt' ? 'JWKS' : 'Client Secret';
};

const getSecretDescriptionByAuthMethod = (tokenEndpointAuthMethod: string) => {
  return tokenEndpointAuthMethod === 'private_key_jwt'
    ? 'private_key_jwt 只支持一条 JWKS 配置，JWKS 内可包含多个 key。'
    : 'client_secret_basic / client_secret_post 支持多条 Client Secret，用于密钥轮换。';
};

const createDefaultSecret = (): ClientSecretItem => ({
  value: '',
  description: '',
});

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
  clientSecrets: ClientSecretItem[];
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
  tokenEndpointAuthMethod: 'client_secret_basic',
  clientSecrets: [createDefaultSecret()],
  isActive: false,
  apiResourceIds: [],
};

const getRequestErrorMessage = (error: unknown, fallback: string) => {
  const axiosError = error as AxiosError<{ title?: string; detail?: string; message?: string }>;
  return axiosError.response?.data?.detail
    || axiosError.response?.data?.message
    || axiosError.response?.data?.title
    || fallback;
};

const ClientManagementPage = () => {
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(false);
  const [sourceData, setSourceData] = useState<Client[]>([]);
  const [total, setTotal] = useState(0);
  const [apiResources, setApiResources] = useState<ApiResource[]>([]);
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);

  const [dialogVisible, setDialogVisible] = useState(false);
  const [editingClient, setEditingClient] = useState<Client | null>(null);
  const [formData, setFormData] = useState<DialogFormData>(defaultFormData);
  const formRef = useRef<any>(null);
  const [submitting, setSubmitting] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);

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

  const handleTokenEndpointAuthMethodChange = (value: string) => {
    setFormData((prev) => {
      return {
        ...prev,
        tokenEndpointAuthMethod: value,
        clientSecrets: value === 'private_key_jwt'
          ? [prev.clientSecrets[0] ?? createDefaultSecret()]
          : (prev.clientSecrets.length > 0 ? prev.clientSecrets : [createDefaultSecret()]),
      };
    });
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

  useEffect(() => {
    fetchData();
  }, [appliedFilters, current, pageSize]);

  useEffect(() => {
    fetchApiResources();
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
      allowedScopes: formData.allowedScopes,
      allowedGrantTypes: formData.allowedGrantTypes,
      requirePkce: formData.requirePkce,
      isActive: formData.isActive,
      clientSecrets: formData.clientSecrets.map((s) => ({
        value: s.value,
        description: s.description,
      })),
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

  const columns: TableProps<Client>['columns'] = useMemo(
    () => [
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
      { colKey: 'tokenEndpointAuthMethod', title: '认证方式', width: 180 },
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
        width: 160,
        cell: ({ row }) => (
          <Space>
            <Button size="small" variant="text" theme="warning" onClick={() => handleEdit(row)}>
              编辑
            </Button>
            <Button size="small" variant="text" theme="danger" onClick={() => handleDelete(row)}>
              删除
            </Button>
          </Space>
        ),
      },
    ],
    []
  );

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
        tokenEndpointAuthMethod: detail.tokenEndpointAuthMethod,
        clientSecrets: detail.clientSecrets?.map((s) => ({ value: s.value, description: s.description ?? '' }))
          ?? [createDefaultSecret()],
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
    const clientSecretsFromForm = form.getFieldValue('clientSecrets') || [];
    const requirePkce = Boolean(form.getFieldValue('requirePkce'));
    const isActive = Boolean(form.getFieldValue('isActive'));
    const secretType = getSecretTypeByAuthMethod(formData.tokenEndpointAuthMethod);
    const clientSecrets = clientSecretsFromForm
      .filter((s: any) => s.value?.trim())
      .map((s: any) => ({ type: secretType, value: s.value, description: s.description || undefined }));

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
          clientSecrets: clientSecrets.length > 0 ? clientSecrets : undefined,
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
          tokenEndpointAuthMethod: formData.tokenEndpointAuthMethod,
          clientSecrets: clientSecrets.length > 0 ? clientSecrets : undefined,
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
          <Form.FormItem label="认证方式" name="tokenEndpointAuthMethod" rules={[{ required: true, message: '请选择认证方式', type: 'error' }]}>
            <Select
              value={formData.tokenEndpointAuthMethod}
              placeholder="请选择"
              options={authMethodOptions}
              onChange={(value) => handleTokenEndpointAuthMethodChange(value as string)}
            />
          </Form.FormItem>
          <Form.FormItem label="允许的 Scope" name="allowedScopes" rules={[{ required: true, message: '请选择 Scope', type: 'error' }]}>
            <Select
              value={formData.allowedScopes}
              multiple
              placeholder="请选择"
              options={scopeOptions}
              onChange={(value) => setFormData((prev) => ({ ...prev, allowedScopes: value as string[] }))}
            />
          </Form.FormItem>
          <Form.FormItem label="允许的 Grant Type" name="allowedGrantTypes" rules={[{ required: true, message: '请选择 Grant Type', type: 'error' }]}>
            <Select
              value={formData.allowedGrantTypes}
              multiple
              placeholder="请选择"
              options={grantTypeOptions}
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

          <Form.FormList name="clientSecrets" initialData={[{ type: '', value: '', description: '' }]}> 
            {(fields, { add, remove }) => (
              <>
                <Form.FormItem label="凭据说明">
                  <div style={{ color: 'var(--td-text-color-secondary)' }}>
                    {getSecretDescriptionByAuthMethod(formData.tokenEndpointAuthMethod)}
                  </div>
                </Form.FormItem>
                {fields.map(({ key, name }) => (
                  <Form.FormItem key={key}>
                    <Form.FormItem name={[name, 'value']} label={getSecretLabelByAuthMethod(formData.tokenEndpointAuthMethod)} rules={[{ required: true, type: 'error' }]}> 
                      {formData.tokenEndpointAuthMethod === 'private_key_jwt' ? (
                        <Textarea
                          autosize={{ minRows: 8, maxRows: 16 }}
                          placeholder='请输入 JWKS JSON，例如：{"keys":[...]}'
                        />
                      ) : (
                        <Input type="password" placeholder="请输入 Client Secret" />
                      )}
                    </Form.FormItem>
                    <Form.FormItem name={[name, 'description']} label="描述">
                      <Input placeholder={formData.tokenEndpointAuthMethod === 'private_key_jwt' ? '例如：生产环境签名公钥集' : '例如：2026 Q2 轮换密钥'} />
                    </Form.FormItem>
                    {formData.tokenEndpointAuthMethod !== 'private_key_jwt' && (
                      <Form.FormItem>
                        <MinusCircleIcon size="20px" style={{ cursor: 'pointer' }} onClick={() => remove(name)} />
                      </Form.FormItem>
                    )}
                  </Form.FormItem>
                ))}
                {formData.tokenEndpointAuthMethod !== 'private_key_jwt' && (
                  <Form.FormItem style={{ marginLeft: 100 }}>
                    <Button theme="default" variant="dashed" onClick={() => add(createDefaultSecret())}>
                      + 新增
                    </Button>
                  </Form.FormItem>
                )}
              </>
            )}
          </Form.FormList>

          {/* 
          <Form.FormItem label="Client Secrets" name="clientSecrets" >
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {formData.clientSecrets.map((secret, index) => (
                <div key={secret.id} style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <Select
                    value={secret.type}
                    style={{ flex: 1, minWidth: 200 }}
                    options={[
                      { label: 'SharedSecret', value: 'shared_secret' },
                      { label: 'JWKS', value: 'jwks' },
                    ]}
                    onChange={(value) => updateClientSecret(secret.id, 'type', value as string)}
                  />
                  <Input
                    value={secret.value}
                    placeholder="请输入 Secret"
                    style={{ flex: 1, minWidth: 350 }}
                    onChange={(value) => updateClientSecret(secret.id, 'value', value)}
                  />
                  <Input
                    value={secret.description}
                    placeholder="描述"
                    style={{ flex: 1, minWidth: 350 }}
                    onChange={(value) => updateClientSecret(secret.id, 'description', value)}
                  />
                  {index === 0 ? (
                    <Button variant="outline" onClick={addClientSecret}>
                      +
                    </Button>
                  ) : (
                    <Button variant="outline" onClick={() => removeClientSecret(secret.id)}>
                      -
                    </Button>
                  )}
                </div>
              ))}
            </div>
          </Form.FormItem> */}


          <Form.FormItem label="回调地址" >
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
