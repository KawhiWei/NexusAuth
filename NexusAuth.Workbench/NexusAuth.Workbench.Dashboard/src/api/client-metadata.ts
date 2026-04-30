import request from './request';

export type ClientOption = {
  value: string;
  label: string;
  description: string;
};

export type ClientMetadata = {
  scopes: ClientOption[];
  grantTypes: ClientOption[];
  tokenEndpointAuthMethods: ClientOption[];
};

export const getClientMetadata = (): Promise<ClientMetadata> => {
  return request.get<ClientMetadata>('/client-metadata');
};
