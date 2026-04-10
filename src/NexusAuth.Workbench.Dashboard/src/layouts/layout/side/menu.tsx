import { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";

import { Menu, MenuValue } from "tdesign-react";
import { getMenuList } from "../../../api/auth";
import { setPageLoading } from '../../../page-loading';
import IconComponent from "../../common/icon";

const { MenuItem, SubMenu } = Menu;

interface IProp {
    collapse: boolean;
    theme: 'light' | 'dark';
}

interface MenuItemData {
    id: string;
    route: string;
    name: string;
    iconName: string | null;
    parentId: string | null;
    path: string;
    parentPaths: string[];
    children: MenuItemData[];
}

interface RawMenuItem {
    id: string;
    route: string;
    name: string;
    iconName: string | null;
    parentId: string | null;
}

interface ParentMenuNode {
    id: string;
    parentPaths: string[];
}

const MenuComponent = (props: IProp) => {
    const { pathname } = useLocation();
    const navigate = useNavigate();

    const [menus, setMenus] = useState<RawMenuItem[]>([]);
    const [userExpanded, setUserExpanded] = useState<string[]>([]);
    const [initialized, setInitialized] = useState(false);

    useEffect(() => {
        getMenuList().then(res => {
            setMenus(res);
        });
    }, []);

    const transferTreeMenuData = useCallback((menuList: RawMenuItem[], parentMenu?: ParentMenuNode): MenuItemData[] => {
        const parentId = parentMenu ? parentMenu.id : null;
        return menuList.filter(item => item.parentId === parentId).map(item => {
            const parentPaths = parentMenu?.parentPaths || [];
            const lastPath = parentPaths.length > 0 ? parentPaths[parentPaths.length - 1] : '';
            const path = (parentMenu ? `${lastPath}${item.route}` : item.route) || '';
            return {
                ...item,
                path,
                parentPaths,
                children: transferTreeMenuData(menuList, {
                    id: item.id,
                    parentPaths: [...parentPaths, path].filter(Boolean),
                })
            };
        });
    }, []);

    const treeMenus = useMemo(() => transferTreeMenuData(menus), [menus, transferTreeMenuData]);

    useEffect(() => {
        if (treeMenus.length > 0 && !initialized) {
            const keys: string[] = [];
            const findParent = (items: MenuItemData[]) => {
                for (const item of items) {
                    if (item.children.length > 0) {
                        const hasActiveChild = item.children.some(
                            child => child.path === pathname || pathname.startsWith(child.path + '/')
                        );
                        if (hasActiveChild) {
                            keys.push(item.path);
                        }
                        findParent(item.children);
                    }
                }
            };
            findParent(treeMenus);
            setUserExpanded(keys);
            setInitialized(true);
        }
    }, [treeMenus, pathname, initialized]);

    const onExpand = (keys: MenuValue[]) => {
        setUserExpanded(keys.map(String));
    };

    const navigateWithLoading = async (nextPath: string) => {
        if (nextPath === pathname) {
            return;
        }

        setPageLoading(true);
        navigate(nextPath);
    };

    const onMenuChange = (value: MenuValue) => {
        const nextPath = String(value);
        const queue: MenuItemData[] = [...treeMenus];
        while (queue.length > 0) {
            const node = queue.shift();
            if (!node) {
                break;
            }
            if (node.path === nextPath) {
                if (node.children.length === 0) {
                    void navigateWithLoading(nextPath);
                }
                return;
            }
            if (node.children.length > 0) {
                queue.push(...node.children);
            }
        }

        void navigateWithLoading(nextPath);
    };

    const getMenuIcon = (iconName: string | null) => {
        if (!iconName) {
            return undefined;
        }
        return <IconComponent iconName={iconName} />;
    };

    const submenuPopupProps = props.collapse
        ? {
            trigger: 'hover' as const,
            attach: () => document.body,
        }
        : undefined;

    const renderSubMenu = (item: MenuItemData) => {
        return (
            <SubMenu
                key={item.path}
                value={item.path}
                icon={getMenuIcon(item.iconName)}
                popupProps={submenuPopupProps}
                title={item.name}>
                {item.children.map((childItem) => {
                    if (childItem.children.length > 0) {
                        return renderSubMenu(childItem);
                    }
                    return (
                        <MenuItem key={childItem.path} value={childItem.path} icon={getMenuIcon(childItem.iconName)}>
                            {childItem.name}
                        </MenuItem>
                    );
                })}
            </SubMenu>
        );
    };

    return (
        <Menu
            value={pathname}
            expanded={userExpanded}
            collapsed={props.collapse}
            theme={props.theme}
            expandMutex={true}
            onExpand={onExpand}
            onChange={onMenuChange}>
            {treeMenus.map((item) => {
                if (item.children.length > 0) {
                    return renderSubMenu(item);
                }
                return (
                    <MenuItem key={item.path} value={item.path} icon={getMenuIcon(item.iconName)}>
                        {item.name}
                    </MenuItem>
                );
            })}
        </Menu>
    );
};

export default MenuComponent;
