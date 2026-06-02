export interface AdminLoginRequest {
  username: string
  password: string
  deviceId?: string
}

export interface AdminLoginResponse {
  adminKey: string
  username: string
  role: 'super_admin' | 'admin'
}

export interface AdminBootstrapRequest {
  username: string
  password: string
}

export interface AdminResetSuperAdminRequest {
  username: string
  password: string
}

export interface AdminMeResponse {
  id: string
  username: string
  role: 'super_admin' | 'admin'
  status: 'active' | 'disabled'
}

export interface AdminSessionListItem {
  id: string
  current: boolean
  maskedIpAddress: string
  ipAddress: string | null
  userAgentSummary: string
  createdAt: string
  expiresAt: string
  lastSeenAt: string
}
