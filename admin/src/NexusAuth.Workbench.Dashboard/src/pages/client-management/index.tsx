import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { AxiosError } from 'axios';
import { Button, Form, Input, MessagePlugin, Pagination, Select, Space, Table, Tag, type TableProps } from 'tdesign-react';
import {
  deleteClient,
  getClients,
  resetClientCredential,
  type Client,
} from '../../api/client';

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

const getRequestErrorMessage = (error: unknown, fallback: string) => {
  const axiosError = error as AxiosError<{ title?: string; detail?: string; message?: string }>;
  return axiosError.response?.data?.detail
    || axiosError.response?.data?.message
    || axiosError.response?.data?.title
    || fallback;
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
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);

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

  useEffect(() => {
    fetchData();
  }, [appliedFilters, current, pageSize]);

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

  const getResettableAuthMethod = (client: Client) => {
    return client.tokenEndpointAuthMethod === 'private_key_jwt' ? undefined : client.tokenEndpointAuthMethod;
  };

  const handleQuery = () => {
    setAppliedFilters(filters);
    setCurrent(1);
  };

  const handleReset = () => {
    setFilters(defaultFilters);
    setAppliedFilters(defaultFilters);
    setCurrent(1);
  };

  const handleAdd = () => {
    navigate('/oauth/client-management/create');
  };

  const handleEdit = (row: Client) => {
    navigate(`/oauth/client-management/edit/${row.id}`);
  };

  const handleDelete = async (row: Client) => {
    const confirmed = window.confirm(`确定要删除客户端 "${row.clientName}" 吗？`);
    if (!confirmed) {
      return;
    }

    try {
      await deleteClient(row.id);
      MessagePlugin.success('删除成功');
      await fetchData();
    } catch (error) {
      console.error('Failed to delete client:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '删除客户端失败'));
    }
  };

  const handleResetCredential = async (client: Client) => {
    const method = getResettableAuthMethod(client);
    if (!method) {
      MessagePlugin.error('当前客户端没有可重置的共享凭据');
      return;
    }

    const confirmed = window.confirm('Client Secret 只会展示一次，重置后旧 Secret 将失效。确定要继续吗？');
    if (!confirmed) {
      return;
    }

    try {
      setSubmitting(true);
      const result = await resetClientCredential(client.id, { tokenEndpointAuthMethod: method });
      const secret = result.generatedCredential?.clientSecret;
      if (secret) {
        await navigator.clipboard.writeText(secret);
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

  const renderArrayTags = (values?: string[]) => {
    if (!values?.length) {
      return '-';
    }

    return (
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
        {values.map((value) => (
          <Tag key={value} variant="light-outline">
            {value}
          </Tag>
        ))}
      </div>
    );
  };

  const columns: TableProps<Client>['columns'] = [
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
    {
      colKey: 'tokenEndpointAuthMethod',
      title: '认证方式',
      minWidth: 180,
      cell: ({ row }) => <Tag theme="primary" variant="light-outline">{row.tokenEndpointAuthMethod}</Tag>,
    },
    {
      colKey: 'redirectUris',
      title: '回调地址',
      minWidth: 260,
      cell: ({ row }) => renderArrayTags(row.redirectUris),
    },
    {
      colKey: 'action',
      title: '操作',
      width: 160,
      fixed: 'right',
      cell: ({ row }) => (
        <Space direction="vertical" size={4} style={{ alignItems: 'flex-start' }}>
          <Button variant="text" theme="primary" type="button" onClick={() => handleEdit(row)}>编辑</Button>
          {getResettableAuthMethod(row) && (
            <Button variant="text" theme="primary" type="button" loading={submitting} onClick={() => handleResetCredential(row)}>重置 Secret</Button>
          )}
          <Button variant="text" theme="danger" type="button" onClick={() => handleDelete(row)}>删除</Button>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <div className="page-filter-bar">
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
              <Button theme="primary" type="button" onClick={handleQuery}>查询</Button>
              <Button variant="base" type="button" onClick={handleReset}>重置</Button>
              <Button theme="primary" type="button" onClick={handleAdd}>新增</Button>
            </Space>
          </Form.FormItem>
        </Form>
      </div>

      <div className="page-table-section">
        <div ref={tableWrapRef}>
          <Table rowKey="id" columns={columns} data={sourceData} verticalAlign="middle" maxHeight={tableMaxHeight} tableLayout="fixed" loading={loading} />
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

export default ClientManagementPage;
