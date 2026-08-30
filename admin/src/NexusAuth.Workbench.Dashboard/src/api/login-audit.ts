import request from './request';

export type LoginAuditLog = {
  id: string;
  username: string;
  clientId?: string | null;
  isSuccessful: boolean;
  failureReason?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  occurredAt: string;
};

export type LoginAuditPage = {
  items: LoginAuditLog[];
  total: number;
  page: number;
  pageSize: number;
};

export type LoginAuditFilter = {
  keyword?: string;
  isSuccessful?: boolean;
  clientId?: string;
  page?: number;
  pageSize?: number;
};

export const getLoginAudits = (filter?: LoginAuditFilter): Promise<LoginAuditPage> => {
  const params = new URLSearchParams();
  if (filter?.keyword) params.append('keyword', filter.keyword);
  if (filter?.isSuccessful !== undefined) params.append('isSuccessful', String(filter.isSuccessful));
  if (filter?.clientId) params.append('clientId', filter.clientId);
  if (filter?.page !== undefined) params.append('page', String(filter.page));
  if (filter?.pageSize !== undefined) params.append('pageSize', String(filter.pageSize));
  const query = params.toString();
  return request.get<LoginAuditPage>(query ? `/login-audits?${query}` : '/login-audits');
};
