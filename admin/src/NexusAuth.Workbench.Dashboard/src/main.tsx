import 'tdesign-react/es/style/index.css';
import './main.css';

import App from './App.tsx';
import { applyThemeMode, getThemeMode } from './theme';
import { createRoot } from 'react-dom/client';

// Keep the React tree and the preloaded document attribute in sync before the first render.
applyThemeMode(getThemeMode());

createRoot(document.getElementById('root')!).render(
  <App />,
);
