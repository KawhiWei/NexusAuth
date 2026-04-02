type HeroPanelProps = {
  authenticated: boolean
  onLogin: () => Promise<void>
  onLogout: () => Promise<void>
  onLoadProfile: () => Promise<void>
}

export function HeroPanel({ authenticated, onLogin, onLogout, onLoadProfile }: HeroPanelProps) {
  return (
    <section className="hero">
      <div className="hero-card">
        <div className="badge">React + TypeScript + BFF</div>
        <h1>NexusAuth Demo</h1>
        <p>
          这是一个使用 React + TypeScript 重构后的前后分离示例。前端只和 BFF 通信，BFF 负责接入
          NexusAuth 的 OAuth 2.0 + OpenID Connect 授权码登录。
        </p>
        <div className="hero-list">
          <div>前端端口：<strong>5200</strong></div>
          <div>BFF 端口：<strong>5201</strong></div>
          <div>NexusAuth 端口：<strong>5100</strong></div>
        </div>
      </div>

      <div className="panel stack">
        <div>
          <h2>统一登录入口</h2>
          <p className="muted">登录跳转到 NexusAuth，回调由 BFF 在服务端处理，前端通过 Cookie 感知会话状态。</p>
        </div>

        <div className="btn-row">
          {!authenticated && (
            <button className="btn-primary" onClick={() => void onLogin()}>
              使用 NexusAuth 登录
            </button>
          )}

          {authenticated && (
            <>
              <button className="btn-secondary" onClick={() => void onLoadProfile()}>
                读取业务资料
              </button>
              <button className="btn-danger" onClick={() => void onLogout()}>
                退出登录
              </button>
            </>
          )}
        </div>
      </div>
    </section>
  )
}
