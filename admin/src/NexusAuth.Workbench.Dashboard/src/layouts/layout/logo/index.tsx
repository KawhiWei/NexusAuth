import BrandComponent from '../../../components/brand';

interface LogoComponentProps {
  collapse?: boolean;
}

const LogoComponent = ({ collapse }: LogoComponentProps) => {
  return <BrandComponent className="logo-wrap" compact={collapse} />;
};
export default LogoComponent;
