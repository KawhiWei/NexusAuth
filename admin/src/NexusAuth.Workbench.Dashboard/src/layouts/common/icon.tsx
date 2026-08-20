import { DashboardIcon, LockOnIcon, RobotIcon, SettingIcon, UserIcon } from 'tdesign-icons-react';

interface IProp {
    iconName: string;
}

const iconMap: Record<string, React.ReactNode> = {
    "ruleConfig": <SettingIcon />,
    "dashboard": <DashboardIcon />,
    "aiAgent": <RobotIcon />,
    "user": <UserIcon />,
    "lock": <LockOnIcon />,
    "key": <LockOnIcon />
};

const IconRenderer = (iconName: string) => {
    const icon = iconMap[iconName];
    return icon || null;
};

const IconComponent = (props: IProp) => {
    return (IconRenderer(props.iconName))

}
export default IconComponent
