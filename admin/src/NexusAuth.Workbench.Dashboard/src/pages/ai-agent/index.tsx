import './style.less';
import '@tdesign-react/chat/es/style/index.js';

import { ChatBot } from '@tdesign-react/chat';
import { AddIcon } from 'tdesign-icons-react';
import { Button, Empty, Menu, Space } from 'tdesign-react';
import { useMemo, useRef, useState } from 'react';
import type { ChatMessagesData } from 'tdesign-web-components/lib/chat-engine/type';

type ChatBotProps = React.ComponentProps<typeof ChatBot>;
type MessageChangeEvent = Parameters<NonNullable<ChatBotProps['onMessageChange']>>[0];

type ChatSession = {
  id: string;
  title: string;
  updatedAt: number;
  messages: ChatMessagesData[];
};

type ProductConfigAnswer = {
  summary: string;
  displayText: string;
  productInfo: {
    environment: string;
    resourceCode: string;
    productCode: string;
    financeCode: string;
    goldToadCode: string;
  };
  mappings: Array<{
    sceneType: string;
    sceneName: string;
    receiptType: string;
    receiptName: string;
  }>;
};

type StructuredChatEvent =
  | { event: 'meta'; data: ProductConfigAnswer }
  | { event: 'text'; data: string }
  | { event: 'done'; data: { rawAnswer?: string } }
  | { event: 'error'; data: { message?: string } };

const createId = () => `${Date.now()}-${Math.random().toString(16).slice(2)}`;

const readMessageText = (message: ChatMessagesData) => {
  if (!Array.isArray(message.content)) {
    return '';
  }

  for (const item of message.content) {
    if ((item.type === 'text' || item.type === 'markdown') && typeof item.data === 'string') {
      const normalized = item.data.trim();
      if (normalized.length > 0) {
        return normalized;
      }
    }
  }

  return '';
};

const readSessionTitle = (messages: ChatMessagesData[]) => {
  for (const message of messages) {
    if (message.role !== 'user') {
      continue;
    }
    const text = readMessageText(message);
    if (text) {
      return text.length > 20 ? `${text.slice(0, 20)}...` : text;
    }
  }

  return '新会话';
};

const normalizeMarkdownText = (text: string) => {
  return text
    .replace(/\r\n/g, '\n')
    .replace(/\\r\\n/g, '\n')
    .replace(/\\n/g, '\n')
    .replace(/\\t/g, '  ')
    .trim();
};

const parseStructuredDto = (payload: unknown): ProductConfigAnswer | null => {
  const source = (() => {
    if (typeof payload === 'string') {
      try {
        return JSON.parse(payload) as unknown;
      } catch {
        return null;
      }
    }
    return payload;
  })();

  if (!source || typeof source !== 'object') {
    return null;
  }

  const dto = source as {
    summary?: unknown;
    displayText?: unknown;
    productInfo?: Record<string, unknown>;
    mappings?: Array<Record<string, unknown>>;
  };

  return {
    summary: typeof dto.summary === 'string' ? dto.summary : '',
    displayText: typeof dto.displayText === 'string' ? dto.displayText : '',
    productInfo: {
      environment: String(dto.productInfo?.environment ?? ''),
      resourceCode: String(dto.productInfo?.resourceCode ?? ''),
      productCode: String(dto.productInfo?.productCode ?? ''),
      financeCode: String(dto.productInfo?.financeCode ?? ''),
      goldToadCode: String(dto.productInfo?.goldToadCode ?? ''),
    },
    mappings: Array.isArray(dto.mappings)
      ? dto.mappings.map((item) => ({
          sceneType: String(item.sceneType ?? ''),
          sceneName: String(item.sceneName ?? ''),
          receiptType: String(item.receiptType ?? ''),
          receiptName: String(item.receiptName ?? ''),
        }))
      : [],
  };
};

const toStructuredMarkdown = (answer: ProductConfigAnswer) => {
  const sections: string[] = [];

  if (answer.summary) {
    sections.push('### 查询结论', `> ${answer.summary}`);
  }

  const productInfoRows = [
    ['环境', answer.productInfo.environment],
    ['资源编码', answer.productInfo.resourceCode],
    ['产品编码', answer.productInfo.productCode],
    ['财务产品码', answer.productInfo.financeCode],
    ['金蟾产品码', answer.productInfo.goldToadCode],
  ].filter(([, value]) => Boolean(value));

  if (productInfoRows.length > 0) {
    sections.push(
      '### 产品信息',
      [
        '| 字段 | 值 |',
        '| --- | --- |',
        ...productInfoRows.map(([label, value]) => `| ${label} | ${value} |`),
      ].join('\n'),
    );
  }

  if (answer.mappings.length > 0) {
    sections.push(
      '### 场景映射',
      [
        '| 场景编码 | 场景名称 | 单据类型编码 | 单据类型名称 |',
        '| --- | --- | --- | --- |',
        ...answer.mappings.map(
          (row) =>
            `| ${row.sceneType || '-'} | ${row.sceneName || '-'} | ${row.receiptType || '-'} | ${row.receiptName || '-'} |`,
        ),
      ].join('\n'),
    );
  }

  if (sections.length === 0 && answer.displayText) {
    sections.push(normalizeMarkdownText(answer.displayText));
  }

  return sections.join('\n\n');
};

const formatTime = (timestamp: number) => new Date(timestamp).toLocaleString();

