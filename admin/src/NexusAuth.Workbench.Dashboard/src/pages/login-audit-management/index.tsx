import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { AxiosError } from 'axios';
import { Button, Form, Input, MessagePlugin, Pagination, Select, Space, Table, Tag, Tooltip, type TableProps } from 'tdesign-react';
import { RefreshIcon, SearchIcon } from 'tdesign-icons-react';
import { getLoginAudits, type LoginAuditLog } from '../../api/login-audit';
import './style.less';

type FilterState = {
  keyword: string;
  isSuccessful: '' | boolean;
  clientId: string;
};

const defaultFilters: FilterState = { keyword: '', isSuccessful: '', clientId: '' };

const resultOptions = [
  { label: '全部结果', value: '' },
  { label: '成功', value: true },
  { label: '失败', value: false },
];

const getRequestErrorMessage = (error: unknown, fallback: string) => {
  const axiosError = error as AxiosError<{ title?: string; detail?: string; message?: string }>;
  return axiosError.response?.data?.detail
    || axiosError.response?.data?.message
    || axiosError.response?.data?.title
    || fallback;
};

const formatDateTime = (value: string) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('zh-CN', { hour12: false });
};

const LoginAuditManagementPage = () => {
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [sourceData, setSourceData] = useState<LoginAuditLog[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const result = await getLoginAudits({
        keyword: appliedFilters.keyword.trim() || undefined,
        isSuccessful: appliedFilters.isSuccessful === '' ? undefined : appliedFilters.isSuccessful,
        clientId: appliedFilters.clientId.trim() || undefined,
        page: current,
        pageSize,
      });
      setSourceData(result.items);
      setTotal(result.total);
    } catch (error) {
      console.error('Failed to fetch login audits:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '加载登录记录失败'));
    } finally {
      setLoading(false);
    }
  }, [appliedFilters, current, pageSize]);

  useEffect(() => { void fetchData(); }, [fetchData]);

  useEffect(() => {
    const updateTableMaxHeight = () => {
      const top = tableWrapRef.current?.getBoundingClientRect().top;
      setTableMaxHeight(top ? Math.max(Math.floor(window.innerHeight - top - 110), 260) : Math.max(window.innerHeight - 200, 260));
    };
    updateTableMaxHeight();
    window.addEventListener('resize', updateTableMaxHeight);
    return () => window.removeEventListener('resize', updateTableMaxHeight);
  }, []);

  const columns: TableProps<LoginAuditLog>['columns'] = useMemo(() => [
    { colKey: 'username', title: '登录账号', width: 180, ellipsis: true },
    {
      colKey: 'isSuccessful', title: '结果', width: 100,
      cell: ({ row }) => row.isSuccessful
        ? <Tag theme="success" variant="light-outline">成功</Tag>
        : <Tag theme="danger" variant="light-outline">失败</Tag>,
    },
    { colKey: 'clientId', title: '来源应用', width: 180, ellipsis: true, cell: ({ row }) => row.clientId || '直接登录' },
    { colKey: 'ipAddress', title: 'IP 地址', width: 150, ellipsis: true, cell: ({ row }) => row.ipAddress || '-' },
    { colKey: 'failureReason', title: '失败原因', width: 170, ellipsis: true, cell: ({ row }) => row.failureReason || '-' },
    {
      colKey: 'userAgent', title: '客户端', minWidth: 260, ellipsis: true,
      cell: ({ row }) => row.userAgent ? <Tooltip content={row.userAgent}>{row.userAgent}</Tooltip> : '-',
    },
    { colKey: 'occurredAt', title: '登录时间', width: 180, cell: ({ row }) => formatDateTime(row.occurredAt) },
  ], []);

  const handleQuery = () => { setAppliedFilters({ ...filters }); setCurrent(1); };
  const handleReset = () => { setFilters(defaultFilters); setAppliedFilters(defaultFilters); setCurrent(1); };

  return (
    <div className="login-audit-management-page">
      <div className="page-filter-bar">
        <Form layout="inline" className="login-audit-management-filter-form">
          <Form.FormItem label="关键词">
            <Input clearable value={filters.keyword} prefixIcon={<SearchIcon />} placeholder="登录账号或 IP" style={{ width: 240 }} onChange={(value) => setFilters((previous) => ({ ...previous, keyword: value }))} />
          </Form.FormItem>
          <Form.FormItem label="结果">
            <Select value={filters.isSuccessful} options={resultOptions} style={{ width: 130 }} onChange={(value) => setFilters((previous) => ({ ...previous, isSuccessful: value === true || value === false ? value : '' }))} />
          </Form.FormItem>
          <Form.FormItem label="来源应用">
            <Input clearable value={filters.clientId} placeholder="Client ID" style={{ width: 180 }} onChange={(value) => setFilters((previous) => ({ ...previous, clientId: value }))} />
          </Form.FormItem>
          <Form.FormItem>
            <Space>
              <Button theme="primary" icon={<SearchIcon />} onClick={handleQuery}>查询</Button>
              <Button variant="base" icon={<RefreshIcon />} onClick={handleReset}>重置</Button>
            </Space>
          </Form.FormItem>
        </Form>
      </div>
      <div className="page-table-section">
        <div ref={tableWrapRef}>
          <Table rowKey="id" columns={columns} data={sourceData} verticalAlign="middle" maxHeight={tableMaxHeight} tableLayout="fixed" resizable loading={loading} />
        </div>
        <div className="login-audit-management-pagination">
          <Pagination total={total} current={current} pageSize={pageSize} pageSizeOptions={[10, 20, 50]} showPageSize showJumper onCurrentChange={setCurrent} onPageSizeChange={(size) => { setPageSize(Number(size)); setCurrent(1); }} />
        </div>
      </div>
    </div>
  );
};

export default LoginAuditManagementPage;
