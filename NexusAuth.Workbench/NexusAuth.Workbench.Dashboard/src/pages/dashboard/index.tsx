import './style.less';

import { Area, Column, Line, Pie } from '@ant-design/charts';
import { useEffect, useMemo, useState } from 'react';
import { Card, Col, Progress, Row, Space, Tag } from 'tdesign-react';

import { isDarkTheme, subscribeThemeMode } from '../../theme';

type MetricCard = {
  key: string;
  title: string;
  value: string;
  trend: string;
  trendUp: boolean;
  progress: number;
  status: 'success' | 'warning' | 'error';
};

const metricCards: MetricCard[] = [
  { key: 'm1', title: '今日交易额', value: '¥ 2,348,900', trend: '+12.6%', trendUp: true, progress: 78, status: 'success' },
  { key: 'm2', title: '活跃用户', value: '18,426', trend: '+6.8%', trendUp: true, progress: 64, status: 'success' },
  { key: 'm3', title: '订单完成率', value: '93.2%', trend: '-1.4%', trendUp: false, progress: 93, status: 'warning' },
  { key: 'm4', title: '异常告警', value: '23', trend: '-18.0%', trendUp: true, progress: 32, status: 'error' },
];

const weeklyUserData = Array.from({ length: 12 }).map((_, index) => ({
  week: `W${index + 1}`,
  value: 1200 + ((index * 287) % 1400),
}));

const tradeTrendData = Array.from({ length: 30 }).map((_, index) => ({
  day: `03-${String(index + 1).padStart(2, '0')}`,
  amount: 82 + Math.round(Math.sin((index + 2) / 4) * 20 + (index % 8) * 1.8),
}));

const channelData = [
  { type: '小程序', value: 36 },
  { type: '官网', value: 28 },
  { type: 'APP', value: 22 },
  { type: '线下渠道', value: 14 },
];

const conversionDates = Array.from({ length: 12 }).map((_, index) => `2026-${String(index + 1).padStart(2, '0')}`);
const conversionData = conversionDates.flatMap((date, index) => {
  const base = 600 + Math.round(Math.sin((index + 1) / 2.6) * 80);
  return [
    { date, type: '站内广告', value: base + 220 },
    { date, type: '自然搜索', value: base + 280 },
    { date, type: '活动推荐', value: base + 170 },
    { date, type: '渠道分销', value: base + 120 },
  ];
});

const alerts = [
  { level: '高', text: '支付网关响应时间升高，P95 超过 480ms', time: '2 分钟前' },
  { level: '中', text: '库存同步任务延迟，待处理队列 129 条', time: '14 分钟前' },
  { level: '低', text: '短信服务重试成功率下降 3.2%', time: '27 分钟前' },
  { level: '中', text: '新渠道回调失败率高于阈值', time: '39 分钟前' },
];

