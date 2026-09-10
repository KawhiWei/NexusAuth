import { useEffect, useRef, useState } from 'react';
import type { AxiosError } from 'axios';
import { Button, Card, Dialog, Drawer, Empty, Form, Input, Loading, MessagePlugin, Pagination, Select, Space, Switch, Textarea, Tooltip } from 'tdesign-react';
import { AddIcon, DeleteIcon, EditIcon, ErrorCircleFilledIcon, RefreshIcon, SearchIcon, ViewListIcon } from 'tdesign-icons-react';
import {
  createApiResource,
  deleteApiResource,
  getApiResource,
  getApiResources,
  updateApiResource,
  updateApiResourceStatus,
  type ApiResource,
  type CreateApiResourceRequest,
  type UpdateApiResourceRequest,
} from '../../api/api-resource';
import './style.less';
import '../management-card.less';

type FilterState = {
  keyword: string;
  isActive: '' | boolean;
};

const defaultFilters: FilterState = {
  keyword: '',
  isActive: '',
};

const statusOptions = [
  { label: '全部状态', value: '' },
  { label: '启用', value: true },
  { label: '禁用', value: false },
];

type DialogFormData = {
  name: string;
  displayName: string;
  audience: string;
  description: string;
  isActive: boolean;
};

const defaultFormData: DialogFormData = {
  name: '',
  displayName: '',
  audience: '',
  description: '',
  isActive: true,
};

const getRequestErrorMessage = (error: unknown, fallback: string) => {
  const axiosError = error as AxiosError<{ title?: string; detail?: string; message?: string }>;
  return axiosError.response?.data?.detail
    || axiosError.response?.data?.message
    || axiosError.response?.data?.title
    || fallback;
};

const audienceLabel = (
  <Space size={4} align="center">
    <span>Audience</span>
    <Tooltip content="Audience 用于标识 access token 面向的 API 资源。NexusAuth 会用它来校验请求传入的 scope 是否对应有效资源，并据此解析 token 的 aud。">
      <ErrorCircleFilledIcon style={{ color: 'var(--td-text-color-secondary)', cursor: 'pointer' }} />
    </Tooltip>
  </Space>
);

