import { useEffect, useState } from 'react';
import { ConfigProvider } from 'tdesign-react';
import RootRouterProvider from '../src/router/provider';
import { setCachedAuthStatus, checkAuthenticated } from '../src/router/auth';

const globalConfig = {
  table: {
    size: 'small' as const,
  },
};

const App = () => {
  const [initialized, setInitialized] = useState(false);

  useEffect(() => {
    checkAuthenticated().then((isAuth) => {
      setCachedAuthStatus(isAuth);
      setInitialized(true);
    });
  }, []);

  if (!initialized) {
    return null;
  }

  return (
    <ConfigProvider globalConfig={globalConfig}>
      <RootRouterProvider></RootRouterProvider>
    </ConfigProvider>
  )
}

export default App
