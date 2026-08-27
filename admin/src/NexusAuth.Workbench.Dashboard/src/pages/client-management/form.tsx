import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import type { AxiosError } from 'axios';
import { AddIcon, DeleteIcon } from 'tdesign-icons-react';
import { Button, Form, Input, MessagePlugin, Select, Switch, Tag, Textarea, Tooltip } from 'tdesign-react';
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
import { getAllApiResources, type ApiResource } from '../../api/api-resource';
import { getClientMetadata, type ClientMetadata } from '../../api/client-metadata';
import './style.less';

type ClientFormData = {
  clientId: string;
  clientName: string;
  description: string;
  redirectUris: { id: string; value: string }[];
  postLogoutRedirectUris: { id: string; value: string }[];
  allowedScopes: string[];
  allowedGrantTypes: string[];
  apiResourceIds: string[];
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

type FormOption = {
  label: string;
  value: string;
  description?: string;
};

const authMethodOptions: FormOption[] = [
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

const grantTypeOptions: FormOption[] = [
  { label: 'authorization_code', value: 'authorization_code' },
  { label: 'client_credentials', value: 'client_credentials' },
  { label: 'refresh_token', value: 'refresh_token' },
];

const oauthStandardScopeOptions: FormOption[] = [
  { label: 'openid', value: 'openid' },
  { label: 'profile', value: 'profile' },
  { label: 'email', value: 'email' },
  { label: 'phone', value: 'phone' },
  { label: 'address', value: 'address' },
  { label: 'offline_access', value: 'offline_access' },
];

const oauthStandardScopeValues = new Set(oauthStandardScopeOptions.map((scope) => scope.value));

const defaultFormData: ClientFormData = {
  clientId: '',
  clientName: '',
  description: '',
  redirectUris: [{ id: '1', value: '' }],
  postLogoutRedirectUris: [{ id: '1', value: '' }],
  allowedScopes: [],
  allowedGrantTypes: [],
  apiResourceIds: [],
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

const mergeOptions = (...groups: FormOption[][]): FormOption[] => {
  const merged = new Map<string, FormOption>();
  groups.flat().forEach((option) => {
    if (!option.value || merged.has(option.value)) {
      return;
    }
    merged.set(option.value, option);
  });
  return Array.from(merged.values());
};

const formatDateTime = (value?: string) => {
  if (!value) {
    return '-';
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('zh-CN');
};

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
  allowedScopes: (client.allowedScopes ?? ['openid']).filter((scope) => oauthStandardScopeValues.has(scope)),
  allowedGrantTypes: client.allowedGrantTypes ?? ['authorization_code'],
  apiResourceIds: client.apiResourceIds ?? [],
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
  const [apiResources, setApiResources] = useState<ApiResource[]>([]);
  const [editingClient, setEditingClient] = useState<Client | null>(null);
  const [formData, setFormData] = useState<ClientFormData>(defaultFormData);
  const formRef = useRef<any>(null);
  const [submitting, setSubmitting] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [loadingResources, setLoadingResources] = useState(false);
  const [generatedCredential, setGeneratedCredential] = useState<GeneratedClientCredential | null>(null);

  const authMethodSelectOptions = useMemo(
    () => mergeOptions(authMethodOptions, clientMetadata.tokenEndpointAuthMethods, formData.tokenEndpointAuthMethod ? [{ label: formData.tokenEndpointAuthMethod, value: formData.tokenEndpointAuthMethod }] : []),
    [clientMetadata.tokenEndpointAuthMethods, formData.tokenEndpointAuthMethod],
  );

  const grantTypeSelectOptions = useMemo(
    () => mergeOptions(
      grantTypeOptions,
      clientMetadata.grantTypes,
      formData.allowedGrantTypes.map((value) => ({ label: value, value })),
    ),
    [clientMetadata.grantTypes, formData.allowedGrantTypes],
  );

  const scopeSelectOptions = useMemo(
    () => mergeOptions(
      oauthStandardScopeOptions,
      clientMetadata.scopes,
      formData.allowedScopes.map((value) => ({ label: value, value })),
    ),
    [clientMetadata.scopes, formData.allowedScopes],
  );

  const apiResourceSelectOptions = useMemo(() => {
    const resourceOptions = apiResources.map((resource) => ({
      label: resource.displayName ? `${resource.displayName} (${resource.audience})` : resource.audience,
      value: resource.id,
      description: resource.description,
    }));
    const missingOptions = formData.apiResourceIds
      .filter((resourceId) => !apiResources.some((resource) => resource.id === resourceId))
      .map((resourceId) => ({ label: resourceId, value: resourceId }));
    return mergeOptions(resourceOptions, missingOptions);
  }, [apiResources, formData.apiResourceIds]);

  useEffect(() => {
    let active = true;
    setLoadingResources(true);
    Promise.all([
      getClientMetadata().catch((error) => {
        console.error('Failed to fetch client metadata:', error);
        return defaultClientMetadata;
      }),
      getAllApiResources({ isActive: true }).catch((error) => {
        console.error('Failed to fetch active API resources:', error);
        return [];
      }),
    ]).then(([metadata, resources]) => {
      if (!active) {
        return;
      }
      setClientMetadata(metadata);
      setApiResources(resources);
    }).finally(() => {
      if (active) {
        setLoadingResources(false);
      }
    });

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (!isEditMode) {
      setEditingClient(null);
      setFormData(defaultFormData);
      setGeneratedCredential(null);
      return;
    }

    if (!id) {
      MessagePlugin.error('缺少应用 ID');
      navigate('/oauth/client-management', { replace: true });
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
        MessagePlugin.error(getRequestErrorMessage(error, '加载应用详情失败'));
        navigate('/oauth/client-management', { replace: true });
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
      description: formData.description,
      tokenEndpointAuthMethod: formData.tokenEndpointAuthMethod,
      keyMaterialSource: formData.keyMaterialSource,
      jwksInputMode: formData.jwksInputMode,
      jwks: formData.jwks,
      jwksUri: formData.jwksUri,
      allowedScopes: formData.allowedScopes,
      allowedGrantTypes: formData.allowedGrantTypes,
      apiResourceIds: formData.apiResourceIds,
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
    setFormData((prev) => ({
      ...prev,
      [type]: [...prev[type], { id: String(Date.now()), value: '' }],
    }));
  };

  const removeRedirectUri = (type: 'redirectUris' | 'postLogoutRedirectUris', uriId: string) => {
    setFormData((prev) => ({
      ...prev,
      [type]: prev[type].filter((uri) => uri.id !== uriId),
    }));
  };

  const updateRedirectUri = (type: 'redirectUris' | 'postLogoutRedirectUris', uriId: string, value: string) => {
    setFormData((prev) => ({
      ...prev,
      [type]: prev[type].map((uri) => (uri.id === uriId ? { ...uri, value } : uri)),
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
      setFormData(toFormData(result.client));
      setGeneratedCredential(result.generatedCredential ?? null);
      MessagePlugin.success('已生成新凭据');
    } catch (error) {
      console.error('Failed to generate client credential:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '生成应用凭据失败'));
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

    const redirectUris = formData.redirectUris.map((uri) => uri.value.trim()).filter(Boolean);
    const postLogoutRedirectUris = formData.postLogoutRedirectUris.map((uri) => uri.value.trim()).filter(Boolean);
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
          apiResourceIds: formData.apiResourceIds,
          requirePkce,
          isActive,
          tokenEndpointAuthMethod: formData.tokenEndpointAuthMethod,
          jwks,
          jwksUri,
        };
        const updated = await updateClient(editingClient.id, request);
        setEditingClient(updated);
        setFormData(toFormData(updated));
        MessagePlugin.success('应用已保存');
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
        apiResourceIds: formData.apiResourceIds,
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
      MessagePlugin.success('应用已创建');
    } catch (error) {
      console.error('Failed to save client:', error);
      MessagePlugin.error(getRequestErrorMessage(error, isEditMode ? '更新应用失败' : '创建应用失败'));
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
      <div className="client-credential-alert">
        <div>
          <div className="client-credential-alert__title">{title} 已生成</div>
          <div className="client-credential-alert__hint">该凭据只会展示一次。请立即复制并安全保存，离开当前页面后将无法再次查看。</div>
        </div>
        {generatedCredential.clientSecret && (
          <Tag theme="primary" variant="light" size="large" className="client-credential-alert__value">{generatedCredential.clientSecret}</Tag>
        )}
        {generatedCredential.privateKeyPem && (
          <Textarea readonly autosize={{ minRows: 10, maxRows: 16 }} value={generatedCredential.privateKeyPem} />
        )}
        <Button variant="outline" onClick={() => copyCredentialValue(value)}>复制 {title}</Button>
      </div>
    );
  };

  const renderClientOverview = () => {
    if (!editingClient) {
      return null;
    }

    return (
      <div className="client-overview">
        <div className="client-overview__meta">
          <div className="client-overview__title">应用状态</div>
          <div className="client-overview__tags">
            <Tag theme={editingClient.isActive ? 'success' : 'default'}>{editingClient.isActive ? '启用' : '禁用'}</Tag>
            <Tag theme="primary" variant="light-outline">{editingClient.clientId}</Tag>
            <Tag variant="light-outline">{editingClient.tokenEndpointAuthMethod}</Tag>
          </div>
          <div className="client-overview__created">创建时间：{formatDateTime(editingClient.createdAt)}</div>
        </div>
      </div>
    );
  };

  const renderUriRows = (type: 'redirectUris' | 'postLogoutRedirectUris', placeholder: string) => (
    <div className="client-uri-list">
      {formData[type].map((uri, index) => (
        <div className="client-uri-row" key={uri.id}>
          <Input value={uri.value} placeholder={placeholder} onChange={(value) => updateRedirectUri(type, uri.id, value)} />
          <Tooltip content={index === 0 ? '新增地址' : '删除地址'}>
            <Button
              type="button"
              variant="outline"
              shape="square"
              size="small"
              className="client-uri-action"
              aria-label={index === 0 ? '新增地址' : '删除地址'}
              icon={index === 0 ? <AddIcon /> : <DeleteIcon />}
              onClick={() => (index === 0 ? addRedirectUri(type) : removeRedirectUri(type, uri.id))}
            />
          </Tooltip>
        </div>
      ))}
    </div>
  );

  const pageTitle = isEditMode ? '编辑应用' : '新增应用';

  return (
    <div className="client-form-page">
      <div className="client-form-header">
        <div className="client-form-header__content">
          <div className="client-form-header__title">{pageTitle}</div>
          <div className="client-form-header__description">配置 OAuth/OIDC 应用的基础信息、授权类型、回调地址和客户端认证方式。</div>
        </div>
        <div className="client-form-header__actions">
          {isEditMode && editingClient && (
            <Button variant="outline" loading={submitting} onClick={handleGenerateCredential}>生成新凭据</Button>
          )}
          <Button variant="outline" onClick={handleBackToList} disabled={submitting}>返回列表</Button>
        </div>
      </div>

      {renderClientOverview()}
      {renderCredentialCard()}

      {loadingDetail ? (
        <div className="client-form-loading">正在加载应用详情...</div>
      ) : (
        <Form key={editingClient?.id ?? 'new-client'} ref={formRef} layout="vertical" labelAlign="top" initialData={formData}>
          <div className="client-form-grid">
            <section className="client-form-section">
              <div className="client-form-section__heading">基础信息</div>
              <div className="client-form-fields">
                <Form.FormItem label="Client ID" name="clientId" rules={[{ required: true, message: '请输入 Client ID', type: 'error' }]}>
                  <Input value={formData.clientId} disabled={isEditMode} placeholder="请输入 Client ID" onChange={(value) => setFormData((prev) => ({ ...prev, clientId: value }))} />
                </Form.FormItem>
                <Form.FormItem label="应用名称" name="clientName" rules={[{ required: true, message: '请输入应用名称', type: 'error' }]}>
                  <Input value={formData.clientName} placeholder="请输入应用名称" onChange={(value) => setFormData((prev) => ({ ...prev, clientName: value }))} />
                </Form.FormItem>
              </div>
              <Form.FormItem label="描述" name="description">
                <Textarea value={formData.description} placeholder="请输入应用描述" autosize={{ minRows: 3, maxRows: 6 }} onChange={(value) => setFormData((prev) => ({ ...prev, description: value }))} />
              </Form.FormItem>
            </section>

            <section className="client-form-section">
              <div className="client-form-section__heading">授权配置</div>
              <Form.FormItem label="允许的 Scope" name="allowedScopes" rules={[{ required: true, message: '请选择 Scope', type: 'error' }]}>
                <Select value={formData.allowedScopes} multiple placeholder="请选择 Scope" options={scopeSelectOptions} onChange={(value) => setFormData((prev) => ({ ...prev, allowedScopes: value as string[] }))} />
              </Form.FormItem>
              <Form.FormItem label="允许的 Grant Type" name="allowedGrantTypes" rules={[{ required: true, message: '请选择 Grant Type', type: 'error' }]}>
                <Select value={formData.allowedGrantTypes} multiple placeholder="请选择 Grant Type" options={grantTypeSelectOptions} onChange={(value) => setFormData((prev) => ({ ...prev, allowedGrantTypes: value as string[] }))} />
              </Form.FormItem>
              <Form.FormItem label="服务资源" name="apiResourceIds" help="选择应用可以访问的 API 服务资源，资源的 audience 会用于生成访问令牌的 aud。">
                <Select value={formData.apiResourceIds} multiple loading={loadingResources} placeholder="请选择服务资源" options={apiResourceSelectOptions} onChange={(value) => setFormData((prev) => ({ ...prev, apiResourceIds: value as string[] }))} />
              </Form.FormItem>
            </section>

            <section className="client-form-section">
              <div className="client-form-section__heading">回调地址</div>
              <Form.FormItem label="登录回调地址" help="授权完成后，NexusAuth 将把授权响应发送到这些地址。">
                {renderUriRows('redirectUris', '请输入完整的 HTTPS 回调地址')}
              </Form.FormItem>
              <Form.FormItem label="登出回调地址">
                {renderUriRows('postLogoutRedirectUris', '请输入登出后的回调地址')}
              </Form.FormItem>
            </section>

            <section className="client-form-section">
              <div className="client-form-section__heading">客户端认证</div>
              <div className="client-form-fields">
                <Form.FormItem label="认证方式" name="tokenEndpointAuthMethod" rules={[{ required: true, message: '请选择认证方式', type: 'error' }]}>
                  <Select value={formData.tokenEndpointAuthMethod} placeholder="请选择认证方式" options={authMethodSelectOptions} onChange={(value) => handleAuthMethodChange(String(value))} />
                </Form.FormItem>
                <div className="client-switch-group">
                  <Form.FormItem label="启用 PKCE" name="requirePkce">
                    <Switch value={formData.requirePkce} onChange={(value) => setFormData((prev) => ({ ...prev, requirePkce: value }))} />
                  </Form.FormItem>
                  <Form.FormItem label="应用状态" name="isActive">
                    <Switch value={formData.isActive} onChange={(value) => setFormData((prev) => ({ ...prev, isActive: value }))} />
                  </Form.FormItem>
                </div>
              </div>

              {formData.tokenEndpointAuthMethod === 'private_key_jwt' && (
                <div className="client-jwt-material">
                  <div className="client-form-section__subheading">JWT 客户端认证材料</div>
                  <div className="client-form-fields">
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
                      <Form.FormItem label="JWKS" name="jwks">
                        <Textarea value={formData.jwks} placeholder="请输入客户端公钥 JWKS JSON" autosize={{ minRows: 4, maxRows: 10 }} onChange={(value) => setFormData((prev) => ({ ...prev, jwks: value }))} />
                      </Form.FormItem>
                    ) : <div className="client-form-help">自动生成模式下，创建应用时会由后端生成 RSA 密钥对并登记 JWKS，创建成功后会在本页面一次性展示私钥明文。</div>
                  ) : (
                    <Form.FormItem label="JWKS URI" name="jwksUri">
                      <Input value={formData.jwksUri} placeholder="请输入客户端 jwks_uri，例如 https://client.example.com/.well-known/jwks.json" onChange={(value) => setFormData((prev) => ({ ...prev, jwksUri: value }))} />
                    </Form.FormItem>
                  )}
                </div>
              )}
            </section>
          </div>

          <div className="client-form-actions">
            <Button theme="primary" loading={submitting} disabled={loadingDetail} onClick={handleSubmit}>{isEditMode ? '保存' : '创建'}</Button>
          </div>
        </Form>
      )}
    </div>
  );
};

export default ClientFormPage;
