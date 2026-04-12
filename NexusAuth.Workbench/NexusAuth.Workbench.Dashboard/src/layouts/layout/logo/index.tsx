import logo from '../../../assets/react.svg';

interface LogoComponentProps {
    collapse?: boolean;
}

const LogoComponent = ({ collapse }: LogoComponentProps) => {
    return (
        <div className={`logo-wrap${collapse ? ' is-collapsed' : ''}`}>
            <img alt="logo" src={logo} width="30px" height="100%" />
            <h1 className="logo-title">React TDesign</h1>
        </div>
    )
};
export default LogoComponent;
