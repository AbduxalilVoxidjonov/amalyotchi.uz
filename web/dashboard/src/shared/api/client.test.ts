import { http, HttpResponse } from 'msw';
import { ApiError, createApiClient } from '@amaliyotchi/shared';
import { issueSession, mockUsers } from '@/mocks/data';
import { server } from '@/mocks/server';
import { useAuthStore } from '@/shared/auth/store';
import { api } from './client';

function jsonResponse(status: number, body: unknown, contentType = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': contentType } });
}

describe('createApiClient', () => {
  it('ProblemDetails javobini ApiError ga aylantiradi', async () => {
    const client = createApiClient({
      fetch: async () =>
        jsonResponse(
          403,
          { status: 403, title: "Ruxsat yo'q", detail: "Parol noto'g'ri.", traceId: 'abc' },
          'application/problem+json',
        ),
    });

    const err = await client.get('/api/x').catch((e: unknown) => e);
    expect(err).toBeInstanceOf(ApiError);
    const apiErr = err as ApiError;
    expect(apiErr.status).toBe(403);
    expect(apiErr.kind).toBe('forbidden');
    expect(apiErr.message).toBe("Parol noto'g'ri.");
    expect(apiErr.traceId).toBe('abc');
  });

  it("validation xatolarida fieldErrors to'ldiriladi", async () => {
    const client = createApiClient({
      fetch: async () =>
        jsonResponse(400, {
          status: 400,
          title: "Ma'lumotlar noto'g'ri",
          errors: { Password: ['Parolni kiriting.'] },
        }),
    });
    const err = (await client.post('/api/x', {}).catch((e: unknown) => e)) as ApiError;
    expect(err.kind).toBe('validation');
    expect(err.fieldError('password')).toBe('Parolni kiriting.');
  });

  it('401 → refresh → bitta qayta urinish (yangi token bilan)', async () => {
    const seen: string[] = [];
    let token = 'old';
    const fetchMock = vi.fn(async (_url: RequestInfo | URL, init?: RequestInit) => {
      const auth = (init?.headers as Record<string, string>)['Authorization'] ?? '';
      seen.push(auth);
      return auth === 'Bearer new'
        ? jsonResponse(200, { ok: true })
        : new Response(null, { status: 401 });
    });
    const refreshSession = vi.fn(async () => {
      token = 'new';
      return token;
    });

    const client = createApiClient({
      fetch: fetchMock,
      getAccessToken: () => token,
      refreshSession,
    });
    await expect(client.get('/api/secure')).resolves.toEqual({ ok: true });
    expect(refreshSession).toHaveBeenCalledTimes(1);
    expect(seen).toEqual(['Bearer old', 'Bearer new']);
  });

  it('parallel 401 lar bitta refresh ni kutadi (navbat)', async () => {
    let token = 'old';
    const fetchMock = vi.fn(async (_url: RequestInfo | URL, init?: RequestInit) => {
      const auth = (init?.headers as Record<string, string>)['Authorization'] ?? '';
      return auth === 'Bearer new'
        ? jsonResponse(200, { auth })
        : new Response(null, { status: 401 });
    });
    const refreshSession = vi.fn(
      () =>
        new Promise<string>((resolve) =>
          setTimeout(() => {
            token = 'new';
            resolve(token);
          }, 10),
        ),
    );
    const client = createApiClient({
      fetch: fetchMock,
      getAccessToken: () => token,
      refreshSession,
    });

    const results = await Promise.all([client.get('/a'), client.get('/b'), client.get('/c')]);
    expect(results).toHaveLength(3);
    expect(refreshSession).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledTimes(6); // 3 ta 401 + 3 ta retry
  });

  it('refresh muvaffaqiyatsiz → onSessionExpired va 401 ApiError', async () => {
    const onSessionExpired = vi.fn();
    const client = createApiClient({
      fetch: async () => new Response(null, { status: 401 }),
      getAccessToken: () => 'x',
      refreshSession: async () => null,
      onSessionExpired,
    });
    const err = (await client.get('/api/secure').catch((e: unknown) => e)) as ApiError;
    expect(err.kind).toBe('unauthorized');
    expect(onSessionExpired).toHaveBeenCalledTimes(1);
  });

  it("auth:false bo'lsa refresh urinilmaydi", async () => {
    const refreshSession = vi.fn(async () => 'new');
    const client = createApiClient({
      fetch: async () => new Response(null, { status: 401 }),
      refreshSession,
    });
    await expect(client.post('/api/auth/login', {}, { auth: false })).rejects.toBeInstanceOf(
      ApiError,
    );
    expect(refreshSession).not.toHaveBeenCalled();
  });

  it('tarmoq xatosi → kind network', async () => {
    const client = createApiClient({
      fetch: async () => {
        throw new TypeError('Failed to fetch');
      },
    });
    const err = (await client.get('/x').catch((e: unknown) => e)) as ApiError;
    expect(err.kind).toBe('network');
  });

  it("204 → undefined; query paramlar URL ga qo'shiladi", async () => {
    const urls: string[] = [];
    const client = createApiClient({
      baseUrl: 'https://api.test/',
      fetch: async (url) => {
        urls.push(String(url));
        return new Response(null, { status: 204 });
      },
    });
    await expect(
      client.get('/items', { query: { page: 1, q: 'ab c', skip: undefined, ids: [1, 2] } }),
    ).resolves.toBeUndefined();
    expect(urls[0]).toBe('https://api.test/items?page=1&q=ab+c&ids=1&ids=2');
  });
});

describe('dashboard api (MSW + store)', () => {
  it("eskirgan access token bilan /me → refresh → qayta so'rov muvaffaqiyatli", async () => {
    const admin = mockUsers[0]!;
    const session = issueSession(admin);
    // Store'ga haqiqiy refresh token, lekin yaroqsiz access token qo'yamiz.
    useAuthStore.getState().setSession({ ...session, accessToken: 'expired.token.value' });

    const me = await api.get<{ id: string }>('/api/auth/me');
    expect(me.id).toBe(admin.id);
    expect(useAuthStore.getState().accessToken).not.toBe('expired.token.value');
    expect(useAuthStore.getState().refreshToken).not.toBe(session.refreshToken); // rotatsiya
  });

  it('refresh ham rad etilsa store tozalanadi', async () => {
    useAuthStore.getState().setSession({
      accessToken: 'bad',
      accessTokenExpiresAt: new Date().toISOString(),
      refreshToken: 'unknown-refresh',
      user: mockUsers[0]!,
    });
    server.use(http.get('/api/auth/me', () => new HttpResponse(null, { status: 401 })));

    await expect(api.get('/api/auth/me')).rejects.toMatchObject({ status: 401 });
    expect(useAuthStore.getState().status).toBe('anonymous');
    expect(useAuthStore.getState().refreshToken).toBeNull();
  });
});
