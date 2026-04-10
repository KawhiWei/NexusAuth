import { useEffect, useState } from 'react';
import { Button, Input, Space, Tooltip } from 'tdesign-react';
import { FullscreenExitIcon, FullscreenIcon, MoonIcon, SearchIcon, SunnyIcon } from 'tdesign-icons-react';

interface PublicHeaderProps {
  theme: 'light' | 'dark';
  onChangeTheme: () => void;
}

const PublicHeader = ({ theme, onChangeTheme }: PublicHeaderProps) => {
    const [isFullscreen, setIsFullscreen] = useState(Boolean(document.fullscreenElement));

    useEffect(() => {
        const handleFullscreenChange = () => {
            setIsFullscreen(Boolean(document.fullscreenElement));
        };

        document.addEventListener('fullscreenchange', handleFullscreenChange);
        return () => {
            document.removeEventListener('fullscreenchange', handleFullscreenChange);
        };
    }, []);

    const handleToggleFullscreen = async () => {
        if (document.fullscreenElement) {
            await document.exitFullscreen();
            return;
        }
        await document.documentElement.requestFullscreen();
    };

    return (
        <div className="layout-header-edit" >
            <Space size="medium">
        <Input
          style={{
            width: 200
          }}
          prefixIcon={<SearchIcon />}
          placeholder="请输入内容查询"
        />
 <Tooltip
          placement="bottom"
          trigger="hover"
          content={`点击切换为${theme === 'light' ? '暗黑' : '亮色'}模式`}
        >
          <Button
            shape="circle"
            icon={theme === 'light' ? <MoonIcon /> : <SunnyIcon />}
            onClick={onChangeTheme}
          />
        </Tooltip>
        <Tooltip
          placement="bottom"
          trigger="hover"
          content={isFullscreen ? '退出全屏' : '全屏显示'}
        >
          <Button
            shape="circle"
            icon={isFullscreen ? <FullscreenExitIcon /> : <FullscreenIcon />}
            onClick={() => {
              void handleToggleFullscreen();
            }}
          />
        </Tooltip>
                
            </Space>
        </div>

    )
}
export default PublicHeader
