/** `@twa-dev/sdk` uchun test stub'i — `initData` ni testda o'zgartirish mumkin. */
export const webAppStub = {
  initData: '',
  initDataUnsafe: {},
  colorScheme: 'light' as 'light' | 'dark',
  themeParams: {},
  version: '8.0',
  isVersionAtLeast: () => true,
  ready: () => undefined,
  expand: () => undefined,
  disableVerticalSwipes: () => undefined,
  setHeaderColor: () => undefined,
  setBackgroundColor: () => undefined,
  setBottomBarColor: () => undefined,
  onEvent: () => undefined,
  offEvent: () => undefined,
  openLink: () => undefined,
  HapticFeedback: { notificationOccurred: () => undefined },
};

export function setInitData(value: string) {
  webAppStub.initData = value;
}
