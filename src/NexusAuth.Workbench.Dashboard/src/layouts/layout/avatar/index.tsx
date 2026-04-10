import { Avatar, Dropdown, Button } from 'tdesign-react';
import type { DropdownOption } from 'tdesign-react';
import { PoweroffIcon, UserIcon } from 'tdesign-icons-react';

import { router } from "../../../router";
import { TOKEN_STORAGE_KEY } from '../../../router/auth';

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

  const handleClickMenuItem = (dropdownItem: DropdownOption) => {
    if (dropdownItem.value === 'logout') {
      localStorage.removeItem(TOKEN_STORAGE_KEY);
      localStorage.removeItem('userInfo');
      router.navigate('/login', { replace: true });
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
