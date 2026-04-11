import { Avatar, Dropdown, Button } from 'tdesign-react';
import type { DropdownOption } from 'tdesign-react';
import { PoweroffIcon, UserIcon } from 'tdesign-icons-react';

import { router } from "../../../router";
import { setCachedAuthStatus } from '../../../router/auth';
import { logout } from '../../../api/login';

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
    if (dropdownItem.value === 'logout') {
      try {
        const result: { logoutUrl: string } = await logout();
        setCachedAuthStatus(false);
        if (result.logoutUrl) {
          window.location.href = result.logoutUrl;
        } else {
          window.location.replace('/login');
        }
      } catch {
        setCachedAuthStatus(false);
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
      <Button variant="text" style={{ padding: 0 }}>
        <Avatar
          style={{
            backgroundColor: '#165DFF'
          }}
        >
          H
        </Avatar>
      </Button>
    </Dropdown>
  );
}
export default AvatarComponent;
