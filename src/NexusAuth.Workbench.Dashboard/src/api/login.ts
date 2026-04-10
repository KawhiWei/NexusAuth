export type LoginParams = {
  username: string;
  password: string;
};

export type LoginResponse = {
  token: string;
  userInfo: {
    id: string;
    name: string;
    username: string;
  };
};

const mockAccounts = [
  {
    id: '1',
    name: '管理员',
    username: 'admin@react-template.tech',
    password: '123456',
  },
  {
    id: '2',
    name: '系统管理员',
    username: 'admin',
    password: '123456',
  },
];

export const mockLogin = (params: LoginParams) => {
  return new Promise<LoginResponse>((resolve, reject) => {
    window.setTimeout(() => {
      const matchedUser = mockAccounts.find((item) => item.username === params.username && item.password === params.password);

      if (!matchedUser) {
        reject(new Error('账号或密码错误'));
        return;
      }

      resolve({
        token: `mock-token-${matchedUser.id}`,
        userInfo: {
          id: matchedUser.id,
          name: matchedUser.name,
          username: matchedUser.username,
        },
      });
    }, 500);
  });
};
