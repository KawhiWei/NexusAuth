export type DemoUser = {
  sub?: string | null
  preferred_username?: string | null
  name?: string | null
  email?: string | null
  phone_number?: string | null
}

export type SessionResponse = {
  isAuthenticated: boolean
  user: DemoUser
  expiresIn: number
}

export type ProfileResponse = {
  message: string
  user: DemoUser
  tokenType: string
}

export type LogoutResponse = {
  logoutUrl?: string
}

export type LoginResponse = {
  authorizeUrl: string
}

export type RequestResult<T> = {
  status: number
  data: T | null
}

export type StatusState = {
  type: 'info' | 'error'
  message: string
}
