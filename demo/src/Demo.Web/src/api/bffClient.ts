import type { LoginResponse, LogoutResponse, ProfileResponse, RequestResult, SessionResponse } from '../types/auth'
import axios, { AxiosError } from 'axios'

const http = axios.create({
  baseURL: '/api',
  withCredentials: true,
})

async function requestJson<T>(url: string, method: 'GET' | 'POST' = 'GET'): Promise<RequestResult<T>> {
  try {
    const response = await http.request<T>({
      url,
      method,
    })

    return {
      status: response.status,
      data: response.data ?? null,
    }
  } catch (error) {
    const axiosError = error as AxiosError<T>
    if (axiosError.response) {
      return {
        status: axiosError.response.status,
        data: (axiosError.response.data as T) ?? null,
      }
    }

    throw error
  }
}

export function getSession() {
  return requestJson<SessionResponse>('/auth/me')
}

export function startLogin() {
  return requestJson<LoginResponse>('/auth/login')
}

export function logout() {
  return requestJson<LogoutResponse>('/auth/logout', 'POST')
}

export function getProfile() {
  return requestJson<ProfileResponse>('/profile')
}
