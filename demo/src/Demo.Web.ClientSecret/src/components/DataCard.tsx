import type { ReactNode } from 'react'

type DataCardProps = {
  title: string
  children: ReactNode
}

export function DataCard({ title, children }: DataCardProps) {
  return (
    <article className="card">
      <h3>{title}</h3>
      <div className="token-box">{children}</div>
    </article>
  )
}
