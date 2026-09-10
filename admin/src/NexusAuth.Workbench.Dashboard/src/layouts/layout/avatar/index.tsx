import { Avatar, Dropdown, Button } from 'tdesign-react';
import type { DropdownOption } from 'tdesign-react';
import { KeyIcon, PoweroffIcon, UserIcon } from 'tdesign-icons-react';

import { setCachedAuthStatus } from '../../../router/auth';
import { getConfig, logout } from '../../../api/login';

const TABS_STORAGE_KEY = 'nexus-auth-layout-tabs';

const clearLayoutTabs = () => {
  window.localStorage.removeItem(TABS_STORAGE_KEY);
};

const AvatarComponent = () => {
  const iconStyle: React.CSSProperties = {
    marginRight: 8,
    fontSize: 16,
    transform: 'translateY(1px)'
  };

  const options = [
    {
      content: (
        <span>
          <KeyIcon style={iconStyle} />
          账号设置
        </span>
      ),
      value: 'account-settings',
    },
    {
      content: (
        <span>
          <UserIcon style={iconStyle} />
          个人信息
        </span>
      ),
      value: 'admin',
    },
    {
      content: (
        <span>
          <PoweroffIcon style={iconStyle} />
          退出登录
        </span>
      ),
      value: 'logout',
    },
  ];

  const handleClickMenuItem = async (dropdownItem: DropdownOption) => {
    if (dropdownItem.value === 'account-settings') {
      const accountWindow = window.open('about:blank', '_blank');

      try {
        const config = await getConfig();
        const accountUrl = `${config.authority.replace(/\/$/, '')}/account`;

        if (accountWindow) {
          accountWindow.opener = null;
          accountWindow.location.replace(accountUrl);
        } else {
          window.open(accountUrl, '_blank', 'noopener,noreferrer');
        }
      } catch {
        accountWindow?.close();
      }
      return;
    }

    if (dropdownItem.value === 'logout') {
      try {
        const result: { logoutUrl: string } = await logout();
        setCachedAuthStatus(false);
        clearLayoutTabs();
        if (result.logoutUrl) {
          window.location.href = result.logoutUrl;
        } else {
          window.location.replace('/login');
        }
      } catch {
        setCachedAuthStatus(false);
        clearLayoutTabs();
        window.location.replace('/login');
      }
    }
  };

  return (
    <Dropdown
      placement="bottom-right"
      options={options}
      onClick={handleClickMenuItem}
    >
      <Button className="layout-avatar-trigger" variant="text" shape="circle" size="small">
        <Avatar className="layout-avatar">
          H
        </Avatar>
      </Button>
    </Dropdown>
  );
}
export default AvatarComponent;
