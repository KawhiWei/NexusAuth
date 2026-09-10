import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { AxiosError } from 'axios';
import { AddIcon, DeleteIcon, EditIcon, RefreshIcon, SearchIcon } from 'tdesign-icons-react';
import { Button, Card, Empty, Form, Input, Loading, MessagePlugin, Pagination, Select, Space, Tag } from 'tdesign-react';
import { deleteClient, getClients, resetClientCredential, type Client } from '../../api/client';
import { getAllApiResources } from '../../api/api-resource';
import './style.less';
import '../management-card.less';

type FilterState = { keyword: string; isActive: '' | boolean };

const defaultFilters: FilterState = { keyword: '', isActive: true };
const statusOptions = [{ label: '全部状态', value: '' }, { label: '启用', value: true }, { label: '禁用', value: false }];

const getRequestErrorMessage = (error: unknown, fallback: string) => {
  const axiosError = error as AxiosError<{ title?: string; detail?: string; message?: string }>;
  return axiosError.response?.data?.detail || axiosError.response?.data?.message || axiosError.response?.data?.title || fallback;
};

const ClientManagementPage = () => {
  const navigate = useNavigate();
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [sourceData, setSourceData] = useState<Client[]>([]);
  const [total, setTotal] = useState(0);
  const [resourceNamesById, setResourceNamesById] = useState<Record<string, string>>({});

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
      MessagePlugin.error(getRequestErrorMessage(error, '加载客户端失败'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void fetchData(); }, [appliedFilters, current, pageSize]);

  useEffect(() => {
    void getAllApiResources()
      .then((resources) => setResourceNamesById(Object.fromEntries(resources.map((resource) => [resource.id, resource.name]))))
      .catch((error) => console.error('Failed to fetch API resources for client cards:', error));
  }, []);

  const getResettableAuthMethod = (client: Client) => client.tokenEndpointAuthMethod === 'private_key_jwt' ? undefined : client.tokenEndpointAuthMethod;
  const handleQuery = () => { setAppliedFilters(filters); setCurrent(1); };
  const handleReset = () => { setFilters(defaultFilters); setAppliedFilters(defaultFilters); setCurrent(1); };
  const handleAdd = () => navigate('/oauth/client-management/create');
  const handleEdit = (client: Client) => navigate(`/oauth/client-management/edit/${client.id}`);

  const handleDelete = async (client: Client) => {
    if (!window.confirm(`确定要删除客户端 "${client.clientName}" 吗？`)) return;
    try {
      await deleteClient(client.id);
      MessagePlugin.success('删除成功');
      await fetchData();
    } catch (error) {
      console.error('Failed to delete client:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '删除客户端失败'));
    }
  };

  const handleResetCredential = async (client: Client) => {
    const method = getResettableAuthMethod(client);
    if (!method || !window.confirm('Client Secret 只会展示一次，重置后旧 Secret 将失效。确定要继续吗？')) return;
    try {
      setSubmitting(true);
      const result = await resetClientCredential(client.id, { tokenEndpointAuthMethod: method });
      if (result.generatedCredential?.clientSecret) {
        await navigator.clipboard.writeText(result.generatedCredential.clientSecret);
        MessagePlugin.success('Secret 已生成并复制，请立即安全保存');
      } else {
        MessagePlugin.success('凭据已重置');
      }
      await fetchData();
    } catch (error) {
      console.error('Failed to reset client credential:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '重置客户端凭据失败'));
    } finally {
      setSubmitting(false);
    }
  };

  const renderTags = (values?: string[], theme: 'primary' | 'default' = 'default') => values?.length ? (
    <div className="management-card__tags">
      {values.map((value) => <Tag key={value} theme={theme} variant="light-outline" size="small">{value}</Tag>)}
    </div>
  ) : <span className="management-card__empty">未配置</span>;

  return (
    <div className="management-card-page">
      <div className="management-card-page__toolbar">
        <Form layout="inline">
          <Form.FormItem label="关键词"><Input clearable prefixIcon={<SearchIcon />} value={filters.keyword} placeholder="请输入 Client ID 或名称" style={{ width: 280 }} onChange={(value) => setFilters((prev) => ({ ...prev, keyword: value }))} /></Form.FormItem>
          <Form.FormItem label="状态"><Select value={filters.isActive} style={{ width: 140 }} options={statusOptions} onChange={(value) => setFilters((prev) => ({ ...prev, isActive: value === true || value === false ? value : '' }))} /></Form.FormItem>
          <Form.FormItem><Space><Button theme="primary" icon={<SearchIcon />} onClick={handleQuery}>查询</Button><Button variant="outline" icon={<RefreshIcon />} onClick={handleReset}>重置</Button><Button theme="primary" icon={<AddIcon />} onClick={handleAdd}>新增应用</Button></Space></Form.FormItem>
        </Form>
      </div>

      <Loading loading={loading} className="management-card-page__loading">
        {sourceData.length ? <div className="management-card-grid management-card-grid--clients">
          {sourceData.map((client) => <Card key={client.id} className="management-card" bordered>
            <div className="management-card__header"><div className="management-card__heading"><span className="management-card__title">{client.clientName}</span><code className="management-card__identifier">{client.clientId}</code></div><div className="management-card__status"><Tag theme={client.isActive ? 'success' : 'default'} variant="light" size="small">{client.isActive ? '启用' : '禁用'}</Tag><Tag theme={client.requirePkce ? 'success' : 'default'} variant="light-outline" size="small">PKCE {client.requirePkce ? '· S256' : '· 未启用'}</Tag></div></div>
            <div className="management-card__meta-grid"><div><span>认证方式</span><code>{client.tokenEndpointAuthMethod}</code></div><div><span>授权方式</span>{renderTags(client.allowedGrantTypes, 'primary')}</div></div>
            <div className="management-card__section"><span>授权 Scope</span>{renderTags(client.allowedScopes, 'primary')}</div>
            <div className="management-card__section"><span>关联服务资源</span>{renderTags(client.apiResourceIds?.map((resourceId) => resourceNamesById[resourceId] || resourceId))}</div>
            <div className="management-card__section management-card__section--uri"><span>回调地址</span>{client.redirectUris?.length ? <code title={client.redirectUris[0]}>{client.redirectUris[0]}</code> : <span className="management-card__empty">未配置</span>}</div>
            <div className="management-card__footer"><Space size="small"><Button variant="text" theme="primary" icon={<EditIcon />} onClick={() => handleEdit(client)}>编辑</Button>{getResettableAuthMethod(client) && <Button variant="text" theme="primary" loading={submitting} onClick={() => void handleResetCredential(client)}>重置 Secret</Button>}<Button variant="text" theme="danger" icon={<DeleteIcon />} onClick={() => void handleDelete(client)}>删除</Button></Space></div>
          </Card>)}
        </div> : <Empty description="暂无应用" />}
      </Loading>
      <div className="management-card-page__pagination"><Pagination total={total} current={current} pageSize={pageSize} pageSizeOptions={[10, 20, 50]} showPageSize showJumper onCurrentChange={(next) => setCurrent(next)} onPageSizeChange={(size) => { setPageSize(Number(size)); setCurrent(1); }} /></div>
    </div>
  );
};

export default ClientManagementPage;
