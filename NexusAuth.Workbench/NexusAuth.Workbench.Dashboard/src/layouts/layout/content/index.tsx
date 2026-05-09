import { Outlet, useLocation } from 'react-router-dom';
import { Suspense, useCallback, useEffect, useRef, useState } from 'react';

import GlobalLoading from '../../../components/global-loading';
import { setPageLoading } from '../../../page-loading';

const PublicContent = () => {
    const { pathname } = useLocation();
    const contentInnerRef = useRef<HTMLDivElement | null>(null);
    const contentRef = useRef<HTMLDivElement | null>(null);
    const [contentMaxHeight, setContentMaxHeight] = useState<number>();

    useEffect(() => {
        setPageLoading(false);
    }, [pathname]);

    const updateContentMaxHeight = useCallback(() => {
        if (!contentRef.current) {
            return;
        }
        const { top } = contentRef.current.getBoundingClientRect();
        const nextHeight = Math.max(Math.floor(window.innerHeight - top), 0);
        setContentMaxHeight((prev) => (prev === nextHeight ? prev : nextHeight));
    }, []);

    useEffect(() => {
        updateContentMaxHeight();

        const frame = window.requestAnimationFrame(() => {
            updateContentMaxHeight();
        });

        return () => {
            window.cancelAnimationFrame(frame);
        };
    }, [pathname, updateContentMaxHeight]);

    useEffect(() => {
        updateContentMaxHeight();

        const resizeObserver = new ResizeObserver(() => {
            updateContentMaxHeight();
        });

        if (contentInnerRef.current) {
            resizeObserver.observe(contentInnerRef.current);
        }

        window.addEventListener('resize', updateContentMaxHeight);

        return () => {
            resizeObserver.disconnect();
            window.removeEventListener('resize', updateContentMaxHeight);
        };
    }, [updateContentMaxHeight]);

    return (

        <Suspense fallback={<GlobalLoading height={contentMaxHeight || 320} />}>
            <div className="layout-content-inner" ref={contentInnerRef}>
                <div className="layout-main-content" ref={contentRef} style={{ maxHeight: contentMaxHeight }}>
                    <Outlet />
                </div>
            </div>

        </Suspense>
    )
}
export default PublicContent
