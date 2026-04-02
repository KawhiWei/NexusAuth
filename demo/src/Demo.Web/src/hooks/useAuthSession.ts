import { useEffect, useMemo, useState } from 'react'
import { getProfile, getSession, logout, startLogin } from '../api/bffClient'
import type { DemoUser, ProfileResponse, StatusState } from '../types/auth'

const defaultProfilePayload = '等待调用 /api/profile'

export function useAuthSession() {
  const [loading, setLoading] = useState(true)
  const [authenticated, setAuthenticated] = useState(false)
  const [user, setUser] = useState<DemoUser | null>(null)
  const [profilePayload, setProfilePayload] = useState(defaultProfilePayload)
  const [status, setStatus] = useState<StatusState>({ type: 'info', message: '正在检查登录状态...' })

  const isCallbackRoute = useMemo(() => window.location.pathname === '/auth/callback', [])

  useEffect(() => {
    if (isCallbackRoute) {
      window.history.replaceState({}, '', '/')
      setStatus({ type: 'info', message: '登录成功，正在刷新会话状态...' })
    }

    void refreshSession()
  }, [isCallbackRoute])

  async function refreshSession() {
    try {
      setLoading(true)
      const result = await getSession()
      if (result.status === 401 || !result.data) {
        setAuthenticated(false)
        setUser(null)
        setStatus({ type: 'info', message: '当前未登录，请点击“使用 NexusAuth 登录”。' })
        return
      }

      setAuthenticated(result.data.isAuthenticated)
      setUser(result.data.user)
      setStatus({ type: 'info', message: '当前已登录，服务端会话有效。' })
    } catch (error) {
      setStatus({ type: 'error', message: `会话检查失败：${(error as Error).message}` })
    } finally {
      setLoading(false)
    }
  }

  async function handleLogin() {
    try {
      setStatus({ type: 'info', message: '正在请求 BFF 生成授权地址...' })
      const result = await startLogin()
      if (!result.data?.authorizeUrl) {
        setStatus({ type: 'error', message: '登录初始化失败。' })
        return
      }

      window.location.href = result.data.authorizeUrl
    } catch (error) {
      setStatus({ type: 'error', message: `登录初始化失败：${(error as Error).message}` })
    }
  }

  async function handleLogout() {
    try {
      setStatus({ type: 'info', message: '正在退出登录...' })
      const result = await logout()
      setAuthenticated(false)
      setUser(null)
      setProfilePayload(defaultProfilePayload)

      if (result.data?.logoutUrl) {
        window.location.href = result.data.logoutUrl
        return
      }

      setStatus({ type: 'info', message: '已退出登录。' })
    } catch (error) {
      setStatus({ type: 'error', message: `退出失败：${(error as Error).message}` })
    }
  }

  async function handleLoadProfile() {
    try {
      setStatus({ type: 'info', message: '正在调用受保护的业务接口...' })
      const result = await getProfile()
      if (result.status === 401 || !result.data) {
        setAuthenticated(false)
        setUser(null)
        setStatus({ type: 'error', message: '服务端会话已失效，请重新登录。' })
        return
      }

      setProfilePayload(JSON.stringify(result.data satisfies ProfileResponse, null, 2))
      setStatus({ type: 'info', message: '业务接口调用成功。' })
    } catch (error) {
      setStatus({ type: 'error', message: `业务接口调用失败：${(error as Error).message}` })
    }
  }

  return {
    loading,
    authenticated,
    user,
    profilePayload,
    status,
    handleLogin,
    handleLogout,
    handleLoadProfile,
  }
}