const parseStructuredEvent = (payload: unknown): StructuredChatEvent | null => {
  if (!payload || typeof payload !== 'object') {
    return null;
  }

  const event = payload as { event?: unknown; data?: unknown };
  if (event.event === 'meta') {
    const answer = parseStructuredDto(event.data);
    return answer ? { event: 'meta', data: answer } : null;
  }

  if (event.event === 'text' && typeof event.data === 'string') {
    return { event: 'text', data: event.data };
  }

  if (event.event === 'done' && event.data && typeof event.data === 'object') {
    return { event: 'done', data: event.data as { rawAnswer?: string } };
  }

  if (event.event === 'error' && event.data && typeof event.data === 'object') {
    return { event: 'error', data: event.data as { message?: string } };
  }

  return null;
};

const AIAgent = () => {
  const [sessions, setSessions] = useState<ChatSession[]>(() => {
    const initialId = createId();
    return [
      {
        id: initialId,
        title: '新会话',
        updatedAt: Date.now(),
        messages: [],
      },
    ];
  });
  const [activeSessionId, setActiveSessionId] = useState(() => sessions[0].id);
  const [streamTextMap, setStreamTextMap] = useState<Record<string, string>>({});
  const [isChatScrolling, setIsChatScrolling] = useState(false);
  const scrollTimerRef = useRef<number | null>(null);

  const activeSession = useMemo(
    () => sessions.find((session) => session.id === activeSessionId) ?? sessions[0],
    [activeSessionId, sessions],
  );

  const messageProps = useMemo(
    () => ({
      assistant: {
        placement: 'left' as const,
      },
      user: {
        placement: 'right' as const,
      },
    }),
    [],
  );

  const handleCreateSession = () => {
    const newSession: ChatSession = {
      id: createId(),
      title: '新会话',
      updatedAt: Date.now(),
      messages: [],
    };
    setSessions((prev) => [newSession, ...prev]);
    setStreamTextMap((prev) => ({ ...prev, [newSession.id]: '' }));
    setActiveSessionId(newSession.id);
  };

  const handleSelectSession = (sessionId: string) => {
    setActiveSessionId(sessionId);
  };

  const handleMessageChange = (event: MessageChangeEvent) => {
    const nextMessages = event.detail as ChatMessagesData[];

    setSessions((prev) =>
      prev.map((session) =>
        session.id === activeSessionId
          ? {
              ...session,
              messages: nextMessages,
              updatedAt: Date.now(),
              title: readSessionTitle(nextMessages),
            }
          : session,
      ),
    );
  };

  const handleChatScrollActivity = () => {
    setIsChatScrolling(true);
    if (scrollTimerRef.current) {
      window.clearTimeout(scrollTimerRef.current);
    }
    scrollTimerRef.current = window.setTimeout(() => {
      setIsChatScrolling(false);
      scrollTimerRef.current = null;
    }, 900);
  };

  return (
    <div className="ai-agent-page">
      <aside className="ai-agent-sessions">
        <Button className="ai-agent-new-session-btn" icon={<AddIcon />} onClick={handleCreateSession}>
          新建
        </Button>

        <div className="ai-agent-history-panel">
          <Space align="center" className="ai-agent-history-header">
            <span className="ai-agent-history-title">会话列表</span>
          </Space>

          <div className="ai-agent-sessions-list">
            {sessions.length === 0 ? (
              <Empty description="暂无历史会话" />
            ) : (
              <Menu
                className="ai-agent-session-menu"
                value={activeSessionId}
                expanded={[]}
                onChange={(value) => handleSelectSession(String(value))}
              >
                {sessions.map((session) => (
                  <Menu.MenuItem key={session.id} value={session.id} className="ai-agent-session-menu-item">
                    <div className="ai-agent-session-title-text">{session.title}</div>
                    <div className="ai-agent-session-meta">{formatTime(session.updatedAt)}</div>
                  </Menu.MenuItem>
                ))}
              </Menu>
            )}
          </div>
        </div>
      </aside>

      <div
        className={`ai-agent-content${isChatScrolling ? ' is-chat-scrolling' : ''}`}
        onWheel={handleChatScrollActivity}
        onTouchMove={handleChatScrollActivity}
      >
        <ChatBot
          key={activeSession.id}
          className="ai-agent-chatbot"
          defaultMessages={activeSession.messages}
          messageProps={messageProps}
          listProps={{
            autoScroll: true,
            defaultScrollTo: 'bottom',
          }}
          onMessageChange={handleMessageChange}
          chatServiceConfig={{
            endpoint: '/api/Chat/ChatStructured',
            stream: true,
            onRequest: ({ prompt, ...rest }) => ({
              ...rest,
              method: 'POST',
              headers: {
                'Content-Type': 'application/json',
              },
              body: JSON.stringify({
                message: prompt,
                userId: '1209332',
                sessionId: activeSession.id,
              }),
            }),
            onMessage: (chunk) => {
              const event = parseStructuredEvent(chunk);
              if (!event) {
                return null;
              }

              if (event.event === 'meta') {
                const markdown = toStructuredMarkdown(event.data);
                if (!markdown) {
                  return null;
                }

                return {
                  type: 'markdown',
                  data: markdown,
                  strategy: 'merge',
                };
              }

              if (event.event === 'text') {
                const normalizedText = normalizeMarkdownText(event.data);
                const nextStreamText = `${streamTextMap[activeSession.id] || ''}${normalizedText}`;
                setStreamTextMap((prev) => ({
                  ...prev,
                  [activeSession.id]: nextStreamText,
                }));

                return {
                  type: 'text',
                  data: normalizedText,
                  strategy: 'merge',
                };
              }

              if (event.event === 'error') {
                const message = event.data.message || '请求失败';
                return {
                  type: 'markdown',
                  data: `> 错误：${message}`,
                  status: 'error',
                  strategy: 'merge',
                };
              }

              return null;
            },
          }}
        />
      </div>
    </div>
  );
};

export default AIAgent;
