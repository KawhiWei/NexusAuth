import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { AxiosError } from 'axios';
import { Button, Dialog, Drawer, Form, Input, MessagePlugin, Pagination, Select, Space, Switch, Table, Tag, type TableProps } from 'tdesign-react';
import { EditIcon, KeyIcon, RefreshIcon, SearchIcon } from 'tdesign-icons-react';
import { getUsers, resetUserPassword, updateUser, updateUserStatus, type ManagedUser, type ResetManagedUserPasswordRequest, type UpdateManagedUserRequest } from '../../api/user';
import './style.less';

type FilterState = {
  keyword: string;
  isActive: '' | boolean;
};

type UserFormData = {
  nickname: string;
  email: string;
  phoneNumber: string;
  givenName: string;
  familyName: string;
  title: string;
  userType: string;
  preferredLanguage: string;
  locale: string;
  timezone: string;
};

type ResetPasswordFormData = {
  newPassword: string;
  confirmPassword: string;
};

const defaultFilters: FilterState = {
  keyword: '',
  isActive: '',
};

const defaultFormData: UserFormData = {
  nickname: '',
  email: '',
  phoneNumber: '',
  givenName: '',
  familyName: '',
  title: '',
  userType: '',
  preferredLanguage: '',
  locale: '',
  timezone: '',
};

