import { useEffect, useState } from 'react';
import { Button, Input, Space, Tooltip } from 'tdesign-react';
import { Fullscreen1Icon, FullscreenExit1Icon, MoonIcon, SearchIcon, SunnyIcon } from 'tdesign-icons-react';

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
                    width: 200,
                  }}
                  aria-label="全局搜索"
                  prefixIcon={<SearchIcon />}
                  placeholder="请输入内容查询"
                />
                <Tooltip
                  placement="bottom"
                  trigger="hover"
                  content={`点击切换为${theme === 'light' ? '暗黑' : '亮色'}模式`}
                >
                  <Button
                    className="layout-header-action"
                    size="small"
                    shape="circle"
                    theme="default"
                    variant="text"
                    type="button"
                    aria-label={theme === 'light' ? '切换到暗黑模式' : '切换到亮色模式'}
                    icon={theme === 'light' ? <MoonIcon size="20px" /> : <SunnyIcon size="20px" />}
                    onClick={onChangeTheme}
                  />
                </Tooltip>
                <Tooltip
                  placement="bottom"
                  trigger="hover"
                  content={isFullscreen ? '退出全屏' : '全屏显示'}
                >
                  <Button
                    className="layout-header-action"
                    size="small"
                    shape="circle"
                    theme="default"
                    variant="text"
                    type="button"
                    aria-label={isFullscreen ? '退出全屏' : '全屏显示'}
                    icon={isFullscreen ? <FullscreenExit1Icon size="20px" /> : <Fullscreen1Icon size="20px" />}
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
