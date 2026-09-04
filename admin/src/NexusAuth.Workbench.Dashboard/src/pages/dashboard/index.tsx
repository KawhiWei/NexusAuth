import './style.less';

import { Column, Line, Pie } from '@ant-design/charts';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Select, Tag } from 'tdesign-react';
import {
  ChartLineDataIcon,
  CheckCircleFilledIcon,
  ChevronRightIcon,
  ErrorCircleFilledIcon,
  RefreshIcon,
  ShieldErrorFilledIcon,
  TrendingDownIcon,
  TrendingUpIcon,
  UserIcon,
} from 'tdesign-icons-react';

import { isDarkTheme, subscribeThemeMode } from '../../theme';

type MetricIconKind = 'requests' | 'success' | 'users' | 'risk';
type TrendTone = 'positive' | 'negative';

type MetricCard = {
  key: string;
  title: string;
  value: string;
  trend: string;
  trendDirection: 'up' | 'down';
  trendTone: TrendTone;
  icon: MetricIconKind;
  helper: string;
};

type SecurityEvent = {
  level: '高' | '中' | '低';
  title: string;
  source: string;
  time: string;
};

const metricCards: MetricCard[] = [
  {
    key: 'requests',
    title: '认证请求',
    value: '128,640',
    trend: '+12.4%',
    trendDirection: 'up',
    trendTone: 'positive',
    icon: 'requests',
    helper: '较上一周期',
  },
  {
    key: 'success-rate',
    title: '认证成功率',
    value: '98.72%',
    trend: '+0.36%',
    trendDirection: 'up',
    trendTone: 'positive',
    icon: 'success',
    helper: '较上一周期',
  },
  {
    key: 'users',
    title: '活跃用户',
    value: '18,426',
    trend: '+6.8%',
    trendDirection: 'up',
    trendTone: 'positive',
    icon: 'users',
    helper: '较上一周期',
  },
  {
    key: 'risk-events',
    title: '高风险事件',
    value: '23',
    trend: '-18.0%',
    trendDirection: 'down',
    trendTone: 'positive',
    icon: 'risk',
    helper: '较上一周期',
  },
];

const authTrendData = [
  { date: '05-27', status: '成功', value: 15680 },
  { date: '05-28', status: '成功', value: 17420 },
  { date: '05-29', status: '成功', value: 16850 },
  { date: '05-30', status: '成功', value: 19380 },
  { date: '05-31', status: '成功', value: 18410 },
  { date: '06-01', status: '成功', value: 20560 },
  { date: '06-02', status: '成功', value: 20340 },
  { date: '05-27', status: '失败', value: 260 },
  { date: '05-28', status: '失败', value: 310 },
  { date: '05-29', status: '失败', value: 284 },
  { date: '05-30', status: '失败', value: 352 },
  { date: '05-31', status: '失败', value: 326 },
  { date: '06-01', status: '失败', value: 382 },
  { date: '06-02', status: '失败', value: 348 },
];

const authResultData = [
  { type: '成功', value: 98.72 },
  { type: '凭据错误', value: 0.81 },
  { type: '账号锁定', value: 0.29 },
  { type: 'MFA 失败', value: 0.18 },
];

const applicationRankData = [
  { application: 'Workbench Portal', value: 36.8 },
  { application: 'Mobile App', value: 28.4 },
  { application: 'Partner BFF', value: 21.7 },
  { application: 'Open API', value: 16.9 },
  { application: 'CLI', value: 8.6 },
];

const securityEvents: SecurityEvent[] = [
  { level: '高', title: '检测到异常登录尝试', source: '185.22.64.18', time: '8 分钟前' },
  { level: '中', title: 'Partner BFF 签名校验失败', source: 'Partner BFF', time: '24 分钟前' },
  { level: '低', title: '新设备完成 MFA 验证', source: '用户 10482', time: '41 分钟前' },
  { level: '中', title: '账号触发登录频率限制', source: '103.76.12.90', time: '1 小时前' },
];

const rangeOptions = [
  { label: '近 7 天', value: '7d' },
  { label: '近 30 天', value: '30d' },
  { label: '近 90 天', value: '90d' },
];

