import { lazy, useEffect, useState } from "react";

import ErrorPage from "../../components/error";
import { getMenuList } from "../../api/auth";
import { lazyLoad } from '../../lazyLoad/lazy'
import { replaceRoutes } from "../../router/router-utils";
import { router } from "../../router";

const useUserDetail = () => {
    const [loading, setLoading] = useState(true);
    const [requestLoading] = useState(true);
    const [disconnect] = useState(true);
    useEffect(() => {
        /**
         * 设置主题
         */
        // document.body.setAttribute('arco-theme', 'dark');
        getMenuList().then(res => {
            function formatMenus(
                menus: any[],
                menuGroup: Record<string, any[]>,
                routes: any[],
                parentMenu?: any
            ): any[] {
                return menus.map(menu => {
                    /**
                     * 判断当前格式化内是否传入了父级菜单
                     * 如果传入的父级菜单则在当前遍历的菜单内添加父级路由
                     */
                    const children = menuGroup[menu.id!];
                    const parentPaths = parentMenu?.parentPaths || [];
                    const lastPath = parentPaths[parentPaths.length - 1];
                    const path = (parentMenu ? `${lastPath}${menu.route}` : menu.route) || '';
                    routes.push({
                        ...menu,
                        path,
                        parentPaths,
                    });

                    return {
                        ...menu,
                        path,
                        parentPaths,
                        children: children?.length ? formatMenus(children, menuGroup, routes, {
                            ...menu,
                            parentPaths: [...parentPaths, path || ''].filter(o => o),
                        }) : undefined,
                    };
                });
            }

            const menus: any[] = res;
            /**
             * 将菜单按照父子关系分组
             */
            const menuGroup = menus.reduce<Record<string, any[]>>((prev: any, menu: any) => {
                if (!menu.parentId) {
                    return prev;
                }

                if (!prev[menu.parentId]) {
                    prev[menu.parentId] = [];
                }
                prev[menu.parentId].push(menu);
                return prev;
            }, {});

            const routes: any[] = [];
            /**
             * 格式化路由
             */
            formatMenus(menus.filter(o => !o.parentId), menuGroup, routes);

            /**
             * 路由替换，此处试讲所有路由拉平，不包含二级路由等，菜单与路由分开处理
             */
            replaceRoutes('protected-layout', [
                ...routes.map(menu => {
                    /**
                     * @description: 路由格式化将二级路由和一级路由合并生成路由列表
                     */
                    const routePath = menu.path.replace(/^\//, '');
                    var route = {
                        path: routePath,
                        id: menu.path,
                        // Component: menu.componentPath ? lazy(modules[`../pages/${menu.componentPath}/index.tsx`]) : ErrorPage,
                        Component: menu.componentPath ? lazyLoad[menu.componentPath] ? lazy(lazyLoad[menu.componentPath]) : ErrorPage : ErrorPage,
                        name: menu.name,
                        handle: {
                            parentPaths: menu.parentPaths,
                            path: menu.route,
                            name: menu.name,
                            icon: menu.icon,
                        },
                    }
                    return route
                }), {
                    id: '*',
                    path: '*',
                    Component: ErrorPage,
                    handle: {
                        path: '404',
                        name: '404',
                    },
                }
            ]);
            router.navigate(`${location.pathname}${location.search}${location.hash}`, {
                replace: true,
                state: {
                    routeReloadAt: Date.now(),
                },
            });
            setLoading(false);
        })

    }, [])
    return {
        loading: requestLoading || loading,
        disconnectWS: disconnect,
    }
}
export default useUserDetail
