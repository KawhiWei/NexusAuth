import { Breadcrumb } from 'tdesign-react';

const { BreadcrumbItem } = Breadcrumb;
import { Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Outlet, useLocation, useMatches } from 'react-router-dom';

import GlobalLoading from '../../../components/global-loading';
import { getMenuList } from '../../../api/auth';
import { setPageLoading } from '../../../page-loading';

interface MenuInfo {
    name: string;
    parentPaths: string[];
}

const PublicContent = () => {
    const { pathname } = useLocation();
    const matches = useMatches();
    const [menuMap, setMenuMap] = useState<Record<string, MenuInfo>>({});
    const contentInnerRef = useRef<HTMLDivElement | null>(null);
    const breadcrumbHeaderRef = useRef<HTMLDivElement | null>(null);
    const contentRef = useRef<HTMLDivElement | null>(null);
    const [contentMaxHeight, setContentMaxHeight] = useState<number>();

    useEffect(() => {
        setPageLoading(false);
    }, [pathname]);

    useEffect(() => {
        getMenuList().then(res => {
            const map: Record<string, MenuInfo> = {};

            const buildMap = (menus: any[], parentMenu?: any) => {
                const parentId = parentMenu ? parentMenu.id : null;
                menus
                    .filter(item => item.parentId === parentId)
                    .forEach(item => {
                        const parentPaths = parentMenu?.parentPaths || [];
                        const lastPath = parentPaths.length > 0 ? parentPaths[parentPaths.length - 1] : '';
                        const path = (parentMenu ? `${lastPath}${item.route}` : item.route) || '';
                        if (path) {
                            map[path] = { name: item.name, parentPaths };
                        }
                        buildMap(menus, {
                            ...item,
                            parentPaths: [...parentPaths, path || ''].filter(o => o),
                        });
                    });
            };

            buildMap(res);
            setMenuMap(map);
        });
    }, []);

    const breadcrumbItems = useMemo(() => {
        const currentPath = pathname;
        const current = menuMap[currentPath];
        if (!current) {
            const lastMatch = matches[matches.length - 1];
            const handle = lastMatch?.handle as { name?: string } | undefined;
            return handle?.name ? [{ key: currentPath, label: handle.name }] : [];
        }

        return [...current.parentPaths, currentPath].map(path => ({
            key: path,
            label: menuMap[path]?.name || path,
        }));
    }, [menuMap, matches, pathname]);

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
    }, [breadcrumbItems.length, pathname, updateContentMaxHeight]);

    useEffect(() => {
        updateContentMaxHeight();

        const resizeObserver = new ResizeObserver(() => {
            updateContentMaxHeight();
        });

        if (contentInnerRef.current) {
            resizeObserver.observe(contentInnerRef.current);
        }

        if (breadcrumbHeaderRef.current) {
            resizeObserver.observe(breadcrumbHeaderRef.current);
        }

        window.addEventListener('resize', updateContentMaxHeight);

        return () => {
            resizeObserver.disconnect();
            window.removeEventListener('resize', updateContentMaxHeight);
        };
    }, [breadcrumbItems.length, updateContentMaxHeight]);


    return (

        <Suspense fallback={<GlobalLoading height={contentMaxHeight || 320} />}>
            <div className="layout-content-inner" ref={contentInnerRef}>
                {breadcrumbItems.length > 0 && (
                    <div className="layout-content-header" ref={breadcrumbHeaderRef}>
                        <Breadcrumb className="layout-content-breadcrumb">
                            {breadcrumbItems.map(item => (
                                <BreadcrumbItem key={item.key}>{item.label}</BreadcrumbItem>
                            ))}
                        </Breadcrumb>
                    </div>
                )}
                <div className="layout-main-content" ref={contentRef} style={{ maxHeight: contentMaxHeight }}>
                    <Outlet />
                </div>
            </div>

        </Suspense>
    )
}
export default PublicContent
