import './style.less';

import { Button, MessagePlugin } from 'tdesign-react';
import { useEffect, useState } from 'react';

import { checkAuthenticated, setCachedAuthStatus } from '../../router/auth';
import { startLogin } from '../../api/login';
import { getThemeMode } from '../../theme';

const Login = () => {
  const [loading, setLoading] = useState(false);
  const [themeMode] = useState<'light' | 'dark'>(() => getThemeMode());

  useEffect(() => {
    void checkAuthStatus();
  }, []);

  async function checkAuthStatus() {
    const authenticated = await checkAuthenticated();
    setCachedAuthStatus(authenticated);
    if (authenticated) {
      window.location.href = '/dashboard';
    }
  }

  const handleLogin = async () => {
    try {
      setLoading(true);
      const result = await startLogin();
      
      if (result.authorizeUrl) {
        window.location.href = result.authorizeUrl;
      }
    } catch (error) {
      MessagePlugin.error(error instanceof Error ? error.message : '登录失败');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={`stitch-login-page stitch-login-page--${themeMode}`}>
      <div className="stitch-login-bg" />
      <div className="stitch-login-orb stitch-login-orb-primary" />
      <div className="stitch-login-orb stitch-login-orb-secondary" />

      <header className="stitch-login-header">
        <div className="stitch-login-brand">NexusAuth Workbench</div>
      </header>

      <main className="stitch-login-main">
        <section className="stitch-login-card">
          <div className="stitch-login-identity">
            <h1 className="stitch-login-title">欢迎回来</h1>
            <p className="stitch-login-subtitle">点击下方按钮使用 NexusAuth 账号登录</p>
          </div>

          <Button className="stitch-login-submit" theme="primary" size="large" loading={loading} onClick={handleLogin} block>
            使用 NexusAuth 登录
          </Button>
        </section>

        <div className="stitch-login-status">
          <div className="stitch-login-status-item">
            <span className="stitch-login-status-dot" />
            <span>系统在线</span>
          </div>
        </div>
      </main>

      <footer className="stitch-login-footer">
        <div>© 2024 NexusAuth. 保留所有权利。</div>
      </footer>

      <div className="stitch-login-side-visual" />
    </div>
  );
};

export default Login;