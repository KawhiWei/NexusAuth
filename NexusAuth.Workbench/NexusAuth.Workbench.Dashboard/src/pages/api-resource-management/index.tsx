import { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Card, Drawer, Form, Input, Pagination, Select, Space, Table, Tag, Textarea, type TableProps } from 'tdesign-react';
import { getApiResources, createApiResource, type ApiResource, type CreateApiResourceRequest } from '../../api/api-resource';

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
};

const defaultFormData: DialogFormData = {
  name: '',
  displayName: '',
  audience: '',
  description: '',
};

const ApiResourceManagementPage = () => {
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(false);
  const [sourceData, setSourceData] = useState<ApiResource[]>([]);
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);
  const [dialogVisible, setDialogVisible] = useState(false);
  const [formData, setFormData] = useState<DialogFormData>(defaultFormData);

  const fetchData = async () => {
    try {
      setLoading(true);
      const filter: { keyword?: string; isActive?: boolean } = {};
      if (appliedFilters.keyword) filter.keyword = appliedFilters.keyword;
      if (appliedFilters.isActive !== '') filter.isActive = appliedFilters.isActive;
      const result = await getApiResources(filter);
      setSourceData(result);
    } catch (error) {
      console.error('Failed to fetch api resources:', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, [appliedFilters]);

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

  const showDialog = () => {
    setFormData(defaultFormData);
    setDialogVisible(true);
  };

  const handleSubmit = async () => {
    if (!formData.name || !formData.displayName || !formData.audience) {
      return;
    }

    try {
      const request: CreateApiResourceRequest = {
        name: formData.name,
        displayName: formData.displayName,
        audience: formData.audience,
        description: formData.description || undefined,
      };
      await createApiResource(request);
      setDialogVisible(false);
      fetchData();
    } catch (error) {
      console.error('Failed to create api resource:', error);
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
      { colKey: 'description', title: '描述', minWidth: 200, ellipsis: true },
      { colKey: 'createdAt', title: '创建时间', width: 180 },
    ],
    []
  );

  return (
    <div>
      <Drawer
        visible={dialogVisible}
        header="新增 API 资源"
        onClose={() => setDialogVisible(false)}
        size="520"
      >
        <Form layout="vertical" colon>
          <Form.FormItem label="名称 (Name)">
            <Input
              value={formData.name}
              placeholder="如: my-api"
              onChange={(value) => setFormData((prev) => ({ ...prev, name: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="显示名称">
            <Input
              value={formData.displayName}
              placeholder="如: 我的 API"
              onChange={(value) => setFormData((prev) => ({ ...prev, displayName: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="Audience">
            <Input
              value={formData.audience}
              placeholder="如: my-api"
              onChange={(value) => setFormData((prev) => ({ ...prev, audience: value }))}
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
          <Button variant="base" onClick={() => setDialogVisible(false)}>
            取消
          </Button>
          <Button theme="primary" onClick={handleSubmit}>
            创建
          </Button>
        </div>
      </Drawer>

      <Card bordered>
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
              <Button theme="primary" onClick={showDialog}>
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

export default ApiResourceManagementPage;