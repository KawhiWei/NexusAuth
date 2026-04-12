import { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Card, Drawer, Form, Input, Pagination, Select, Space, Switch, Table, Tag, Textarea, Transfer, type TableProps } from 'tdesign-react';
import { getClients, deleteClient, createClient, updateClient, type Client, type CreateClientRequest, type UpdateClientRequest } from '../../api/client';
import { getApiResources, type ApiResource } from '../../api/api-resource';
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
  type: string;
  value: string;
  description: string;
};

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
  tokenEndpointAuthMethod: '',
  clientSecrets: [{ type: '', value: '', description: '' }],
  isActive: false,
  apiResourceIds: [],
};

const ClientManagementPage = () => {
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(false);
  const [sourceData, setSourceData] = useState<Client[]>([]);
  const [apiResources, setApiResources] = useState<ApiResource[]>([]);
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);

  const [dialogVisible, setDialogVisible] = useState(false);
  const [editingClient, setEditingClient] = useState<Client | null>(null);
  const [formData, setFormData] = useState<DialogFormData>(defaultFormData);
  const formRef = useRef<any>(null);

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
      const filter: { keyword?: string; isActive?: boolean } = {};
      if (appliedFilters.keyword) filter.keyword = appliedFilters.keyword;
      if (appliedFilters.isActive !== '') filter.isActive = appliedFilters.isActive;
      const result = await getClients(filter);
      setSourceData(result);
    } catch (error) {
      console.error('Failed to fetch clients:', error);
    } finally {
      setLoading(false);
    }
  };

  const fetchApiResources = async () => {
    try {
      const resources = await getApiResources();
      setApiResources(resources);
    } catch (error) {
      console.error('Failed to fetch api resources:', error);
    }
  };

  useEffect(() => {
    fetchData();
  }, [appliedFilters]);

  useEffect(() => {
    fetchApiResources();
  }, []);

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
      }
    }
  };

  const handleEdit = (row: Client) => {
    setEditingClient(row);
    setFormData({
      clientId: row.clientId,
      clientName: row.clientName,
      description: row.description ?? '',
      redirectUris: row.redirectUris?.map((uri, i) => ({ id: String(i + 1), value: uri })) ?? [{ id: '1', value: '' }],
      postLogoutRedirectUris: row.postLogoutRedirectUris?.map((uri, i) => ({ id: String(i + 1), value: uri })) ?? [{ id: '1', value: '' }],
      allowedScopes: row.allowedScopes ?? ['openid'],
      allowedGrantTypes: row.allowedGrantTypes ?? ['authorization_code'],
      requirePkce: row.requirePkce,
      tokenEndpointAuthMethod: row.tokenEndpointAuthMethod,
      clientSecrets: row.clientSecrets?.map((s, i) => ({ id: String(i + 1), type: s.type, value: s.value, description: s.description ?? '' })) ?? [{ id: '1', type: 'SharedSecret', value: '', description: '' }],
      isActive: row.isActive,
      apiResourceIds: row.apiResourceIds ?? [],
    });
    setDialogVisible(true);
  };

  const handleAdd = () => {
    setEditingClient(null);
    setFormData(defaultFormData);
    setDialogVisible(true);
  };

  useEffect(() => {
    if (dialogVisible && editingClient) {
      const form = formRef.current;
      if (form && editingClient.clientSecrets) {
        form.setFieldsValue({ 
          clientSecrets: editingClient.clientSecrets.map((s: any) => ({ 
            type: s.type, 
            value: s.value, 
            description: s.description ?? '' 
          })) 
        });
      }
    }
  }, [dialogVisible, editingClient]);

  const handleClose = () => {
    setDialogVisible(false);
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
    const clientSecrets = clientSecretsFromForm
      .filter((s: any) => s.value?.trim())
      .map((s: any) => ({ type: s.type, value: s.value, description: s.description || undefined }));

    try {
      if (editingClient) {
        const request: UpdateClientRequest = {
          clientName: formData.clientName,
          description: formData.description || undefined,
          redirectUris: redirectUris.length > 0 ? redirectUris : undefined,
          postLogoutRedirectUris: postLogoutRedirectUris.length > 0 ? postLogoutRedirectUris : undefined,
          allowedScopes: formData.allowedScopes,
          allowedGrantTypes: formData.allowedGrantTypes,
          requirePkce: formData.requirePkce,
          isActive: formData.isActive,
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
          requirePkce: formData.requirePkce,
          tokenEndpointAuthMethod: formData.tokenEndpointAuthMethod,
          clientSecrets: clientSecrets.length > 0 ? clientSecrets : undefined,
          apiResourceIds: formData.apiResourceIds,
        };
        await createClient(request);
      }

      setDialogVisible(false);
      fetchData();
    } catch (error) {
      console.error('Failed to save client:', error);
    }
  };

  const total = sourceData.length;

  useEffect(() => {
    const maxPage = Math.max(1, Math.ceil(total / pageSize));
    if (current > maxPage) {
      setCurrent(maxPage);
    }
  }, [current, pageSize, total]);

  const pagedData = useMemo(() => {
    const start = (current - 1) * pageSize;
    return sourceData.slice(start, start + pageSize);
  }, [current, sourceData, pageSize]);

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
        <Form ref={formRef} layout="vertical" labelAlign="right" labelWidth={200} colon initialData={defaultFormData}>
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
              onChange={(value) => setFormData((prev) => ({ ...prev, tokenEndpointAuthMethod: value as string }))}
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
          <Form.FormItem label="启用 PKCE">
            <Switch
              value={formData.requirePkce}
              onChange={(value) => setFormData((prev) => ({ ...prev, requirePkce: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="状态">
            <Switch
              value={formData.isActive}
              onChange={(value) => setFormData((prev) => ({ ...prev, isActive: value }))}
            />
          </Form.FormItem>

          <Form.FormList name="clientSecrets" initialData={[{ type: '', value: '', description: '' }]}>
            {(fields, { add, remove }) => (
              <>
                {fields.map(({ key, name }) => (
                  <Form.FormItem key={key}>
                    <Form.FormItem name={[name, 'type']} label="Secret类型" rules={[{ required: true, type: 'error' }]}>
                      <Select
                        style={{ flex: 1, minWidth: 200 }}
                        options={[
                          { label: 'SharedSecret', value: 'shared_secret' },
                          { label: 'JWKS', value: 'jwks' },
                        ]} />
                    </Form.FormItem>
                    <Form.FormItem name={[name, 'value']} label="Secret值" rules={[{ required: true, type: 'error' }]}>
                      <Input />
                    </Form.FormItem>
                    <Form.FormItem name={[name, 'description']} label="描述">
                      <Input />
                    </Form.FormItem>
                    <Form.FormItem>
                      <MinusCircleIcon size="20px" style={{ cursor: 'pointer' }} onClick={() => remove(name)} />
                    </Form.FormItem>
                  </Form.FormItem>
                ))}
                <Form.FormItem style={{ marginLeft: 100 }}>
                  <Button theme="default" variant="dashed" onClick={() => add({ type: 'shared_secret', value: '', description: '' })}>
                    + 新增
                  </Button>
                </Form.FormItem>
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
              checked={formData.apiResourceIds}
              direction="both"
              onChange={(value) => setFormData((prev) => ({ ...prev, apiResourceIds: value as string[] }))}
            />
          </Form.FormItem>
        </Form>
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 24 }}>
          <Button variant="base" onClick={handleClose}>
            取消
          </Button>
          <Button theme="primary" onClick={handleSubmit}>
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
            data={pagedData}
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
            onPageSizeChange={(size) => setPageSize(Number(size))}
          />
        </div>
      </Card>
    </div>
  );
};

export default ClientManagementPage;