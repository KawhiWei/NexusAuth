import request from './request';

export type CreateClientRequest = {
  clientId: string;
  clientName: string;
  description?: string;
  redirectUris?: string[];
  postLogoutRedirectUris?: string[];
  allowedScopes?: string[];
  allowedGrantTypes?: string[];
  apiResourceIds?: string[];
  requirePkce: boolean;
  tokenEndpointAuthMethod?: string;
  autoGenerateJwks?: boolean;
  jwks?: string;
  jwksUri?: string;
};

export type UpdateClientRequest = {
  clientName?: string;
  description?: string;
  redirectUris?: string[];
  postLogoutRedirectUris?: string[];
  allowedScopes?: string[];
  allowedGrantTypes?: string[];
  apiResourceIds?: string[];
  requirePkce?: boolean;
  tokenEndpointAuthMethod?: string;
  jwks?: string;
  jwksUri?: string;
  isActive?: boolean;
};

export type GenerateClientCredentialRequest = {
  tokenEndpointAuthMethod?: string;
  autoGenerateJwks?: boolean;
  description?: string;
};

export type ClientCredential = {
  id: string;
  type: string;
  isActive: boolean;
  createdAt: string;
};

export type GeneratedClientCredential = {
  type: string;
  clientSecret?: string;
  privateKeyPem?: string;
  jwks?: string;
  description?: string;
};

export type ClientMutationResult = {
  client: Client;
  generatedCredential?: GeneratedClientCredential;
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
  apiResourceIds: string[];
  requirePkce: boolean;
  isActive: boolean;
  tokenEndpointAuthMethod: string;
  jwks?: string;
  jwksUri?: string;
  credentials: ClientCredential[];
  createdAt: string;
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

export const createClient = (data: CreateClientRequest): Promise<ClientMutationResult> => {
  return request.post<ClientMutationResult>('/clients', data);
};

export const updateClient = (id: string, data: UpdateClientRequest): Promise<Client> => {
  return request.put<Client>(`/clients/${id}`, data);
};

export const generateClientCredential = (id: string, data: GenerateClientCredentialRequest = {}): Promise<ClientMutationResult> => {
  return request.post<ClientMutationResult>(`/clients/${id}/credentials`, data);
};

export const resetClientCredential = (id: string, data: GenerateClientCredentialRequest = {}): Promise<ClientMutationResult> => {
  return request.post<ClientMutationResult>(`/clients/${id}/credentials/reset`, data);
};

export const deleteClient = (id: string): Promise<void> => {
  return request.delete<void>(`/clients/${id}`);
};
