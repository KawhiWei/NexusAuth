import request from './request';

export const SCIM_CREDENTIAL_SCOPES = ['scim:read', 'scim:write'] as const;

export type ScimCredentialScope = (typeof SCIM_CREDENTIAL_SCOPES)[number];

export type ScimCredential = {
  id: string;
  name: string;
  scopes: string[];
  isActive: boolean;
  expiresAt?: string | null;
  lastUsedAt?: string | null;
  createdAt: string;
  revokedAt?: string | null;
};

export type CreateScimCredentialRequest = {
  name: string;
  scopes: ScimCredentialScope[];
  expiresAt?: string | null;
};

export type UpdateScimCredentialRequest = {
  name: string;
  scopes: ScimCredentialScope[];
  expiresAt?: string | null;
  isActive: boolean;
};

export type CreatedScimCredential = {
  credential: ScimCredential;
  token: string;
};

export const getScimCredentials = (): Promise<ScimCredential[]> => {
  return request.get<ScimCredential[]>('/scim-credentials');
};

export const createScimCredential = (data: CreateScimCredentialRequest): Promise<CreatedScimCredential> => {
  return request.post<CreatedScimCredential>('/scim-credentials', data);
};

export const updateScimCredential = (id: string, data: UpdateScimCredentialRequest): Promise<ScimCredential> => {
  return request.put<ScimCredential>(`/scim-credentials/${id}`, data);
};

export const revokeScimCredential = (id: string): Promise<void> => {
  return request.post<void>(`/scim-credentials/${id}/revoke`);
};
