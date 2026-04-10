import request from './request';

export type ClientSecretInput = {
  type: string;
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
};

export type ClientFilter = {
  keyword?: string;
  isActive?: boolean;
};

export const getClients = (filter?: ClientFilter) => {
  const params = new URLSearchParams();
  if (filter?.keyword) params.append('keyword', filter.keyword);
  if (filter?.isActive !== undefined) params.append('isActive', String(filter.isActive));
  const query = params.toString();
  return request.get<Client[]>(query ? `/clients?${query}` : '/clients');
};

export const getClient = (id: string) => {
  return request.get<Client>(`/clients/${id}`);
};

export const createClient = (data: CreateClientRequest) => {
  return request.post<Client>('/clients', data);
};

export const updateClient = (id: string, data: UpdateClientRequest) => {
  return request.put<Client>(`/clients/${id}`, data);
};

export const deleteClient = (id: string) => {
  return request.delete<void>(`/clients/${id}`);
};