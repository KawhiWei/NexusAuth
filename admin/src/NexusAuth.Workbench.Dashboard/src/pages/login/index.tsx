import './style.less';

import { Button, MessagePlugin, Tooltip } from 'tdesign-react';
import { MoonIcon, SunnyIcon } from 'tdesign-icons-react';
import { useEffect, useState } from 'react';

import { checkAuthenticated, setCachedAuthStatus } from '../../router/auth';
import { applyThemeMode, getThemeMode } from '../../theme';
import BrandComponent from '../../components/brand';

const Login = () => {
  const [loading, setLoading] = useState(false);
  const [themeMode, setThemeMode] = useState<'light' | 'dark'>(() => getThemeMode());

  useEffect(() => {
    void checkAuthStatus();
  }, []);

  async function checkAuthStatus() {
    setCachedAuthStatus(false);
    const authenticated = await checkAuthenticated();
    setCachedAuthStatus(authenticated);
    if (authenticated) {
      window.location.href = '/dashboard';
    }
  }

  const handleToggleTheme = () => {
    const nextTheme = themeMode === 'light' ? 'dark' : 'light';
    setThemeMode(nextTheme);
    applyThemeMode(nextTheme);
  };

  const handleLogin = async () => {
    setLoading(true);
    // APISIX owns the OIDC redirect and callback flow. This must be a top-level
    // navigation rather than an XHR request so the browser can follow the SSO redirect.
    window.location.assign('/workbenchproxy/login');
  };

  return (
    <div className={`stitch-login-page stitch-login-page--${themeMode}`}>
      <div className="stitch-login-bg" />

      <header className="stitch-login-header">
        <BrandComponent className="stitch-login-brand" />
        <Tooltip content={themeMode === 'light' ? '切换到暗色主题' : '切换到浅色主题'}>
          <Button
            className="stitch-login-theme-button"
            shape="circle"
            variant="text"
            aria-label={themeMode === 'light' ? '切换到暗色主题' : '切换到浅色主题'}
            icon={themeMode === 'light' ? <MoonIcon /> : <SunnyIcon />}
            onClick={handleToggleTheme}
          />
        </Tooltip>
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
