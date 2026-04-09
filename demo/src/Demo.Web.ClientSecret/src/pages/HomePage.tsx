import { DataCard } from '../components/DataCard'
import { HeroPanel } from '../components/HeroPanel'
import { StatusAlert } from '../components/StatusAlert'
import { useAuthSession } from '../hooks/useAuthSession'

export function HomePage() {
  const {
    loading,
    authenticated,
    user,
    profilePayload,
    status,
    handleLogin,
    handleLogout,
    handleLoadProfile,
  } = useAuthSession()

  return (
    <div className="shell">
      <HeroPanel
        authenticated={authenticated}
        onLogin={handleLogin}
        onLogout={handleLogout}
        onLoadProfile={handleLoadProfile}
      />

      <div className="status-wrap">
        <StatusAlert status={status} loading={loading} />
      </div>

      <section className="card-grid">
        <DataCard title="当前用户">{user ? JSON.stringify(user, null, 2) : '尚未登录'}</DataCard>
        <DataCard title="业务接口返回">{profilePayload}</DataCard>
        <DataCard title="测试说明">1. 先运行 NexusAuth.Host
2. 执行最新 seed.sql
3. 启动 Demo.Bff.ClientSecret
4. 启动当前 React + TS 前端
5. 使用测试账号 alice / Pass@123 登录</DataCard>
      </section>
    </div>
  )
}
