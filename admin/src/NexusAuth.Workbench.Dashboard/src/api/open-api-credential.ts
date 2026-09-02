import request from './request';

export const OPEN_API_CREDENTIAL_TARGET_TYPES = ['application', 'service_resource'] as const;

export type OpenApiCredentialTargetType = (typeof OPEN_API_CREDENTIAL_TARGET_TYPES)[number];

export type OpenApiCredential = {
  id: string;
  name: string;
  targetType: OpenApiCredentialTargetType;
  scopes: string[];
  isActive: boolean;
  expiresAt?: string | null;
  lastUsedAt?: string | null;
  createdAt: string;
  revokedAt?: string | null;
};

export type CreateOpenApiCredentialRequest = {
  name: string;
  targetType: OpenApiCredentialTargetType;
  expiresAt?: string | null;
};

export type UpdateOpenApiCredentialRequest = {
  name: string;
  expiresAt?: string | null;
  isActive: boolean;
};

export type CreatedOpenApiCredential = {
  credential: OpenApiCredential;
  token: string;
};

export const getOpenApiCredentials = (): Promise<OpenApiCredential[]> => {
  return request.get<OpenApiCredential[]>('/open-api-credentials');
};

export const createOpenApiCredential = (
  data: CreateOpenApiCredentialRequest,
): Promise<CreatedOpenApiCredential> => {
  return request.post<CreatedOpenApiCredential>('/open-api-credentials', data);
};

export const updateOpenApiCredential = (
  id: string,
  data: UpdateOpenApiCredentialRequest,
): Promise<OpenApiCredential> => {
  return request.put<OpenApiCredential>(`/open-api-credentials/${id}`, data);
};

export const revokeOpenApiCredential = (id: string): Promise<void> => {
  return request.post<void>(`/open-api-credentials/${id}/revoke`);
};
