import request from './request';

export type ClientSecretInput = {
  value: string;
  description?: string;
};

export type CreateClientRequest = {
  clientId: string;
  clientName: string;
  description?: string;
  redirectUris?: string[];
  postLogoutRedirectUris?: string[];
  allowedScopes?: string[];
  allowedGrantTypes?: string[];
  requirePkce: boolean;
  tokenEndpointAuthMethod: string;
  clientSecrets?: ClientSecretInput[];
  apiResourceIds?: string[];
};

export type UpdateClientRequest = {
  clientName?: string;
  description?: string;
  redirectUris?: string[];
  postLogoutRedirectUris?: string[];
  allowedScopes?: string[];
  allowedGrantTypes?: string[];
  requirePkce?: boolean;
  isActive?: boolean;
  clientSecrets?: ClientSecretInput[];
  apiResourceIds?: string[];
};

export type Client = {
  id: string;
  clientId: string;
  clientName: string;
  description?: string;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  allowedScopes: string[];
  allowedGrantTypes: string[];
  requirePkce: boolean;
  isActive: boolean;
  tokenEndpointAuthMethod: string;
  clientSecrets: ClientSecretInput[];
  createdAt: string;
  apiResourceIds?: string[];
};

export type PagedResult<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export type ClientFilter = {
  keyword?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
};

export const getClients = (filter?: ClientFilter): Promise<PagedResult<Client>> => {
  const params = new URLSearchParams();
  if (filter?.keyword) params.append('keyword', filter.keyword);
  if (filter?.isActive !== undefined) params.append('isActive', String(filter.isActive));
  if (filter?.page !== undefined) params.append('page', String(filter.page));
  if (filter?.pageSize !== undefined) params.append('pageSize', String(filter.pageSize));
  const query = params.toString();
  return request.get<PagedResult<Client>>(query ? `/clients?${query}` : '/clients');
};

export const getAllClients = (filter?: Omit<ClientFilter, 'page' | 'pageSize'>): Promise<Client[]> => {
  const params = new URLSearchParams();
  if (filter?.keyword) params.append('keyword', filter.keyword);
  if (filter?.isActive !== undefined) params.append('isActive', String(filter.isActive));
  const query = params.toString();
  return request.get<Client[]>(query ? `/clients/all?${query}` : '/clients/all');
};

export const getClient = (id: string): Promise<Client> => {
  return request.get<Client>(`/clients/${id}`);
};

export const createClient = (data: CreateClientRequest): Promise<Client> => {
  return request.post<Client>('/clients', data);
};

export const updateClient = (id: string, data: UpdateClientRequest): Promise<Client> => {
  return request.put<Client>(`/clients/${id}`, data);
};

export const deleteClient = (id: string): Promise<void> => {
  return request.delete<void>(`/clients/${id}`);
};
