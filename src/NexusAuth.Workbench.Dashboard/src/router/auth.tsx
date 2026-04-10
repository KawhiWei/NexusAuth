import { Navigate, Outlet, useLocation } from 'react-router-dom';

export const TOKEN_STORAGE_KEY = 'token';

export const hasToken = () => Boolean(localStorage.getItem(TOKEN_STORAGE_KEY));

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

  if (!hasToken()) {
    return <Navigate to={getLoginPath(location)} replace state={{ from: location }} />;
  }

  return <Outlet />;
};

export const RedirectIfAuthenticated = () => {
  const location = useLocation();

  if (hasToken()) {
    const redirect = getSafeRedirectPath(new URLSearchParams(location.search).get('redirect'));
    return <Navigate to={redirect || '/dashboard'} replace />;
  }

  return <Outlet />;
};

export const HomeRedirect = () => {
  return <Navigate to={hasToken() ? '/dashboard' : '/login'} replace />;
};