const defaultResetPasswordFormData: ResetPasswordFormData = {
  newPassword: '',
  confirmPassword: '',
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

const formatDateTime = (value?: string | null) => {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString('zh-CN', { hour12: false });
};

const toFormData = (user: ManagedUser): UserFormData => ({
  nickname: user.nickname ?? '',
  email: user.email ?? '',
  phoneNumber: user.phoneNumber ?? '',
  givenName: user.givenName ?? '',
  familyName: user.familyName ?? '',
  title: user.title ?? '',
  userType: user.userType ?? '',
  preferredLanguage: user.preferredLanguage ?? '',
  locale: user.locale ?? '',
  timezone: user.timezone ?? '',
});

const nullableValue = (value: string) => value.trim() || null;

const UserManagementPage = () => {
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [sourceData, setSourceData] = useState<ManagedUser[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);
  const formRef = useRef<any>(null);
  const [editorVisible, setEditorVisible] = useState(false);
  const [editingUser, setEditingUser] = useState<ManagedUser | null>(null);
  const [formData, setFormData] = useState<UserFormData>(defaultFormData);
  const [submitting, setSubmitting] = useState(false);
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const [resetPasswordVisible, setResetPasswordVisible] = useState(false);
  const [resetPasswordTarget, setResetPasswordTarget] = useState<ManagedUser | null>(null);
  const [resetPasswordFormData, setResetPasswordFormData] = useState<ResetPasswordFormData>(defaultResetPasswordFormData);
  const [resetPasswordSubmitting, setResetPasswordSubmitting] = useState(false);
  const resetPasswordFormRef = useRef<any>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const filter: { keyword?: string; isActive?: boolean; page: number; pageSize: number } = {
        page: current,
        pageSize,
      };
      if (appliedFilters.keyword.trim()) {
        filter.keyword = appliedFilters.keyword.trim();
      }
      if (appliedFilters.isActive !== '') {
        filter.isActive = appliedFilters.isActive;
      }

      const result = await getUsers(filter);
      setSourceData(result.items);
      setTotal(result.total);
    } catch (error) {
      console.error('Failed to fetch users:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '加载用户失败'));
    } finally {
      setLoading(false);
    }
  }, [appliedFilters, current, pageSize]);

  useEffect(() => {
    void fetchData();
  }, [fetchData]);

  useEffect(() => {
    const maxPage = Math.max(1, Math.ceil(total / pageSize));
    if (current > maxPage) {
      setCurrent(maxPage);
    }
  }, [current, pageSize, total]);

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
    setAppliedFilters({ ...filters });
    setCurrent(1);
  };

  const handleReset = () => {
    setFilters(defaultFilters);
    setAppliedFilters(defaultFilters);
    setCurrent(1);
  };

  const handleEdit = (user: ManagedUser) => {
    setEditingUser(user);
    setFormData(toFormData(user));
    setEditorVisible(true);
  };

  const handleCloseEditor = () => {
    if (submitting) {
      return;
    }
    setEditorVisible(false);
    setEditingUser(null);
    setFormData(defaultFormData);
  };

  const handleSubmit = async () => {
    if (!editingUser || !formRef.current) {
      return;
    }

    const validation = await formRef.current.validate();
    if (validation.errors && Object.keys(validation.errors).length > 0) {
      return;
    }

    const request: UpdateManagedUserRequest = {
      nickname: formData.nickname.trim(),
      email: nullableValue(formData.email),
      phoneNumber: nullableValue(formData.phoneNumber),
      givenName: nullableValue(formData.givenName),
      familyName: nullableValue(formData.familyName),
      title: nullableValue(formData.title),
      userType: nullableValue(formData.userType),
      preferredLanguage: nullableValue(formData.preferredLanguage),
      locale: nullableValue(formData.locale),
      timezone: nullableValue(formData.timezone),
    };

    try {
      setSubmitting(true);
      await updateUser(editingUser.id, request);
      setEditorVisible(false);
      setEditingUser(null);
      setFormData(defaultFormData);
      await fetchData();
      MessagePlugin.success('用户资料已更新');
    } catch (error) {
      console.error('Failed to update user:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '更新用户资料失败'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleToggleStatus = async (user: ManagedUser) => {
    try {
      setTogglingId(user.id);
      await updateUserStatus(user.id, !user.isActive);
      await fetchData();
      MessagePlugin.success(user.isActive ? '用户已禁用' : '用户已启用');
    } catch (error) {
      console.error('Failed to update user status:', error);
      MessagePlugin.error(getRequestErrorMessage(error, '更新用户状态失败'));
    } finally {
      setTogglingId(null);
    }
  };

  const handleShowResetPassword = (user: ManagedUser) => {
    setResetPasswordTarget(user);
    setResetPasswordFormData(defaultResetPasswordFormData);
    setResetPasswordVisible(true);
  };

  const handleCloseResetPassword = () => {
    if (resetPasswordSubmitting) {
      return;
    }
    setResetPasswordVisible(false);
    setResetPasswordTarget(null);
    setResetPasswordFormData(defaultResetPasswordFormData);
  };

  const handleResetPassword = async () => {
    if (!resetPasswordTarget || !resetPasswordFormRef.current) {
      return;
    }

    const validation = await resetPasswordFormRef.current.validate();
    if (validation.errors && Object.keys(validation.errors).length > 0) {
      return;
    }

    if (resetPasswordFormData.newPassword !== resetPasswordFormData.confirmPassword) {
      MessagePlugin.warning('两次输入的密码不一致');
      return;
    }

    const request: ResetManagedUserPasswordRequest = {
      newPassword: resetPasswordFormData.newPassword,
    };

    try {
      setResetPasswordSubmitting(true);
      await resetUserPassword(resetPasswordTarget.id, request);
      setResetPasswordVisible(false);
      setResetPasswordTarget(null);
      setResetPasswordFormData(defaultResetPasswordFormData);
      MessagePlugin.success('用户密码已重置');
    } catch (error) {
      // Do not log the Axios error because its request config may contain the password.
      MessagePlugin.error(getRequestErrorMessage(error, '重置用户密码失败'));
    } finally {
      setResetPasswordSubmitting(false);
    }
  };

  const columns: TableProps<ManagedUser>['columns'] = useMemo(
    () => [
      { colKey: 'username', title: '用户名', width: 150, ellipsis: true },
      { colKey: 'nickname', title: '昵称', width: 140, ellipsis: true },
      {
        colKey: 'name',
        title: '姓名',
        width: 140,
        ellipsis: true,
        cell: ({ row }) => [row.familyName, row.givenName].filter(Boolean).join(' ') || '-',
      },
      { colKey: 'email', title: '邮箱', minWidth: 210, ellipsis: true, cell: ({ row }) => row.email || '-' },
      { colKey: 'phoneNumber', title: '手机号', width: 150, ellipsis: true, cell: ({ row }) => row.phoneNumber || '-' },
      { colKey: 'title', title: '职务', width: 140, ellipsis: true, cell: ({ row }) => row.title || '-' },
      { colKey: 'userType', title: '用户类型', width: 130, ellipsis: true, cell: ({ row }) => row.userType || '-' },
      {
        colKey: 'isActive',
        title: '状态',
        width: 115,
        cell: ({ row }) => (
          <Switch
            value={row.isActive}
            loading={togglingId === row.id}
            disabled={row.isSystemAccount}
            label={({ value }) => value ? '启用' : '禁用'}
            onChange={() => void handleToggleStatus(row)}
          />
        ),
      },
      {
        colKey: 'source',
        title: '来源',
        width: 100,
        cell: ({ row }) => row.externalId
          ? <Tag theme="primary" variant="outline">SCIM</Tag>
          : <Tag theme="default" variant="outline">本地</Tag>,
      },
      { colKey: 'updatedAt', title: '更新时间', width: 180, cell: ({ row }) => formatDateTime(row.updatedAt) },
      {
        colKey: 'operation',
        title: '操作',
        width: 160,
        fixed: 'right',
        cell: ({ row }) => row.isSystemAccount
          ? <Tag theme="default" variant="light-outline">系统账号受保护</Tag>
          : (
            <Space direction="vertical" size={4} style={{ alignItems: 'flex-start' }}>
              <Button variant="text" theme="primary" icon={<EditIcon />} onClick={() => handleEdit(row)}>
                编辑
              </Button>
              <Button variant="text" theme="danger" icon={<KeyIcon />} onClick={() => handleShowResetPassword(row)}>
                重置密码
              </Button>
            </Space>
          ),
      },
    ],
    [togglingId],
  );

  return (
    <div className="user-management-page">
      <Dialog
        visible={resetPasswordVisible}
        className="user-management-reset-password-dialog"
        header="重置用户密码"
        width="min(440px, calc(100vw - 32px))"
        confirmBtn="重置密码"
        cancelBtn="取消"
        confirmLoading={resetPasswordSubmitting}
        destroyOnClose
        onClose={handleCloseResetPassword}
        onConfirm={() => void handleResetPassword()}
      >
        {resetPasswordTarget && (
          <Form
            className="user-management-password-form"
            key={resetPasswordTarget.id}
            ref={resetPasswordFormRef}
            layout="vertical"
            labelAlign="top"
            colon
            initialData={resetPasswordFormData}
          >
            <div className="user-management-password-form__target">
              为用户 <strong>{resetPasswordTarget.username}</strong> 设置新密码
            </div>
            <Form.FormItem
              label="新密码"
              name="newPassword"
              rules={[
                { required: true, message: '请输入新密码', type: 'error' },
                { whitespace: true, message: '新密码不能只包含空格', type: 'error' },
              ]}
            >
              <Input
                type="password"
                autocomplete="new-password"
                value={resetPasswordFormData.newPassword}
                placeholder="请输入新密码"
                onChange={(value) => setResetPasswordFormData((previous) => ({ ...previous, newPassword: value }))}
              />
            </Form.FormItem>
            <Form.FormItem
              label="确认新密码"
              name="confirmPassword"
              rules={[
                { required: true, message: '请再次输入新密码', type: 'error' },
                { whitespace: true, message: '确认密码不能只包含空格', type: 'error' },
                {
                  validator: (value) => value === resetPasswordFormData.newPassword,
                  message: '两次输入的密码不一致',
                  type: 'error',
                },
              ]}
            >
              <Input
                type="password"
                autocomplete="new-password"
                value={resetPasswordFormData.confirmPassword}
                placeholder="请再次输入新密码"
                onChange={(value) => setResetPasswordFormData((previous) => ({ ...previous, confirmPassword: value }))}
              />
            </Form.FormItem>
          </Form>
        )}
      </Dialog>

      <Drawer
        visible={editorVisible}
        className="user-management-drawer"
        header="编辑用户资料"
        onClose={handleCloseEditor}
        footer={false}
        size="min(620px, 100vw)"
        destroyOnClose
      >
        {editingUser && (
          <>
            <div className="user-management-identity">
              <div>
                <span className="user-management-identity__label">用户名</span>
                <strong>{editingUser.username}</strong>
              </div>
              <div>
                <span className="user-management-identity__label">SCIM External ID</span>
                <code>{editingUser.externalId || '-'}</code>
              </div>
            </div>
            <Form
              className="user-management-form"
              key={editingUser.id}
              ref={formRef}
              layout="vertical"
              labelAlign="top"
              colon
              initialData={formData}
            >
              <div className="user-management-form__grid">
                <Form.FormItem label="昵称" name="nickname" rules={[{ required: true, message: '请输入昵称', type: 'error' }]}>
                  <Input value={formData.nickname} placeholder="请输入昵称" onChange={(value) => setFormData((prev) => ({ ...prev, nickname: value }))} />
                </Form.FormItem>
                <Form.FormItem label="邮箱" name="email">
                  <Input value={formData.email} clearable type="text" placeholder="请输入邮箱" onChange={(value) => setFormData((prev) => ({ ...prev, email: value }))} />
                </Form.FormItem>
                <Form.FormItem label="手机号" name="phoneNumber">
                  <Input value={formData.phoneNumber} clearable type="tel" placeholder="请输入手机号" onChange={(value) => setFormData((prev) => ({ ...prev, phoneNumber: value }))} />
                </Form.FormItem>
                <Form.FormItem label="名 (Given name)" name="givenName">
                  <Input value={formData.givenName} clearable placeholder="请输入名" onChange={(value) => setFormData((prev) => ({ ...prev, givenName: value }))} />
                </Form.FormItem>
                <Form.FormItem label="姓 (Family name)" name="familyName">
                  <Input value={formData.familyName} clearable placeholder="请输入姓" onChange={(value) => setFormData((prev) => ({ ...prev, familyName: value }))} />
                </Form.FormItem>
                <Form.FormItem label="职务 (Title)" name="title">
                  <Input value={formData.title} clearable placeholder="请输入职务" onChange={(value) => setFormData((prev) => ({ ...prev, title: value }))} />
                </Form.FormItem>
                <Form.FormItem label="用户类型 (User type)" name="userType">
                  <Input value={formData.userType} clearable placeholder="请输入用户类型" onChange={(value) => setFormData((prev) => ({ ...prev, userType: value }))} />
                </Form.FormItem>
                <Form.FormItem label="首选语言" name="preferredLanguage">
                  <Input value={formData.preferredLanguage} clearable placeholder="如 zh-CN" onChange={(value) => setFormData((prev) => ({ ...prev, preferredLanguage: value }))} />
                </Form.FormItem>
                <Form.FormItem label="Locale" name="locale">
                  <Input value={formData.locale} clearable placeholder="如 zh-CN" onChange={(value) => setFormData((prev) => ({ ...prev, locale: value }))} />
                </Form.FormItem>
                <Form.FormItem label="时区" name="timezone">
                  <Input value={formData.timezone} clearable placeholder="如 Asia/Shanghai" onChange={(value) => setFormData((prev) => ({ ...prev, timezone: value }))} />
                </Form.FormItem>
              </div>
            </Form>
            <div className="user-management-drawer__footer">
              <Button variant="base" onClick={handleCloseEditor} disabled={submitting}>取消</Button>
              <Button theme="primary" loading={submitting} onClick={() => void handleSubmit()}>保存</Button>
            </div>
          </>
        )}
      </Drawer>

      <div className="page-filter-bar">
        <Form layout="inline" className="user-management-filter-form">
          <Form.FormItem label="关键词">
            <Input
              clearable
              value={filters.keyword}
              prefixIcon={<SearchIcon />}
              placeholder="用户名、昵称、邮箱或手机号"
              style={{ width: 280 }}
              onChange={(value) => setFilters((prev) => ({ ...prev, keyword: value }))}
            />
          </Form.FormItem>
          <Form.FormItem label="状态">
            <Select
              value={filters.isActive}
              options={statusOptions}
              style={{ width: 140 }}
              onChange={(value) => setFilters((prev) => ({ ...prev, isActive: value === true || value === false ? value : '' }))}
            />
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
          <Table
            rowKey="id"
            columns={columns}
            data={sourceData}
            verticalAlign="middle"
            maxHeight={tableMaxHeight}
            tableLayout="fixed"
            resizable
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

export default UserManagementPage;
