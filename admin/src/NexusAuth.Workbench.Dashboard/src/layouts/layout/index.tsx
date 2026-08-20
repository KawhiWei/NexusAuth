import './style.less';

import { Layout, Tabs } from 'tdesign-react';
import { ChevronLeftIcon, ChevronRightIcon } from 'tdesign-icons-react';
import { applyThemeMode, getThemeMode } from '../../theme';
import { getPageLoading, subscribePageLoading } from '../../page-loading';
import { useEffect, useMemo, useState, useSyncExternalStore } from 'react';
import { useLocation, useMatches, useNavigate } from 'react-router-dom';

import AvatarComponent from './avatar';
import GlobalLoading from '../../components/global-loading';
import LogoComponent from './logo';
import PublicContent from './content';
import PublicHeader from './header';
import SliderMenu from './side';
import { getMenuList } from '../../api/auth';
import useUserDetail from '../common/use-user-detail.';

const { Content, Aside, Header } = Layout;
const { TabPanel } = Tabs;

interface MenuInfo {
  name: string;
}

interface TabItem {
  value: string;
  label: string;
  removable: boolean;
}

const DEFAULT_TAB_PATH = '/dashboard';
const DEFAULT_TAB_LABEL = '仪表盘';
const TABS_STORAGE_KEY = 'nexus-auth-layout-tabs';

const getInitialTabs = (): TabItem[] => {
  const defaultTabs = [{ value: DEFAULT_TAB_PATH, label: DEFAULT_TAB_LABEL, removable: false }];
  try {
    const raw = window.localStorage.getItem(TABS_STORAGE_KEY);
    if (!raw) {
      return defaultTabs;
    }
    const parsed = JSON.parse(raw) as TabItem[];
    if (!Array.isArray(parsed) || parsed.length === 0) {
      return defaultTabs;
    }
    const validTabs = parsed.filter((tab) => tab?.value && tab?.label);
    const hasDashboard = validTabs.some((tab) => tab.value === DEFAULT_TAB_PATH);
    return hasDashboard ? validTabs : [...defaultTabs, ...validTabs];
  } catch {
    return defaultTabs;
  }
};