const getMetricIcon = (kind: MetricIconKind) => {
  switch (kind) {
    case 'requests':
      return <ChartLineDataIcon />;
    case 'success':
      return <CheckCircleFilledIcon />;
    case 'users':
      return <UserIcon />;
    case 'risk':
      return <ShieldErrorFilledIcon />;
    default:
      return <ChartLineDataIcon />;
  }
};

const getEventTheme = (level: SecurityEvent['level']): 'danger' | 'warning' | 'success' => {
  if (level === '高') {
    return 'danger';
  }
  if (level === '中') {
    return 'warning';
  }
  return 'success';
};

const formatUpdatedAt = (value: Date) => value.toLocaleTimeString('zh-CN', {
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
});

const Dashboard = () => {
  const [isDark, setIsDark] = useState(() => isDarkTheme());
  const [dateRange, setDateRange] = useState('7d');
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [lastUpdated, setLastUpdated] = useState(() => new Date());
  const refreshTimer = useRef<number | null>(null);

  useEffect(() => {
    return subscribeThemeMode((theme) => {
      setIsDark(theme === 'dark');
    });
  }, []);

  useEffect(() => {
    return () => {
      if (refreshTimer.current !== null) {
        window.clearTimeout(refreshTimer.current);
      }
    };
  }, []);

  const chartColors = useMemo(
    () => ({
      trend: isDark ? ['#71a2ff', '#5bdb9d'] : ['#2563eb', '#16845b'],
      bar: isDark ? '#71a2ff' : '#3f7bff',
      pie: isDark ? ['#71a2ff', '#5bdb9d', '#f0b45d', '#b493ff'] : ['#2563eb', '#16845b', '#b76711', '#805ad5'],
      axisText: isDark ? '#82909e' : '#7b8794',
    }),
    [isDark],
  );

  const selectedRangeLabel = rangeOptions.find((option) => option.value === dateRange)?.label || '近 7 天';

  const handleRefresh = () => {
    if (isRefreshing) {
      return;
    }

    setIsRefreshing(true);
    refreshTimer.current = window.setTimeout(() => {
      setLastUpdated(new Date());
      setIsRefreshing(false);
      refreshTimer.current = null;
    }, 650);
  };

  return (
    <div className="dashboard-page">
      <header className="dashboard-header">
        <div className="dashboard-heading">
          <h1 className="dashboard-title">身份与访问态势</h1>
          <p className="dashboard-subtitle">实时监测组织内认证流量、访问风险与协议健康度</p>
          <div className="dashboard-health" aria-label="协议健康度：稳定">
            <span className="dashboard-health-dot" aria-hidden="true" />
            <span>协议健康度</span>
            <strong>稳定</strong>
            <span className="dashboard-health-detail">OIDC · OAuth 2.0 · SAML 2.0</span>
          </div>
        </div>
        <div className="dashboard-controls">
          <Select
            className="dashboard-range-select"
            value={dateRange}
            options={rangeOptions}
            onChange={(value) => setDateRange(String(value))}
            aria-label="选择统计时间范围"
          />
          <Button
            variant="outline"
            icon={<RefreshIcon />}
            loading={isRefreshing}
            onClick={handleRefresh}
          >
            刷新
          </Button>
          <span className="dashboard-updated">更新于 {formatUpdatedAt(lastUpdated)}</span>
        </div>
      </header>

      <section className="dashboard-metrics" aria-label="核心身份指标">
        {metricCards.map((metric) => (
          <article key={metric.key} className="dashboard-metric-card">
            <div className="metric-card-top">
              <span className="metric-title">{metric.title}</span>
              <span className={`metric-icon metric-icon-${metric.icon}`} aria-hidden="true">
                {getMetricIcon(metric.icon)}
              </span>
            </div>
            <div className="metric-value-row">
              <span className="metric-value">{metric.value}</span>
              <span className={`metric-trend is-${metric.trendTone}`}>
                {metric.trendDirection === 'up' ? <TrendingUpIcon /> : <TrendingDownIcon />}
                {metric.trend}
              </span>
            </div>
            <div className="metric-helper">{metric.helper}</div>
          </article>
        ))}
      </section>

      <section className="dashboard-main-grid" aria-label="认证趋势与结果分布">
        <article className="dashboard-panel dashboard-trend-panel">
          <div className="panel-header">
            <div>
              <h2 className="panel-title">认证请求趋势</h2>
              <p className="panel-caption">成功与失败请求量</p>
            </div>
            <Tag theme="primary" variant="light-outline">{selectedRangeLabel}</Tag>
          </div>
          <div className="dashboard-chart dashboard-trend-chart">
            <Line
              height={300}
              data={authTrendData}
              xField="date"
              yField="value"
              colorField="status"
              seriesField="status"
              theme={isDark ? 'classicDark' : 'classic'}
              color={chartColors.trend}
              shapeField="smooth"
              point={{ shapeField: 'circle', sizeField: 3 }}
              style={{ lineWidth: 2 }}
              legend={{ position: 'top' }}
              interaction={{ tooltip: { marker: true } }}
              axis={{
                x: { title: '日期' },
                y: {
                  title: '请求数',
                  labelFormatter: (value: string) => `${(Number(value) / 1000).toFixed(0)}k`,
                },
              }}
              tooltip={{
                title: 'date',
                items: [
                  {
                    channel: 'y',
                    valueFormatter: (value: number | string) => `${Number(value).toLocaleString('zh-CN')} 次`,
                  },
                ],
              }}
            />
          </div>
        </article>

        <article className="dashboard-panel dashboard-result-panel">
          <div className="panel-header">
            <div>
              <h2 className="panel-title">认证结果分布</h2>
              <p className="panel-caption">按请求结果统计</p>
            </div>
          </div>
          <div className="dashboard-donut-wrap">
            <Pie
              height={248}
              data={authResultData}
              angleField="value"
              colorField="type"
              theme={isDark ? 'classicDark' : 'classic'}
              color={chartColors.pie}
              innerRadius={0.72}
              legend={{ position: 'bottom' }}
              label={false}
              tooltip={{
                items: [
                  {
                    name: '占比',
                    field: 'value',
                    valueFormatter: (value: number | string) => `${Number(value).toFixed(2)}%`,
                  },
                ],
              }}
            />
            <div className="donut-center" aria-label="认证成功率 98.72%">
              <strong>98.72%</strong>
              <span>认证成功率</span>
            </div>
          </div>
        </article>
      </section>

      <section className="dashboard-secondary-grid" aria-label="应用调用排行与近期安全事件">
        <article className="dashboard-panel dashboard-application-panel">
          <div className="panel-header">
            <div>
              <h2 className="panel-title">应用认证调用排行</h2>
              <p className="panel-caption">{selectedRangeLabel}认证请求量（千次）</p>
            </div>
          </div>
          <div className="dashboard-chart dashboard-application-chart">
            <Column
              height={276}
              data={applicationRankData}
              xField="application"
              yField="value"
              theme={isDark ? 'classicDark' : 'classic'}
              color={chartColors.bar}
              columnStyle={{ radius: [5, 5, 0, 0] }}
              label={{
                text: 'value',
                position: 'top',
                formatter: (value: number | string) => `${Number(value).toFixed(1)}k`,
              }}
              axis={{
                x: { title: '应用' },
                y: {
                  title: '调用量（千次）',
                  labelFormatter: (value: string) => `${value}k`,
                },
              }}
              tooltip={{
                items: [
                  {
                    name: '调用量',
                    field: 'value',
                    valueFormatter: (value: number | string) => `${Number(value).toFixed(1)}k 次`,
                  },
                ],
              }}
            />
          </div>
        </article>

        <article className="dashboard-panel dashboard-security-panel">
          <div className="panel-header">
            <div>
              <h2 className="panel-title">近期安全事件</h2>
              <p className="panel-caption">需要关注的访问与认证活动</p>
            </div>
            <ErrorCircleFilledIcon className="panel-header-icon" aria-hidden="true" />
          </div>
          <div className="security-event-list">
            {securityEvents.map((event) => (
              <div key={`${event.title}-${event.time}`} className="security-event-item">
                <Tag theme={getEventTheme(event.level)} variant="light-outline" size="small">
                  {event.level}
                </Tag>
                <div className="security-event-content">
                  <div className="security-event-title">{event.title}</div>
                  <div className="security-event-meta">
                    <span>{event.source}</span>
                    <span>{event.time}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
          <Button className="security-event-more" variant="text" suffix={<ChevronRightIcon />}>
            查看全部
          </Button>
        </article>
      </section>
    </div>
  );
};

export default Dashboard;