const ApiResourceManagementPage = () => {
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(false);
  const [sourceData, setSourceData] = useState<ApiResource[]>([]);
  const [total, setTotal] = useState(0);
  const [dialogVisible, setDialogVisible] = useState(false);
  const [editingResource, setEditingResource] = useState<ApiResource | null>(null);
  const [formData, setFormData] = useState<DialogFormData>(defaultFormData);
  const [submitting, setSubmitting] = useState(false);
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ApiResource | null>(null);
  const [deleting, setDeleting] = useState(false);
  const formRef = useRef<any>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [detailMode, setDetailMode] = useState(false);

  const fetchData = async () => {
    try {
      setLoading(true);
      const filter: { keyword?: string; isActive?: boolean; page: number; pageSize: number } = { page: current, pageSize };
      if (appliedFilters.keyword) filter.keyword = appliedFilters.keyword;
      if (appliedFilters.isActive !== '') filter.isActive = appliedFilters.isActive;
      const result = await getApiResources(filter);
      setSourceData(result.items);
      setTotal(result.total);
    } catch (error) {
      console.error('Failed to fetch api resources:', error);
      MessagePlugin.error('加载 API 资源失败');
    } finally {
      setLoading(false);
    }
  };

  const refreshAfterDelete = async () => {
    if (sourceData.length === 1 && current > 1) {
      setCurrent((page) => page - 1);
      return;
    }

    await fetchData();
  };

  useEffect(() => {
    fetchData();
  }, [appliedFilters, current, pageSize]);

  useEffect(() => {
    if (!dialogVisible || loadingDetail) {
      return;
    }

    const form = formRef.current;
    if (!form) {
      return;
    }

    form.setFieldsValue({
      name: formData.name,
      displayName: formData.displayName,
      audience: formData.audience,
      isActive: formData.isActive,
    });
  }, [dialogVisible, loadingDetail, formData]);

  const handleQuery = () => {
    setAppliedFilters(filters);
    setCurrent(1);
  };

  const handleReset = () => {
    setFilters(defaultFilters);
    setAppliedFilters(defaultFilters);
    setCurrent(1);
  };

  const showDialog = () => {
    setEditingResource(null);
    setFormData(defaultFormData);
    setDetailMode(false);
    setDialogVisible(true);
  };

  const loadResourceDetail = async (row: ApiResource, readOnly: boolean) => {
    try {
      setLoadingDetail(true);
      setDetailMode(readOnly);
      const detail = await getApiResource(row.id);
      const nextFormData = {
        name: detail.name,
        displayName: detail.displayName,
        audience: detail.audience,
        description: detail.description ?? '',
        isActive: detail.isActive,
      };

      setEditingResource(detail);
      setFormData(nextFormData);
    } catch (error) {
      console.error('Failed to fetch api resource detail:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '加载 API 资源详情失败'));
    } finally {
      setLoadingDetail(false);
    }
  };

  const handleView = (row: ApiResource) => {
    void loadResourceDetail(row, true);
  };

  const handleEdit = (row: ApiResource) => {
    void loadResourceDetail(row, false);
  };

  useEffect(() => {
    if (!loadingDetail && editingResource) {
      setDialogVisible(true);
    }
  }, [loadingDetail, editingResource]);

  const handleDelete = (row: ApiResource) => {
    setDeleteTarget(row);
  };

  const confirmDelete = async () => {
    if (!deleteTarget) {
      return;
    }

    try {
      setDeleting(true);
      await deleteApiResource(deleteTarget.id);
      setDeleteTarget(null);
      await refreshAfterDelete();
      MessagePlugin.success('删除成功');
    } catch (error) {
      console.error('Failed to delete api resource:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '删除 API 资源失败'));
    } finally {
      setDeleting(false);
    }
  };

  const handleToggleActive = async (row: ApiResource) => {
    try {
      setTogglingId(row.id);
      await updateApiResourceStatus(row.id, !row.isActive);
      await fetchData();
      MessagePlugin.success(row.isActive ? '已禁用 API 资源' : '已启用 API 资源');
    } catch (error) {
      console.error('Failed to toggle api resource status:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '更新 API 资源状态失败'));
    } finally {
      setTogglingId(null);
    }
  };

  const handleCloseDialog = () => {
    setDialogVisible(false);
    setEditingResource(null);
    setFormData(defaultFormData);
    setDetailMode(false);
  };

  const handleSubmit = async () => {
    const form = formRef.current;
    if (!form) {
      return;
    }

    const results = await form.validate();
    if (results.errors && Object.keys(results.errors).length > 0) {
      return;
    }

    const normalizedName = formData.name.trim();
    const normalizedDisplayName = formData.displayName.trim();
    const normalizedAudience = formData.audience.trim();
    const normalizedDescription = formData.description.trim();
    const isActive = Boolean(form.getFieldValue('isActive'));

    const isEditing = Boolean(editingResource);

    try {
      setSubmitting(true);

      if (editingResource) {
        const request: UpdateApiResourceRequest = {
          displayName: normalizedDisplayName,
          audience: normalizedAudience,
          description: normalizedDescription || undefined,
          isActive,
        };
        await updateApiResource(editingResource.id, request);
      } else {
        const request: CreateApiResourceRequest = {
          name: normalizedName,
          displayName: normalizedDisplayName,
          audience: normalizedAudience,
          description: normalizedDescription || undefined,
        };
        await createApiResource(request);
      }
    } catch (error) {
      console.error('Failed to create api resource:', error);
      MessagePlugin.error(getRequestErrorMessage(error, isEditing ? '更新 API 资源失败' : '创建 API 资源失败'));
      setSubmitting(false);
      return;
    }

    setSubmitting(false);
    handleCloseDialog();

    try {
      await fetchData();
      MessagePlugin.success(isEditing ? '更新成功' : '创建成功');
    } catch (error) {
      console.error('Failed to refresh api resources after save:', error);
      MessagePlugin.warning('保存成功，但列表刷新失败，请手动刷新');
    }
  };

  return (
    <div className="api-resource-page management-card-page">
      <Dialog
        visible={Boolean(deleteTarget)}
        header="删除服务资源"
        theme="danger"
        confirmBtn={{ content: '删除', theme: 'danger' }}
        cancelBtn="取消"
        confirmLoading={deleting}
        onClose={() => {
          if (!deleting) {
            setDeleteTarget(null);
          }
        }}
        onConfirm={() => void confirmDelete()}
      >
        <div className="api-resource-confirm-body">
          <p>确定删除这个服务资源吗？</p>
          <div className="api-resource-confirm-name">{deleteTarget?.displayName || deleteTarget?.name}</div>
          <div className="api-resource-confirm-hint">删除后，已关联的客户端授权关系也会被移除。</div>
        </div>
      </Dialog>

      <Drawer
        visible={dialogVisible}
        className="api-resource-drawer"
        header={detailMode ? '服务资源详情' : editingResource ? '编辑服务资源' : '新增服务资源'}
        onClose={handleCloseDialog}
        footer={false}
        size="min(520px, 100vw)"
      >
        <Form
          className="api-resource-form"
          key={editingResource?.id ?? 'new-api-resource'}
          ref={formRef}
          layout="vertical"
          labelAlign="top"
          colon
          initialData={formData}
        >
          <Form.FormItem
            label="服务唯一标识 (Name)"
            name="name"
            help="服务的稳定唯一标识，创建后不可修改；它不是数据库内部 GUID。"
            rules={[{ required: true, message: '请输入服务唯一标识', type: 'error' }]}
          >
            <Input
              value={formData.name}
              placeholder="如: my-api"
              disabled={Boolean(editingResource) || detailMode}
              onChange={(value) => setFormData((prev) => ({ ...prev, name: value }))}
            />
          </Form.FormItem>
          <Form.FormItem
            label="显示名称"
            name="displayName"
            rules={[{ required: true, message: '请输入显示名称', type: 'error' }]}
          >
            <Input
              value={formData.displayName}
              placeholder="如: 我的 API"
              disabled={detailMode}
              onChange={(value) => setFormData((prev) => ({ ...prev, displayName: value }))}
            />
          </Form.FormItem>
          <Form.FormItem
            label={audienceLabel}
            name="audience"
            help="令牌中的 aud 值，用于 API 校验访问令牌面向的资源。"
            rules={[{ required: true, message: '请输入 Audience', type: 'error' }]}
          >
            <Input
              value={formData.audience}
              placeholder="如: my-api"
              disabled={detailMode}
              onChange={(value) => setFormData((prev) => ({ ...prev, audience: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="状态" name="isActive">
            <Switch
              key={`api-resource-active-${editingResource?.id ?? 'new'}-${String(formData.isActive)}`}
              value={Boolean(formData.isActive)}
              label={({ value }) => value ? '启用' : '禁用'}
              disabled={detailMode}
              onChange={(value) => setFormData((prev) => ({ ...prev, isActive: Boolean(value) }))}
            />
          </Form.FormItem>
          <Form.FormItem label="描述" name="description">
            <Textarea
              value={formData.description}
              placeholder="请输入描述"
              disabled={detailMode}
              onChange={(value) => setFormData((prev) => ({ ...prev, description: value }))}
            />
          </Form.FormItem>
        </Form>
        <div className="api-resource-drawer__footer">
          <Button variant="base" onClick={handleCloseDialog} disabled={submitting || loadingDetail}>
            取消
          </Button>
          {detailMode ? (
            <Button theme="primary" icon={<EditIcon />} disabled={loadingDetail} onClick={() => setDetailMode(false)}>编辑</Button>
          ) : (
            <Button theme="primary" loading={submitting} disabled={loadingDetail} onClick={handleSubmit}>
              {editingResource ? '保存' : '创建'}
            </Button>
          )}
        </div>
      </Drawer>

      <div className="management-card-page__toolbar">
        <Form layout="inline" className="api-resource-filter-form">
          <Form.FormItem label="关键词">
            <Input
              clearable
              value={filters.keyword}
              prefixIcon={<SearchIcon />}
              placeholder="名称、显示名称或 Audience"
              style={{ width: 280 }}
              onChange={(value) => setFilters((prev) => ({ ...prev, keyword: value }))}
            />
          </Form.FormItem>
          <Form.FormItem>
            <Select
              value={filters.isActive}
              options={statusOptions}
              style={{ width: 140 }}
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
              <Button theme="primary" icon={<SearchIcon />} onClick={handleQuery}>
                查询
              </Button>
              <Button variant="outline" icon={<RefreshIcon />} onClick={handleReset}>
                重置
              </Button>
              <Button theme="primary" icon={<AddIcon />} onClick={showDialog}>
                新增资源
              </Button>
            </Space>
          </Form.FormItem>
        </Form>
      </div>

      <Loading loading={loading} className="management-card-page__loading">
        {sourceData.length ? (
          <div className="management-card-grid management-card-grid--resources">
            {sourceData.map((resource) => (
              <Card key={resource.id} className="management-card management-card--resource" bordered>
                <div className="management-card__header">
                  <div className="management-card__heading">
                    <span className="management-card__title">{resource.displayName}</span>
                    <code className="management-card__identifier">{resource.name}</code>
                  </div>
                  <Switch value={resource.isActive} loading={togglingId === resource.id} onChange={() => void handleToggleActive(resource)} />
                </div>
                <div className="management-card__section management-card__section--primary">
                  <span>Scope（唯一标识）</span>
                  <code className="management-card__scope">{resource.name}</code>
                </div>
                <div className="management-card__section">
                  <span>Audience</span>
                  <code>{resource.audience}</code>
                </div>
                <div className="management-card__section management-card__section--description">
                  <span>描述</span>
                  <p>{resource.description || '未填写描述'}</p>
                </div>
                <div className="management-card__footer">
                  <Space size="small">
                    <Button variant="text" theme="primary" icon={<ViewListIcon />} onClick={() => handleView(resource)}>详情</Button>
                    <Button variant="text" theme="primary" icon={<EditIcon />} onClick={() => handleEdit(resource)}>编辑</Button>
                    <Button variant="text" theme="danger" icon={<DeleteIcon />} onClick={() => void handleDelete(resource)}>删除</Button>
                  </Space>
                </div>
              </Card>
            ))}
          </div>
        ) : <Empty description="暂无服务资源" />}
      </Loading>

      <div className="management-card-page__pagination">
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
    </div>
  );
};

export default ApiResourceManagementPage;
