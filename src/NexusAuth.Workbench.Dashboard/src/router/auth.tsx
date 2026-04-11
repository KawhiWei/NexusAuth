import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { checkAuth } from '../api/login';

let cachedAuthStatus: boolean | null = null;

export const getCachedAuthStatus = () => cachedAuthStatus;

export const setCachedAuthStatus = (status: boolean) => {
  cachedAuthStatus = status;
};

export const checkAuthenticated = async () => {
  try {
    const isAuth = await checkAuth();
    setCachedAuthStatus(isAuth);
    return isAuth;
  } catch {
    setCachedAuthStatus(false);
    return false;
  }
};

export const getSafeRedirectPath = (redirect: string | null | undefined) => {
  if (!redirect || !redirect.startsWith('/') || redirect.startsWith('//')) {
    return null;
  }

  return redirect;
};

const getRedirectPath = (location: { pathname: string; search?: string; hash?: string }) => {
  return `${location.pathname}${location.search || ''}${location.hash || ''}`;
};

const getLoginPath = (location: { pathname: string; search?: string; hash?: string }) => {
  const params = new URLSearchParams({ redirect: getRedirectPath(location) });
  return `/login?${params.toString()}`;
};

export const RequireAuth = () => {
  const location = useLocation();

  if (!getCachedAuthStatus()) {
    return <Navigate to={getLoginPath(location)} replace state={{ from: location }} />;
  }

  return <Outlet />;
};

export const RedirectIfAuthenticated = () => {
  const location = useLocation();

  if (getCachedAuthStatus()) {
    const redirect = getSafeRedirectPath(new URLSearchParams(location.search).get('redirect'));
    return <Navigate to={redirect || '/dashboard'} replace />;
  }

  return <Outlet />;
};

export const HomeRedirect = () => {
  return <Navigate to={getCachedAuthStatus() ? '/dashboard' : '/login'} replace />;
};
