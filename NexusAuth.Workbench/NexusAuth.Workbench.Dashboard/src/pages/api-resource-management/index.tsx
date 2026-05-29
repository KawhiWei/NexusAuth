import { useEffect, useMemo, useRef, useState } from 'react';
import type { AxiosError } from 'axios';
import { Button, Drawer, Form, Input, MessagePlugin, Pagination, Select, Space, Switch, Table, Tag, Textarea, Tooltip, type TableProps } from 'tdesign-react';
import { ErrorCircleFilledIcon } from 'tdesign-icons-react';
import {
  createApiResource,
  deleteApiResource,
  getApiResource,
  getApiResources,
  updateApiResource,
  type ApiResource,
  type CreateApiResourceRequest,
  type UpdateApiResourceRequest,
} from '../../api/api-resource';

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
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);
  const [dialogVisible, setDialogVisible] = useState(false);
  const [editingResource, setEditingResource] = useState<ApiResource | null>(null);
  const [formData, setFormData] = useState<DialogFormData>(defaultFormData);
  const [submitting, setSubmitting] = useState(false);
  const formRef = useRef<any>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);

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
    setDialogVisible(true);
  };

  const handleEdit = async (row: ApiResource) => {
    try {
      setLoadingDetail(true);
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

  useEffect(() => {
    if (!loadingDetail && editingResource) {
      setDialogVisible(true);
    }
  }, [loadingDetail, editingResource]);

  const handleDelete = async (row: ApiResource) => {
    const confirmed = window.confirm(`确定要删除 API 资源 "${row.displayName || row.name}" 吗？`);
    if (!confirmed) {
      return;
    }

    try {
      await deleteApiResource(row.id);
      MessagePlugin.success('删除成功');
      await fetchData();
    } catch (error) {
      console.error('Failed to delete api resource:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '删除 API 资源失败'));
    }
  };

  const handleToggleActive = async (row: ApiResource) => {
    try {
      const request: UpdateApiResourceRequest = {
        displayName: row.displayName,
        audience: row.audience,
        description: row.description,
        isActive: !row.isActive,
      };
      await updateApiResource(row.id, request);
      MessagePlugin.success(row.isActive ? '已禁用 API 资源' : '已启用 API 资源');
      await fetchData();
    } catch (error) {
      console.error('Failed to toggle api resource status:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '更新 API 资源状态失败'));
    }
  };

  const handleCloseDialog = () => {
    setDialogVisible(false);
    setEditingResource(null);
    setFormData(defaultFormData);
    const form = formRef.current;
    if (form) {
      form.reset();
    }
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
        MessagePlugin.success('更新成功');
      } else {
        const request: CreateApiResourceRequest = {
          name: normalizedName,
          displayName: normalizedDisplayName,
          audience: normalizedAudience,
          description: normalizedDescription || undefined,
        };
        await createApiResource(request);
        MessagePlugin.success('创建成功');
      }

      handleCloseDialog();
      await fetchData();
    } catch (error) {
      console.error('Failed to create api resource:', error);
      MessagePlugin.error(getRequestErrorMessage(error, editingResource ? '更新 API 资源失败' : '创建 API 资源失败'));
    } finally {
      setSubmitting(false);
    }
  };

  const columns: TableProps<ApiResource>['columns'] = useMemo(
    () => [
      { colKey: 'name', title: '名称', width: 180 },
      { colKey: 'displayName', title: '显示名称', minWidth: 150, ellipsis: true },
      { colKey: 'audience', title: 'Audience', width: 180 },
      {
        colKey: 'isActive',
        title: '状态',
        width: 100,
        cell: ({ row }) => <Tag theme={row.isActive ? 'success' : 'default'}>{row.isActive ? '启用' : '禁用'}</Tag>,
      },
      { colKey: 'description', title: '描述', minWidth: 220, ellipsis: true, cell: ({ row }) => row.description || '-' },
      { colKey: 'createdAt', title: '创建时间', width: 180 },
      {
        colKey: 'action',
        title: '操作',
        width: 220,
        cell: ({ row }) => (
          <Space>
            <Switch value={row.isActive} size="small" onChange={() => handleToggleActive(row)} />
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

  return (
    <div>
      <Drawer
        visible={dialogVisible}
        header={editingResource ? '编辑 API 资源' : '新增 API 资源'}
        onClose={handleCloseDialog}
        footer={false}
        size="85%"
      >
        <Form
          key={editingResource?.id ?? 'new-api-resource'}
          ref={formRef}
          layout="vertical"
          labelAlign="right"
          labelWidth={160}
          colon
          initialData={formData}
        >
          <Form.FormItem label="名称 (Name)" name="name" rules={[{ required: true, message: '请输入名称', type: 'error' }]}>
            <Input
              value={formData.name}
              placeholder="如: my-api"
              disabled={Boolean(editingResource)}
              onChange={(value) => setFormData((prev) => ({ ...prev, name: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="显示名称" name="displayName" rules={[{ required: true, message: '请输入显示名称', type: 'error' }]}>
            <Input
              value={formData.displayName}
              placeholder="如: 我的 API"
              onChange={(value) => setFormData((prev) => ({ ...prev, displayName: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label={audienceLabel} name="audience" rules={[{ required: true, message: '请输入 Audience', type: 'error' }]}> 
            <Input
              value={formData.audience}
              placeholder="如: my-api"
              disabled={Boolean(editingResource)}
              onChange={(value) => setFormData((prev) => ({ ...prev, audience: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="状态" name="isActive">
            <Switch
              key={`api-resource-active-${editingResource?.id ?? 'new'}-${String(formData.isActive)}`}
              value={Boolean(formData.isActive)}
              onChange={(value) => setFormData((prev) => ({ ...prev, isActive: value }))}
            />
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
          <Button variant="base" onClick={handleCloseDialog} disabled={submitting || loadingDetail}>
            取消
          </Button>
          <Button theme="primary" loading={submitting} disabled={loadingDetail} onClick={handleSubmit}>
            {editingResource ? '保存' : '创建'}
          </Button>
        </div>
      </Drawer>

      <div className="page-filter-bar">
        <Form layout="inline">
          <Form.FormItem label="关键词">
            <Input
              clearable
              value={filters.keyword}
              placeholder="请输入名称或显示名称"
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
              <Button theme="primary" onClick={handleQuery}>
                查询
              </Button>
              <Button variant="base" onClick={handleReset}>
                重置
              </Button>
              <Button theme="primary" onClick={showDialog}>
                新增
              </Button>
            </Space>
          </Form.FormItem>
        </Form>
      </div>

      <div className="page-table-section">
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
      </div>
    </div>
  );
};

export default ApiResourceManagementPage;
