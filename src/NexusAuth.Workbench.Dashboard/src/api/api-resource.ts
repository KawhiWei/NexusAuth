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

export type CreateApiResourceRequest = {
  name: string;
  displayName: string;
  audience: string;
  description?: string;
};

export type ApiResourceFilter = {
  keyword?: string;
  isActive?: boolean;
};

export const getApiResources = (filter?: ApiResourceFilter): Promise<ApiResource[]> => {
  const params = new URLSearchParams();
  if (filter?.keyword) params.append('keyword', filter.keyword);
  if (filter?.isActive !== undefined) params.append('isActive', String(filter.isActive));
  const query = params.toString();
  return request.get<ApiResource[]>(query ? `/api-resources?${query}` : '/api-resources');
};

export const getApiResource = (id: string): Promise<ApiResource> => {
  return request.get<ApiResource>(`/api-resources/${id}`);
};

export const createApiResource = (data: CreateApiResourceRequest): Promise<ApiResource> => {
  return request.post<ApiResource>('/api-resources', data);
};