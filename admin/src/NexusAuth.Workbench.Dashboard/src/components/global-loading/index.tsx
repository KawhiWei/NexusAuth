import React from "react";
import { Loading } from 'tdesign-react';

import { isDarkTheme } from '../../theme';

interface GlobalLoadingProps {
    height?: string | number;
}

const GlobalLoading: React.FC<GlobalLoadingProps> = ({ height = '100vh' }) => (
    (() => {
        const isDark = isDarkTheme();

        return (
            <div
                style={{
                    position: 'fixed',
                    inset: 0,
                    zIndex: 999,
                    display: 'flex',
                    justifyContent: 'center',
                    alignItems: 'center',
                    minHeight: height,
                    width: '100vw',
                    background: isDark ? 'rgba(6, 9, 47, 0.03)' : 'rgba(255, 255, 255, 0.04)',
                }}
            >
                <Loading loading={true} size="medium" text="页面加载中..." />
            </div>
        );
    })()
)

export default GlobalLoading;
