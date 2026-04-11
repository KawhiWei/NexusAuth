import request from './request';

export interface UserInfo {
  id: string;
  name: string;
}

export interface LoginResponse {
  isAuthenticated: boolean;
  user: UserInfo;
}

export interface ConfigResponse {
  authority: string;
  clientId: string;
}

export const getConfig = (): Promise<ConfigResponse> => {
  return request.get<ConfigResponse>('/auth/config');
};

export const startLogin = (): Promise<{ authorizeUrl: string }> => {
  return request.get<{ authorizeUrl: string }>('/auth/login');
};

export const getCurrentUser = (): Promise<LoginResponse> => {
  return request.get<LoginResponse>('/auth/me');
};

export const logout = () => {
  return request.post('/auth/logout');
};

export const checkAuth = async (): Promise<boolean> => {
  try {
    const result = await getCurrentUser();
    return result.isAuthenticated;
  } catch {
    return false;
  }
};