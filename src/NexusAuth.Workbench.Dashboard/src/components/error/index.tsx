import { Button } from "tdesign-react"
import { Link } from "react-router-dom"

const ErrorPage = () => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '100%', gap: 16 }}>
      <h1 style={{ fontSize: 72, margin: 0, color: 'var(--td-text-color-primary)' }}>404</h1>
      <p style={{ fontSize: 16, color: 'var(--td-text-color-secondary)' }}>对不起，你访问的页面不存在。</p>
      <Button theme="primary">
        <Link to="/">首页</Link>
      </Button>
    </div>
  )
}
export default ErrorPage
