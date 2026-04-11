import { useEffect, useState } from 'react';
import RootRouterProvider from '../src/router/provider';
import { setCachedAuthStatus, checkAuthenticated } from '../src/router/auth';

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
    <>
      <RootRouterProvider></RootRouterProvider>
    </>
  )
}

export default App
