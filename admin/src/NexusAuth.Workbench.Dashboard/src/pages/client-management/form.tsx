import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import type { AxiosError } from 'axios';
import { Button, Form, Input, MessagePlugin, Select, Space, Switch, Tag, Textarea } from 'tdesign-react';
import {
  createClient,
  generateClientCredential,
  getClient,
  updateClient,
  type Client,
  type CreateClientRequest,
  type GeneratedClientCredential,
  type UpdateClientRequest,
} from '../../api/client';
import { getClientMetadata, type ClientMetadata, type ClientOption } from '../../api/client-metadata';

type ClientFormData = {
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

type ClientFormPageProps = {
  mode: 'create' | 'edit';
};

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

const defaultFormData: ClientFormData = {
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
  isActive: true,
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

const toSelectOptions = (options: ClientOption[]) => options.map((option) => ({
  label: option.label,
  value: option.value,
}));

const toFormData = (client: Client): ClientFormData => ({
  clientId: client.clientId,
  clientName: client.clientName,
  description: client.description ?? '',
  redirectUris: client.redirectUris?.length
    ? client.redirectUris.map((uri, index) => ({ id: String(index + 1), value: uri }))
    : [{ id: '1', value: '' }],
  postLogoutRedirectUris: client.postLogoutRedirectUris?.length
    ? client.postLogoutRedirectUris.map((uri, index) => ({ id: String(index + 1), value: uri }))
    : [{ id: '1', value: '' }],
  allowedScopes: client.allowedScopes ?? ['openid'],
  allowedGrantTypes: client.allowedGrantTypes ?? ['authorization_code'],
  requirePkce: client.requirePkce,
  tokenEndpointAuthMethod: client.tokenEndpointAuthMethod,
  keyMaterialSource: client.jwksUri ? 'jwks_uri' : 'jwks',
  jwksInputMode: 'manual',
  jwks: client.jwks ?? '',
  jwksUri: client.jwksUri ?? '',
  isActive: client.isActive,
});

const ClientFormPage = ({ mode }: ClientFormPageProps) => {
  const navigate = useNavigate();
  const { id } = useParams();
  const isEditMode = mode === 'edit';
  const [clientMetadata, setClientMetadata] = useState<ClientMetadata>(defaultClientMetadata);
  const [editingClient, setEditingClient] = useState<Client | null>(null);
  const [formData, setFormData] = useState<ClientFormData>(defaultFormData);
  const formRef = useRef<any>(null);
  const [submitting, setSubmitting] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
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

  useEffect(() => {
    getClientMetadata()
      .then(setClientMetadata)
      .catch((error) => console.error('Failed to fetch client metadata:', error));
  }, []);

  useEffect(() => {
    if (!isEditMode) {
      setEditingClient(null);
      setFormData(defaultFormData);
      setGeneratedCredential(null);
      return;
    }

    if (!id) {
      MessagePlugin.error('缺少客户端 ID');
      navigate('/client-management', { replace: true });
      return;
    }

    const fetchDetail = async () => {
      try {
        setLoadingDetail(true);
        setGeneratedCredential(null);
        const detail = await getClient(id);
        setEditingClient(detail);
        setFormData(toFormData(detail));
      } catch (error) {
        console.error('Failed to fetch client detail:', error);
        MessagePlugin.error(getRequestErrorMessage(error, '加载客户端详情失败'));
        navigate('/client-management', { replace: true });
      } finally {
        setLoadingDetail(false);
      }
    };

    fetchDetail();
  }, [id, isEditMode, navigate]);

  useEffect(() => {
    if (loadingDetail) {
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
  }, [loadingDetail, formData]);

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

  const copyCredentialValue = async (value?: string) => {
    if (!value) {
      return;
    }

    await navigator.clipboard.writeText(value);
    MessagePlugin.success('已复制');
  };

  const handleBackToList = () => {
    navigate('/oauth/client-management');
  };

  const handleGenerateCredential = async () => {
    if (!editingClient) {
      return;
    }

    try {
      setSubmitting(true);
      const result = await generateClientCredential(editingClient.id, {
        tokenEndpointAuthMethod: formData.tokenEndpointAuthMethod,
        autoGenerateJwks: formData.tokenEndpointAuthMethod === 'private_key_jwt',
      });
      setEditingClient(result.client);
      setGeneratedCredential(result.generatedCredential ?? null);
      MessagePlugin.success('已生成新凭据');
    } catch (error) {
      console.error('Failed to generate client credential:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '生成客户端凭据失败'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmit = async () => {
    const form = formRef.current;
    if (!form) return;

    const results = await form.validate();
    if (results.errors && Object.keys(results.errors).length > 0) {
      return;
    }

    const redirectUris = formData.redirectUris.map((r) => r.value.trim()).filter(Boolean);
    const postLogoutRedirectUris = formData.postLogoutRedirectUris.map((r) => r.value.trim()).filter(Boolean);
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

      if (isEditMode && editingClient) {
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
        const updated = await updateClient(editingClient.id, request);
        setEditingClient(updated);
        setFormData(toFormData(updated));
        MessagePlugin.success('客户端已保存');
        return;
      }

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
      setEditingClient(result.client);
      setFormData(toFormData(result.client));
      setGeneratedCredential(result.generatedCredential ?? null);
      navigate(`/oauth/client-management/edit/${result.client.id}`, { replace: true });
      MessagePlugin.success('客户端已创建');
    } catch (error) {
      console.error('Failed to save client:', error);
      MessagePlugin.error(getRequestErrorMessage(error, isEditMode ? '更新客户端失败' : '创建客户端失败'));
    } finally {
      setSubmitting(false);
    }
  };

  const renderCredentialCard = () => {
    if (!generatedCredential) {
      return null;
    }

    const value = generatedCredential.clientSecret || generatedCredential.privateKeyPem;
    const title = generatedCredential.privateKeyPem ? 'Private Key' : 'Client Secret';

    return (
      <div style={{ marginBottom: 16, padding: 16, border: '1px solid var(--td-warning-color)', borderRadius: 'var(--td-radius-medium)' }}>
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          <div>
            <div style={{ fontSize: 16, fontWeight: 600, marginBottom: 6 }}>{title} 已生成</div>
            <div style={{ color: 'var(--td-warning-color)', lineHeight: 1.7 }}>
              该凭据只会展示一次。请立即复制并安全保存，离开当前页面后将无法再次查看。
            </div>
          </div>
          {generatedCredential.clientSecret && (
            <Tag theme="primary" variant="light" size="large" style={{ wordBreak: 'break-all', whiteSpace: 'normal' }}>
              {generatedCredential.clientSecret}
            </Tag>
          )}
          {generatedCredential.privateKeyPem && (
            <Textarea readonly autosize={{ minRows: 10, maxRows: 16 }} value={generatedCredential.privateKeyPem} />
          )}
          <Button variant="outline" onClick={() => copyCredentialValue(value)}>
            复制 {title}
          </Button>
        </Space>
      </div>
    );
  };

  const pageTitle = isEditMode ? '编辑客户端' : '新增客户端';

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Space direction="vertical" size={8} style={{ width: '100%' }}>
          <Space style={{ width: '100%', justifyContent: 'space-between' }}>
            <div>
              <div style={{ fontSize: 20, fontWeight: 600 }}>{pageTitle}</div>
              <div style={{ color: 'var(--td-text-color-secondary)', marginTop: 6 }}>
                按真实 OAuth/OIDC 控制台的页面流配置基础信息、授权类型、回调地址和客户端认证方式。
              </div>
            </div>
            <Button variant="outline" onClick={handleBackToList}>返回列表</Button>
          </Space>
          {editingClient && (
            <Space size={8} style={{ flexWrap: 'wrap' }}>
              <Tag theme={editingClient.isActive ? 'success' : 'default'}>{editingClient.isActive ? '启用' : '禁用'}</Tag>
              <Tag theme="primary" variant="light-outline">{editingClient.clientId}</Tag>
              <Tag variant="light-outline">{editingClient.tokenEndpointAuthMethod}</Tag>
            </Space>
          )}
        </Space>
      </div>

      {renderCredentialCard()}

      <div>
        {loadingDetail ? (
          <div style={{ padding: 48, textAlign: 'center', color: 'var(--td-text-color-secondary)' }}>正在加载客户端详情...</div>
        ) : (
          <>
            <Form key={editingClient?.id ?? 'new-client'} ref={formRef} layout="vertical" labelAlign="right" labelWidth={200} colon initialData={formData}>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '0 24px' }}>
                <Form.FormItem label="Client ID" name="clientId" rules={[{ required: true, message: '请输入 Client ID', type: 'error' }]}>
                  <Input value={formData.clientId} disabled={isEditMode} placeholder="请输入 Client ID" onChange={(value) => setFormData((prev) => ({ ...prev, clientId: value }))} />
                </Form.FormItem>
                <Form.FormItem label="名称" name="clientName" rules={[{ required: true, message: '请输入名称', type: 'error' }]}>
                  <Input value={formData.clientName} placeholder="请输入名称" onChange={(value) => setFormData((prev) => ({ ...prev, clientName: value }))} />
                </Form.FormItem>
                <Form.FormItem label="认证方式" name="tokenEndpointAuthMethod" rules={[{ required: true, message: '请选择认证方式', type: 'error' }]}>
                  <Select value={formData.tokenEndpointAuthMethod} placeholder="请选择" options={authMethodSelectOptions} onChange={(value) => handleAuthMethodChange(String(value))} />
                </Form.FormItem>
                <Form.FormItem label="启用 PKCE" name="requirePkce">
                  <Switch key={`client-pkce-${editingClient?.id ?? 'new'}-${String(formData.requirePkce)}`} value={Boolean(formData.requirePkce)} onChange={(value) => setFormData((prev) => ({ ...prev, requirePkce: value }))} />
                </Form.FormItem>
                <Form.FormItem label="状态" name="isActive">
                  <Switch key={`client-active-${editingClient?.id ?? 'new'}-${String(formData.isActive)}`} value={Boolean(formData.isActive)} onChange={(value) => setFormData((prev) => ({ ...prev, isActive: value }))} />
                </Form.FormItem>
              </div>

              {formData.tokenEndpointAuthMethod === 'private_key_jwt' && (
                <div style={{ marginBottom: 24, padding: 16, borderRadius: 'var(--td-radius-medium)', background: 'var(--td-bg-color-container-hover)' }}>
                  <div style={{ fontWeight: 600, marginBottom: 16 }}>JWT 客户端认证材料</div>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '0 24px' }}>
                    <Form.FormItem label="公钥来源" name="keyMaterialSource">
                      <Select value={formData.keyMaterialSource} options={keyMaterialSourceOptions} onChange={(value) => handleKeyMaterialSourceChange(value as 'jwks' | 'jwks_uri')} />
                    </Form.FormItem>
                    {formData.keyMaterialSource === 'jwks' && (
                      <Form.FormItem label="JWKS 配置方式" name="jwksInputMode">
                        <Select value={formData.jwksInputMode} options={jwksInputModeOptions} onChange={(value) => handleJwksInputModeChange(value as 'auto_generate' | 'manual')} />
                      </Form.FormItem>
                    )}
                  </div>
                  {formData.keyMaterialSource === 'jwks' ? (
                    formData.jwksInputMode === 'manual' ? (
                      <Form.FormItem label="JWKS">
                        <Textarea value={formData.jwks} placeholder="请输入客户端公钥 JWKS JSON" autosize={{ minRows: 4, maxRows: 10 }} onChange={(value) => setFormData((prev) => ({ ...prev, jwks: value }))} />
                      </Form.FormItem>
                    ) : (
                      <div style={{ color: 'var(--td-text-color-secondary)', lineHeight: 1.7 }}>
                        自动生成模式下，创建客户端时会由后端直接生成 RSA 密钥对并登记 JWKS，创建成功后会在本页面一次性展示私钥明文。
                      </div>
                    )
                  ) : (
                    <Form.FormItem label="JWKS URI">
                      <Input value={formData.jwksUri} placeholder="请输入客户端 jwks_uri，例如 https://client.example.com/.well-known/jwks.json" onChange={(value) => setFormData((prev) => ({ ...prev, jwksUri: value }))} />
                    </Form.FormItem>
                  )}
                </div>
              )}

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '0 24px' }}>
                <Form.FormItem label="允许的 Scope" name="allowedScopes" rules={[{ required: true, message: '请选择 Scope', type: 'error' }]}>
                  <Select value={formData.allowedScopes} multiple placeholder="请选择" options={scopeSelectOptions} onChange={(value) => setFormData((prev) => ({ ...prev, allowedScopes: value as string[] }))} />
                </Form.FormItem>
                <Form.FormItem label="允许的 Grant Type" name="allowedGrantTypes" rules={[{ required: true, message: '请选择 Grant Type', type: 'error' }]}>
                  <Select value={formData.allowedGrantTypes} multiple placeholder="请选择" options={grantTypeSelectOptions} onChange={(value) => setFormData((prev) => ({ ...prev, allowedGrantTypes: value as string[] }))} />
                </Form.FormItem>
              </div>

              <Form.FormItem label="回调地址">
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {formData.redirectUris.map((uri, index) => (
                    <div key={uri.id} style={{ display: 'flex', gap: 8 }}>
                      <Input value={uri.value} placeholder="请输入回调地址" style={{ flex: 1 }} onChange={(value) => updateRedirectUri('redirectUris', uri.id, value)} />
                      {index === 0 ? <Button variant="outline" onClick={() => addRedirectUri('redirectUris')}>+</Button> : <Button variant="outline" onClick={() => removeRedirectUri('redirectUris', uri.id)}>-</Button>}
                    </div>
                  ))}
                </div>
              </Form.FormItem>

              <Form.FormItem label="登出回调地址">
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {formData.postLogoutRedirectUris.map((uri, index) => (
                    <div key={uri.id} style={{ display: 'flex', gap: 8 }}>
                      <Input value={uri.value} placeholder="请输入登出回调地址" style={{ flex: 1 }} onChange={(value) => updateRedirectUri('postLogoutRedirectUris', uri.id, value)} />
                      {index === 0 ? <Button variant="outline" onClick={() => addRedirectUri('postLogoutRedirectUris')}>+</Button> : <Button variant="outline" onClick={() => removeRedirectUri('postLogoutRedirectUris', uri.id)}>-</Button>}
                    </div>
                  ))}
                </div>
              </Form.FormItem>

              <Form.FormItem label="描述">
                <Textarea value={formData.description} placeholder="请输入描述" onChange={(value) => setFormData((prev) => ({ ...prev, description: value }))} />
              </Form.FormItem>
            </Form>
            <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, marginTop: 24 }}>
              <Space>
                {isEditMode && editingClient && (
                  <Button variant="outline" loading={submitting} onClick={handleGenerateCredential}>生成新凭据</Button>
                )}
              </Space>
              <Space>
                <Button variant="base" onClick={handleBackToList} disabled={submitting || loadingDetail}>返回列表</Button>
                <Button theme="primary" loading={submitting} disabled={loadingDetail} onClick={handleSubmit}>{isEditMode ? '保存' : '创建'}</Button>
              </Space>
            </div>
          </>
        )}
      </div>
    </div>
  );
};

export default ClientFormPage;