const PublicLayout = () => {
  const matches = useMatches();
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const pageLoading = useSyncExternalStore(subscribePageLoading, getPageLoading, getPageLoading);

  useUserDetail();

  const [collapsed, setCollapsed] = useState(false);
  const [theme, setTheme] = useState<'light' | 'dark'>(() => getThemeMode());
  const [menuMap, setMenuMap] = useState<Record<string, MenuInfo>>({});
  const [tabs, setTabs] = useState<TabItem[]>(getInitialTabs);

  useEffect(() => {
    window.localStorage.setItem(TABS_STORAGE_KEY, JSON.stringify(tabs));
  }, [tabs]);

  useEffect(() => {
    getMenuList().then((res) => {
      const map: Record<string, MenuInfo> = {};

      const buildMap = (menus: any[], parentMenu?: any) => {
        const parentId = parentMenu ? parentMenu.id : null;
        menus
          .filter((item) => item.parentId === parentId)
          .forEach((item) => {
            const parentPaths = parentMenu?.parentPaths || [];
            const lastPath = parentPaths.length > 0 ? parentPaths[parentPaths.length - 1] : '';
            const path = (parentMenu && !item.route.startsWith('/') ? `${lastPath}${item.route}` : item.route) || '';
            if (path) {
              map[path] = { name: item.name };
            }
            buildMap(menus, {
              ...item,
              parentPaths: [...parentPaths, path || ''].filter(Boolean),
            });
          });
      };

      buildMap(res);
      setMenuMap(map);
    });
  }, []);

  const currentTabLabel = useMemo(() => {
    if (pathname === DEFAULT_TAB_PATH) {
      return menuMap[pathname]?.name || DEFAULT_TAB_LABEL;
    }

    const current = menuMap[pathname];
    if (current?.name) {
      return current.name;
    }

    const lastRoute = matches[matches.length - 1];
    const handle = lastRoute?.handle as { name?: string } | undefined;
    return handle?.name || pathname;
  }, [matches, menuMap, pathname]);

  useEffect(() => {
    setTabs((prev) => {
      const dashboardTab: TabItem = {
        value: DEFAULT_TAB_PATH,
        label: menuMap[DEFAULT_TAB_PATH]?.name || DEFAULT_TAB_LABEL,
        removable: false,
      };

      const restTabs = prev.filter((tab) => tab.value !== DEFAULT_TAB_PATH);
      return [dashboardTab, ...restTabs];
    });
  }, [menuMap]);

  useEffect(() => {
    setTabs((prev) => {
      const existing = prev.find((tab) => tab.value === pathname);
      if (existing) {
        return prev.map((tab) => (
          tab.value === pathname
            ? { ...tab, label: currentTabLabel, removable: pathname !== DEFAULT_TAB_PATH }
            : tab
        ));
      }

      const nextTab: TabItem = {
        value: pathname,
        label: currentTabLabel,
        removable: pathname !== DEFAULT_TAB_PATH,
      };

      const dashboardTab = prev.find((tab) => tab.value === DEFAULT_TAB_PATH) || {
        value: DEFAULT_TAB_PATH,
        label: menuMap[DEFAULT_TAB_PATH]?.name || DEFAULT_TAB_LABEL,
        removable: false,
      };
      const restTabs = prev.filter((tab) => tab.value !== DEFAULT_TAB_PATH);

      if (pathname === DEFAULT_TAB_PATH) {
        return [dashboardTab, ...restTabs];
      }

      return [dashboardTab, ...restTabs, nextTab];
    });
  }, [currentTabLabel, menuMap, pathname]);

  useEffect(() => {
    const lastRoute = matches[matches.length - 1];
    const handle = lastRoute?.handle as { name?: string } | undefined;
    if (handle?.name) {
      document.title = handle.name;
    }
  }, [matches, pathname])

  const handleCollapsed = () => {
    setCollapsed((prev) => !prev);
  };

  const handleChangeTheme = () => {
    const next = theme === 'light' ? 'dark' : 'light';
    setTheme(next);
    applyThemeMode(next);
  };

  const handleTabChange = (value: string | number) => {
    if (value === undefined || value === null) {
      return;
    }

    const nextPath = String(value);
    if (!nextPath || nextPath === 'undefined' || nextPath === pathname) {
      return;
    }

    navigate(nextPath);
  };

  const handleTabRemove = ({ value }: { value: string | number }) => {
    if (value === undefined || value === null) {
      return;
    }

    const closingKey = String(value);
    if (!closingKey || closingKey === 'undefined') {
      return;
    }

    const closingIndex = tabs.findIndex((tab) => tab.value === closingKey);
    if (closingIndex === -1) {
      return;
    }

    const nextTabs = tabs.filter((tab) => tab.value !== closingKey);
    setTabs(nextTabs);

    if (pathname !== closingKey) {
      return;
    }

    const fallbackTab = nextTabs[closingIndex] || nextTabs[closingIndex - 1] || nextTabs[0];
    const fallbackPath = fallbackTab?.value || DEFAULT_TAB_PATH;
    if (fallbackPath && fallbackPath !== pathname) {
      navigate(fallbackPath);
    }
  };

  return (

    <Layout className="layout-container">
      {pageLoading && <GlobalLoading />}
      <Header className="layout-header">
        <div className="layout-header-left">
          <LogoComponent />
        </div>
        <div className="layout-header-right">
          <PublicHeader theme={theme} onChangeTheme={handleChangeTheme} />
          <AvatarComponent />
        </div>
      </Header>

      <Layout className="layout-body">
        <Aside
          width={collapsed ? '64px' : '232px'}
          className={`layout-sider${collapsed ? ' is-collapsed' : ''}`}
        >
          <div className="layout-sider-menu">
            <SliderMenu collapse={collapsed} theme={theme} />
          </div>
          <div className="layout-sider-trigger-bottom" onClick={handleCollapsed}>
            {collapsed ? <ChevronRightIcon /> : <ChevronLeftIcon />}
          </div>
        </Aside>

        <Layout className="layout-main">
          <Content className="layout-content">
            <div className="layout-content-tabs">
              <Tabs
                value={pathname}
                size="medium"
                theme="normal"
                scrollPosition="auto"
                onChange={handleTabChange}
                onRemove={handleTabRemove}
              >
                {tabs.map((tab) => (
                  <TabPanel
                    key={tab.value}
                    value={tab.value}
                    label={tab.label}
                    removable={tab.removable}
                  />
                ))}
              </Tabs>
            </div>
            <PublicContent />
          </Content>
        </Layout>
      </Layout>
    </Layout>)
}
export default PublicLayout
