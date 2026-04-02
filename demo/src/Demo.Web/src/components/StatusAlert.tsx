import type { PropsWithChildren } from 'react'
import type { StatusState } from '../types/auth'

type StatusAlertProps = PropsWithChildren<{
  status: StatusState
  loading?: boolean
}>

export function StatusAlert({ status, loading }: StatusAlertProps) {
  return <div className={`alert ${status.type}`}>{loading ? '正在加载...' : status.message}</div>
}
