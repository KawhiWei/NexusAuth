export type ThemeMode = 'light' | 'dark';

const THEME_STORAGE_KEY = 'theme-mode';
const THEME_ATTRIBUTE = 'theme-mode';

export const getThemeMode = (): ThemeMode => {
  return localStorage.getItem(THEME_STORAGE_KEY) === 'dark' ? 'dark' : 'light';
};

export const isDarkTheme = () => getCurrentThemeMode() === 'dark';

export const getCurrentThemeMode = (): ThemeMode => {
  return document.documentElement.getAttribute(THEME_ATTRIBUTE) === 'dark' ? 'dark' : 'light';
};

export const applyThemeMode = (theme: ThemeMode) => {
  localStorage.setItem(THEME_STORAGE_KEY, theme);

  if (theme === 'dark') {
    document.documentElement.setAttribute(THEME_ATTRIBUTE, 'dark');
    return;
  }

  document.documentElement.removeAttribute(THEME_ATTRIBUTE);
};

export const subscribeThemeMode = (listener: (theme: ThemeMode) => void) => {
  const observer = new MutationObserver(() => {
    listener(getCurrentThemeMode());
  });

  observer.observe(document.documentElement, {
    attributes: true,
    attributeFilter: [THEME_ATTRIBUTE],
  });

  return () => {
    observer.disconnect();
  };
};
