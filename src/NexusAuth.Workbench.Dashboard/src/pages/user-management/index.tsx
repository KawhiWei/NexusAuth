import { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Card, Form, Input, Pagination, Select, Space, Table, Tag, type TableProps } from 'tdesign-react';

type UserRole = '管理员' | '编辑' | '普通用户';
type UserStatus = '活跃' | '禁用';

type UserItem = {
  id: string;
  username: string;
  realName: string;
  email: string;
  role: UserRole;
  status: UserStatus;
  createdAt: string;
};

type FilterState = {
  keyword: string;
  role: '' | UserRole;
  status: '' | UserStatus;
};

const defaultFilters: FilterState = {
  keyword: '',
  role: '',
  status: '',
};

const sourceData: UserItem[] = Array.from({ length: 45 }).map((_, index) => {
  const seq = index + 1;
  const roles: UserRole[] = ['管理员', '编辑', '普通用户'];
  const role = roles[index % 3];
  return {
    id: `USER-${String(seq).padStart(4, '0')}`,
    username: `user_${seq}`,
    realName: `用户_${seq}`,
    email: `user_${seq}@example.com`,
    role,
    status: index % 5 === 0 ? '禁用' : '活跃',
    createdAt: `2026-01-${String((index % 28) + 1).padStart(2, '0')} 10:00`,
  };
});

const roleOptions = [
  { label: '全部角色', value: '' },
  { label: '管理员', value: '管理员' },
  { label: '编辑', value: '编辑' },
  { label: '普通用户', value: '普通用户' },
];

const statusOptions = [
  { label: '全部状态', value: '' },
  { label: '活跃', value: '活跃' },
  { label: '禁用', value: '禁用' },
];

const UserManagementPage = () => {
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);

  const columns: TableProps<UserItem>['columns'] = useMemo(
    () => [
      { colKey: 'id', title: '用户ID', width: 120 },
      { colKey: 'username', title: '用户名', minWidth: 120, ellipsis: true },
      { colKey: 'realName', title: '真实姓名', minWidth: 120, ellipsis: true },
      { colKey: 'email', title: '邮箱', minWidth: 200, ellipsis: true },
      {
        colKey: 'role',
        title: '角色',
        width: 120,
        cell: ({ row }) => {
          const theme = row.role === '管理员' ? 'warning' : row.role === '编辑' ? 'primary' : 'default';
          return <Tag theme={theme} variant="light">{row.role}</Tag>;
        }
      },
      {
        colKey: 'status',
        title: '状态',
        width: 100,
        cell: ({ row }) => (
          <Tag theme={row.status === '活跃' ? 'success' : 'danger'} variant="dot">
            {row.status}
          </Tag>
        ),
      },
      { colKey: 'createdAt', title: '注册时间', width: 180 },
      {
        colKey: 'operation',
        title: '操作',
        width: 160,
        fixed: 'right',
        cell: () => (
          <Space size="small">
            <Button variant="text" theme="primary" size="small">
              编辑
            </Button>
            <Button variant="text" theme="danger" size="small">
              重置密码
            </Button>
          </Space>
        ),
      },
    ],
    [],
  );

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

  const filteredData = useMemo(() => {
    const keywordValue = appliedFilters.keyword.trim().toLowerCase();
    return sourceData.filter((item) => {
      const matchesKeyword =
        keywordValue.length === 0 ||
        item.username.toLowerCase().includes(keywordValue) ||
        item.realName.toLowerCase().includes(keywordValue) ||
        item.email.toLowerCase().includes(keywordValue);
      const matchesRole = !appliedFilters.role || item.role === appliedFilters.role;
      const matchesStatus = !appliedFilters.status || item.status === appliedFilters.status;
      return matchesKeyword && matchesRole && matchesStatus;
    });
  }, [appliedFilters]);

  const total = filteredData.length;

  useEffect(() => {
    const maxPage = Math.max(1, Math.ceil(total / pageSize));
    if (current > maxPage) {
      setCurrent(maxPage);
    }
  }, [current, pageSize, total]);

  const pagedData = useMemo(() => {
    const start = (current - 1) * pageSize;
    return filteredData.slice(start, start + pageSize);
  }, [current, filteredData, pageSize]);

  const handleQuery = () => {
    setAppliedFilters(filters);
    setCurrent(1);
  };

  const handleReset = () => {
    setFilters(defaultFilters);
    setAppliedFilters(defaultFilters);
    setCurrent(1);
  };

  return (
    <div>
      <Card bordered>
        <Form layout="inline">
          <Form.FormItem label="关键词">
            <Input
              clearable
              value={filters.keyword}
              placeholder="请输入用户名/姓名/邮箱"
              style={{ width: 260 }}
              onChange={(value) => setFilters((prev) => ({ ...prev, keyword: value }))}
            />
          </Form.FormItem>

          <Form.FormItem label="角色">
            <Select
              value={filters.role}
              style={{ width: 140 }}
              options={roleOptions}
              onChange={(value) => setFilters((prev) => ({ ...prev, role: String(value ?? '') as '' | UserRole }))}
            />
          </Form.FormItem>

          <Form.FormItem label="状态">
            <Select
              value={filters.status}
              style={{ width: 140 }}
              options={statusOptions}
              onChange={(value) =>
                setFilters((prev) => ({
                  ...prev,
                  status: (value === '活跃' || value === '禁用' ? value : '') as '' | UserStatus,
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

export default UserManagementPage;
