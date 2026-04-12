const listeners = new Set<() => void>();

let pageLoading = false;

export const getPageLoading = () => pageLoading;

export const setPageLoading = (loading: boolean) => {
  if (pageLoading === loading) {
    return;
  }

  pageLoading = loading;
  listeners.forEach((listener) => {
    listener();
  });
};

export const subscribePageLoading = (listener: () => void) => {
  listeners.add(listener);

  return () => {
    listeners.delete(listener);
  };
};
