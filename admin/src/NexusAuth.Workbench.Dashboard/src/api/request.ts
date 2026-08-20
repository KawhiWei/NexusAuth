import axios, { type AxiosError, type AxiosResponse } from 'axios';

declare module 'axios' {
  export interface AxiosInstance {
    get<T>(url: string, config?: AxiosRequestConfig): Promise<T>;
    post<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T>;
    put<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T>;
    delete<T>(url: string, config?: AxiosRequestConfig): Promise<T>;
  }
}

const request = axios.create({
  baseURL: '/api',
  timeout: 10000,
  withCredentials: true,
});

request.interceptors.response.use(
  (response: AxiosResponse) => response.data,
  (error: AxiosError) => {
    if (error.response?.status === 401 && !error.config?.url?.includes('/auth/')) {
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default request;