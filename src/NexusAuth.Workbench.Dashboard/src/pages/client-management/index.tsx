import { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Card, DialogPlugin, Form, Input, Pagination, Select, Space, Table, Tag, type TableProps } from 'tdesign-react';
import { getClients, deleteClient, type Client } from '../../api/client';

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

const ClientManagementPage = () => {
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(false);
  const [sourceData, setSourceData] = useState<Client[]>([]);
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);

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

  const columns: TableProps<Client>['columns'] = useMemo(
    () => [
      { colKey: 'clientId', title: 'Client ID', width: 180 },
      { colKey: 'clientName', title: '名称', minWidth: 150, ellipsis: true },
      {
        colKey: 'isActive',
        title: '状态',
        width: 100,
        cell: ({ row }) => <Tag theme={row.isActive ? 'success' : 'default'}>{row.isActive ? '启用' : '禁用'}</Tag>
      },
      {
        colKey: 'requirePkce',
        title: 'PKCE',
        width: 80,
        cell: ({ row }) => (row.requirePkce ? '是' : '否')
      },
      { colKey: 'tokenEndpointAuthMethod', title: '认证方式', width: 180 },
      { colKey: 'redirectUris', title: '回调地址', minWidth: 200, ellipsis: true, cell: ({ row }) => row.redirectUris?.join(', ') || '-' },
      {
        colKey: 'action',
        title: '操作',
        width: 120,
        cell: ({ row }) => (
          <Space>
            <Button size="small" variant="text" onClick={() => handleDelete(row)}>
              删除
            </Button>
          </Space>
        )
      }
    ],
    []
  );

  const handleQuery = () => {
    setAppliedFilters(filters);
    setCurrent(1);
  };

  const handleReset = () => {
    setFilters(defaultFilters);
    setAppliedFilters(defaultFilters);
    setCurrent(1);
  };

  const handleDelete = async (row: Client) => {
    const confirmed = await DialogPlugin.confirm({
      header: '确认删除',
      body: `确定要删除客户端 "${row.clientName}" 吗？`,
      confirmBtn: '确定',
      cancelBtn: '取消'
    });

    if (confirmed) {
      try {
        await deleteClient(row.id);
        fetchData();
      } catch (error) {
        console.error('Failed to delete client:', error);
      }
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

  return (
    <div>
      <Card bordered>
        <Form layout="inline">
          <Form.FormItem label="关键词">
            <Input
              clearable
              value={filters.keyword}
              placeholder="请输入 Client ID 或名称"
              style={{ width: 260 }}
              onChange={(value) => setFilters((prev) => ({ ...prev, keyword: value }))}
            />
          </Form.FormItem>

          <Form.FormItem label="状态">
            <Select
              value={filters.isActive}
              style={{ width: 140 }}
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
              <Button variant="base" onClick={handleReset}>
                重置
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

export default ClientManagementPage;