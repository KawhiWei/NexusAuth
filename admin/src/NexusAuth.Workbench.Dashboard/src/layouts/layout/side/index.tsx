import MenuComponent from './menu';

interface SliderMenuProps {
    collapse: boolean;
}

const SliderMenu = ({ collapse }: SliderMenuProps) => {
    return (
        <MenuComponent collapse={collapse} />
    )
}
export default SliderMenu
