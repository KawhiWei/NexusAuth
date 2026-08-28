import request from './request';

export type ManagedUser = {
  id: string;
  username: string;
  nickname: string;
  email?: string | null;
  phoneNumber?: string | null;
  isActive: boolean;
  externalId?: string | null;
  givenName?: string | null;
  familyName?: string | null;
  title?: string | null;
  userType?: string | null;
  preferredLanguage?: string | null;
  locale?: string | null;
  timezone?: string | null;
  isSystemAccount: boolean;
  createdAt: string;
  updatedAt: string;
};

export type ManagedUserPage = {
  items: ManagedUser[];
  total: number;
  page: number;
  pageSize: number;
};

export type UserFilter = {
  keyword?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
};

export type UpdateManagedUserRequest = {
  nickname: string;
  email?: string | null;
  phoneNumber?: string | null;
  givenName?: string | null;
  familyName?: string | null;
  title?: string | null;
  userType?: string | null;
  preferredLanguage?: string | null;
  locale?: string | null;
  timezone?: string | null;
};

export type ResetManagedUserPasswordRequest = {
  newPassword: string;
};

export const getUsers = (filter?: UserFilter): Promise<ManagedUserPage> => {
  const params = new URLSearchParams();
  if (filter?.keyword) params.append('keyword', filter.keyword);
  if (filter?.isActive !== undefined) params.append('isActive', String(filter.isActive));
  if (filter?.page !== undefined) params.append('page', String(filter.page));
  if (filter?.pageSize !== undefined) params.append('pageSize', String(filter.pageSize));
  const query = params.toString();
  return request.get<ManagedUserPage>(query ? `/users?${query}` : '/users');
};

export const getUser = (id: string): Promise<ManagedUser> => {
  return request.get<ManagedUser>(`/users/${id}`);
};

export const updateUser = (id: string, data: UpdateManagedUserRequest): Promise<ManagedUser> => {
  return request.put<ManagedUser>(`/users/${id}`, data);
};

export const updateUserStatus = (id: string, isActive: boolean): Promise<ManagedUser> => {
  return request.patch<ManagedUser>(`/users/${id}/status`, { isActive });
};

export const resetUserPassword = (id: string, data: ResetManagedUserPasswordRequest): Promise<void> => {
  return request.post<void>(`/users/${id}/reset-password`, data);
};
