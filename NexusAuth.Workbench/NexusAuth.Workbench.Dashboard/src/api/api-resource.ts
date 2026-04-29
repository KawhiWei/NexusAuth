import request from './request';

export type ApiResource = {
  id: string;
  name: string;
  displayName: string;
  audience: string;
  description?: string;
  isActive: boolean;
  createdAt: string;
};

export type PagedResult<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export type CreateApiResourceRequest = {
  name: string;
  displayName: string;
  audience: string;
  description?: string;
};

export type UpdateApiResourceRequest = {
  displayName?: string;
  audience?: string;
  description?: string;
  isActive?: boolean;
};

export type ApiResourceFilter = {
  keyword?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
};

export const getApiResources = (filter?: ApiResourceFilter): Promise<PagedResult<ApiResource>> => {
  const params = new URLSearchParams();
  if (filter?.keyword) params.append('keyword', filter.keyword);
  if (filter?.isActive !== undefined) params.append('isActive', String(filter.isActive));
  if (filter?.page !== undefined) params.append('page', String(filter.page));
  if (filter?.pageSize !== undefined) params.append('pageSize', String(filter.pageSize));
  const query = params.toString();
  return request.get<PagedResult<ApiResource>>(query ? `/api-resources?${query}` : '/api-resources');
};

export const getAllApiResources = (filter?: Omit<ApiResourceFilter, 'page' | 'pageSize'>): Promise<ApiResource[]> => {
  const params = new URLSearchParams();
  if (filter?.keyword) params.append('keyword', filter.keyword);
  if (filter?.isActive !== undefined) params.append('isActive', String(filter.isActive));
  const query = params.toString();
  return request.get<ApiResource[]>(query ? `/api-resources/all?${query}` : '/api-resources/all');
};

export const getApiResource = (id: string): Promise<ApiResource> => {
  return request.get<ApiResource>(`/api-resources/${id}`);
};

export const createApiResource = (data: CreateApiResourceRequest): Promise<ApiResource> => {
  return request.post<ApiResource>('/api-resources', data);
};

export const updateApiResource = (id: string, data: UpdateApiResourceRequest): Promise<ApiResource> => {
  return request.put<ApiResource>(`/api-resources/${id}`, data);
};

export const deleteApiResource = (id: string): Promise<void> => {
  return request.delete<void>(`/api-resources/${id}`);
};
