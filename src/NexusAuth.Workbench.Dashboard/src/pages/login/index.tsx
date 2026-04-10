import './style.less';

import { FingerprintIcon, LockOnIcon, LoginIcon, MoonIcon, QrcodeIcon, SunnyIcon, UserIcon, UserVisibleIcon } from 'tdesign-icons-react';
import { Button, Checkbox, Form, Input, MessagePlugin, type SubmitContext } from 'tdesign-react';
import { useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';

import { mockLogin } from '../../api/login';
import { router } from '../../router';
import { getSafeRedirectPath, TOKEN_STORAGE_KEY } from '../../router/auth';
import { applyThemeMode, getThemeMode } from '../../theme';

type LoginForm = {
  username: string;
  password: string;
  remember: boolean;
};

const Login = () => {
  const location = useLocation();
  const redirect = getSafeRedirectPath(new URLSearchParams(location.search).get('redirect'));
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();
  const [themeMode, setThemeMode] = useState<'light' | 'dark'>(() => getThemeMode());

  useEffect(() => {
    if (localStorage.getItem(TOKEN_STORAGE_KEY)) {
      router.navigate(redirect || '/dashboard', { replace: true });
      return;
    }

    applyThemeMode(themeMode);
  }, [redirect, themeMode]);

  const handleToggleTheme = () => {
    setThemeMode((prev) => (prev === 'dark' ? 'light' : 'dark'));
  };

  const handleSubmit = async (context: SubmitContext) => {
    if (context.validateResult !== true) {
      return;
    }

    const values = (context.fields || {}) as LoginForm;
    setLoading(true);
    try {
      const result = await mockLogin({
        username: values.username,
        password: values.password,
      });

      localStorage.setItem(TOKEN_STORAGE_KEY, result.token);
      localStorage.setItem('userInfo', JSON.stringify(result.userInfo));
      localStorage.setItem('rememberLogin', values.remember ? '1' : '0');

      const redirectPath = (location.state as { from?: { pathname?: string; search?: string; hash?: string } } | null)?.from;
      const nextPath = redirect || (redirectPath?.pathname ? `${redirectPath.pathname}${redirectPath.search || ''}${redirectPath.hash || ''}` : '/dashboard');

      router.navigate(nextPath, { replace: true });
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
        <div className="stitch-login-brand">React-Design-Template</div>
        <div className="stitch-login-header-actions">
          <button type="button" className="stitch-login-header-btn stitch-login-theme-btn" onClick={handleToggleTheme}>
            {themeMode === 'dark' ? <SunnyIcon size="16px" /> : <MoonIcon size="16px" />}
            <span>{themeMode === 'dark' ? '亮色模式' : '暗色模式'}</span>
          </button>
          <button type="button" className="stitch-login-header-btn">帮助</button>
          <button type="button" className="stitch-login-header-btn">设置</button>
        </div>
      </header>

      <main className="stitch-login-main">
        <section className="stitch-login-card">
          <div className="stitch-login-identity">
            {themeMode === 'light' ? <div className="stitch-login-accent-bar" /> : null}
            <h1 className="stitch-login-title">欢迎回来</h1>
            <p className="stitch-login-subtitle">请输入您的凭据以访问 React-Design-Template 管理后台</p>
            <p className="stitch-login-tips">Mock 账号：admin，密码：123456</p>
          </div>

          <Form
            form={form}
            className="stitch-login-form"
            initialData={{ username: 'admin', password: '123456', remember: true }}
            colon={false}
            labelWidth={0}
            onSubmit={handleSubmit}
          >
            <Form.FormItem
              name="username"
              label="用户名/邮箱"
              labelAlign="top"
              className="stitch-login-form-item"
              rules={[{ required: true, message: '请输入用户名/邮箱' }]}
            >
              <Input
                size="large"
                prefixIcon={<UserIcon />}
                placeholder="admin@react-template.tech"
                clearable
              />
            </Form.FormItem>

            <Form.FormItem
              name="password"
              label="密码"
              labelAlign="top"
              className="stitch-login-form-item"
              rules={[{ required: true, message: '请输入密码' }]}
            >
              <Input
                type="password"
                size="large"
                prefixIcon={<LockOnIcon />}
                suffixIcon={<UserVisibleIcon />}
                placeholder="••••••••"
                clearable
              />
            </Form.FormItem>

            <Form.FormItem name="remember" className="stitch-login-form-item stitch-login-form-item-inline">
              <div className="stitch-login-meta-row">
                <Checkbox>记住我</Checkbox>
                {themeMode === 'dark' ? <button type="button" className="stitch-login-link-btn">忘记密码?</button> : null}
              </div>
            </Form.FormItem>

            <Form.FormItem className="stitch-login-form-item">
              <Button className="stitch-login-submit" type="submit" loading={loading} block>
                <span>登录</span>
                <LoginIcon size="18px" />
              </Button>
            </Form.FormItem>
          </Form>

          <div className="stitch-login-divider">
            <span>{themeMode === 'dark' ? '其他访问方式' : '或者通过以下方式继续'}</span>
          </div>

          <div className="stitch-login-alt-actions">
            <button type="button" className="stitch-login-alt-btn">
              {themeMode === 'dark' ? <FingerprintIcon size="18px" /> : <LoginIcon size="18px" />}
              <span>{themeMode === 'dark' ? '生物识别' : 'SSO 登录'}</span>
            </button>
            <button type="button" className="stitch-login-alt-btn">
              <QrcodeIcon size="18px" />
              <span>扫码登录</span>
            </button>
          </div>
        </section>

        <div className="stitch-login-status">
          <div className="stitch-login-status-item">
            <span className="stitch-login-status-dot" />
            <span>{themeMode === 'dark' ? '系统在线' : '安全可用'}</span>
          </div>
          <div className="stitch-login-status-item">
            <span>v4.0.2 Stable</span>
          </div>
        </div>
      </main>

      <footer className="stitch-login-footer">
        <div>© 2024 React-Design-Template. 保留所有权利。</div>
        <div className="stitch-login-footer-links">
          <button type="button">隐私政策</button>
          <button type="button">服务条款</button>
          <button type="button">联系我们</button>
        </div>
      </footer>

      <div className="stitch-login-side-visual" />
    </div>
  );
};

export default Login;
