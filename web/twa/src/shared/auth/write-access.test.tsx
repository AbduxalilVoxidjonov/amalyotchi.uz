import { screen } from '@testing-library/react';
import { renderApp } from '@/test/render-app';
import { setInitData, webAppStub } from '@/test/telegram-stub';
import { requestWriteAccessOnce, WRITE_ACCESS_FLAG_KEY } from './telegram';

describe('requestWriteAccessOnce', () => {
  beforeEach(() => {
    setInitData('query_id=test&user=%7B%22id%22%3A1%7D&hash=x');
  });

  it("Telegram ichida, ruxsat berilmagan — bir marta so'raladi (localStorage bayrog'i)", () => {
    webAppStub.initDataUnsafe = { user: { id: 1, allows_write_to_pm: false } };
    expect(requestWriteAccessOnce()).toBe(true);
    expect(requestWriteAccessOnce()).toBe(false);
    expect(webAppStub.requestWriteAccess).toHaveBeenCalledTimes(1);
    expect(window.localStorage.getItem(WRITE_ACCESS_FLAG_KEY)).toBe('1');
  });

  it("ruxsat allaqachon berilgan — so'ralmaydi", () => {
    webAppStub.initDataUnsafe = { user: { id: 1, allows_write_to_pm: true } };
    expect(requestWriteAccessOnce()).toBe(false);
    expect(webAppStub.requestWriteAccess).not.toHaveBeenCalled();
  });

  it("eski Telegram (6.9 dan past) — so'ralmaydi, bayroq ham yozilmaydi", () => {
    webAppStub.version = '6.4';
    expect(requestWriteAccessOnce()).toBe(false);
    expect(webAppStub.requestWriteAccess).not.toHaveBeenCalled();
    expect(window.localStorage.getItem(WRITE_ACCESS_FLAG_KEY)).toBeNull();
  });

  it("Telegram tashqarisida — so'ralmaydi", () => {
    setInitData('');
    expect(requestWriteAccessOnce()).toBe(false);
    expect(webAppStub.requestWriteAccess).not.toHaveBeenCalled();
  });

  it('SDK xato tashlasa ilova yiqilmaydi', () => {
    webAppStub.requestWriteAccess.mockImplementationOnce(() => {
      throw new Error('WebAppMethodUnsupported');
    });
    expect(requestWriteAccessOnce()).toBe(false);
  });

  it("muvaffaqiyatli kirgach (AppShell) bir marta so'raladi", async () => {
    renderApp('/');
    await screen.findByRole('navigation');
    expect(webAppStub.requestWriteAccess).toHaveBeenCalledTimes(1);
  });
});