const Dashboard = () => {
  const [isDark, setIsDark] = useState(() => isDarkTheme());

  useEffect(() => {
    return subscribeThemeMode((theme) => {
      setIsDark(theme === 'dark');
    });
  }, []);

  const chartColors = useMemo(
    () => ({
      column: isDark ? '#5f8bff' : '#3f7bff',
      dualColumn: isDark ? '#6f99ff' : '#4f7bff',
      dualLine: isDark ? '#53d68f' : '#2dbf7f',
      pie: isDark ? ['#77a2ff', '#58d49c', '#f6bf5c', '#b493ff'] : ['#4f7bff', '#2dbf7f', '#f2a93b', '#8a65ff'],
      multiLine: isDark ? ['#7ea6ff', '#7de2b0', '#f8cd6b', '#c4a0ff'] : ['#3f7bff', '#1dbf84', '#f3a93e', '#8f63ff'],
    }),
    [isDark],
  );

  return (
    <div className="dashboard-page">
      <div className="dashboard-header">
        <div className="dashboard-title">业务运营仪表盘</div>
        <div className="dashboard-subtitle">汇总关键指标、趋势变化与渠道质量，快速定位业务状态</div>
      </div>

      <Row gutter={[16, 16]}>
        {metricCards.map((metric) => (
          <Col key={metric.key} xs={12} sm={12} md={12} lg={6}>
            <Card className="dashboard-metric-card" bordered>
              <div className="metric-title">{metric.title}</div>
              <div className="metric-value-row">
                <span className="metric-value">{metric.value}</span>
                <Tag theme={metric.trendUp ? 'success' : 'danger'} variant="light">
                  {metric.trend}
                </Tag>
              </div>
              <Progress percentage={metric.progress} status={metric.status} size="small" />
            </Card>
          </Col>
        ))}
      </Row>

      <Row gutter={[16, 16]} className="dashboard-chart-row">
        <Col xs={24} lg={14}>
          <Card title="近30日交易趋势" bordered className="dashboard-card">
            <Area
              height={280}
              data={tradeTrendData}
              xField="day"
              yField="amount"
              axis={{ y: { labelFormatter: '~s' } }}
              tooltip={{ title: 'day' }}
            />
          </Card>
        </Col>
        <Col xs={24} lg={10}>
          <Card title="渠道占比" bordered className="dashboard-card">
            <Pie
              height={280}
              data={channelData}
              angleField="value"
              colorField="type"
              theme={isDark ? 'classicDark' : 'classic'}
              color={chartColors.pie}
              innerRadius={0.62}
              legend={{ position: 'bottom' }}
              label={{
                text: 'type',
                position: 'outside',
                style: {
                  fill: isDark ? '#d7deef' : '#1f2d3d',
                  fontSize: 12,
                  fontWeight: 500,
                },
              }}
              tooltip={{ items: [{ name: '占比', field: 'value' }] }}
            />
          </Card>
        </Col>
      </Row>

      <Row gutter={[16, 16]} className="dashboard-chart-row">
        <Col xs={24} lg={14}>
          <Card title="新增用户（周）" bordered className="dashboard-card">
            <Column
              height={280}
              data={weeklyUserData}
              xField="week"
              yField="value"
              color={chartColors.column}
              columnStyle={{ radius: [6, 6, 0, 0] }}
              axis={{ y: { labelFormatter: '~s' } }}
            />
          </Card>
        </Col>
        <Col xs={24} lg={10}>
          <Card title="订单来源占比趋势（归一化）" bordered className="dashboard-card">
            <Line
              height={280}
              data={[...conversionData].sort((a, b) => {
                if (a.type === b.type) {
                  return a.date.localeCompare(b.date);
                }
                return a.type.localeCompare(b.type);
              })}
              xField="date"
              yField="value"
              colorField="type"
              seriesField="type"
              theme={isDark ? 'classicDark' : 'classic'}
              color={chartColors.multiLine}
              normalize
              shapeField="smooth"
              point={{ shapeField: 'circle', sizeField: 2.5 }}
              style={{ lineWidth: 2 }}
              interaction={{ tooltip: { marker: true } }}
              axis={{ y: { title: '占比', labelFormatter: (v: string) => `${Math.round(Number(v) * 100)}%` } }}
              tooltip={{
                title: 'date',
                items: [
                  {
                    channel: 'y',
                    valueFormatter: (value: number | string) => `${(Number(value) * 100).toFixed(1)}%`,
                  },
                ],
              }}
              legend={{ position: 'top' }}
            />
          </Card>
        </Col>
      </Row>

      <Card title="实时告警" bordered className="dashboard-card dashboard-alert-card">
        <Space direction="vertical" size={12} style={{ width: '100%' }}>
          {alerts.map((item, index) => (
            <div key={`${item.text}-${index}`} className="dashboard-alert-item">
              <div className="dashboard-alert-main">
                <Tag theme={item.level === '高' ? 'danger' : item.level === '中' ? 'warning' : 'primary'} variant="light-outline">
                  {item.level}
                </Tag>
                <span className="dashboard-alert-text">{item.text}</span>
              </div>
              <span className="dashboard-alert-time">{item.time}</span>
            </div>
          ))}
        </Space>
      </Card>
    </div>
  );
};

export default Dashboard;
