import MenuComponent from './menu';

interface SliderMenuProps {
    collapse: boolean;
    theme: 'light' | 'dark';
}

const SliderMenu = ({ collapse, theme }: SliderMenuProps) => {
    return (
        <MenuComponent collapse={collapse} theme={theme} />
    )
}
export default SliderMenu
