import { isApiError } from './api-error';
import { createApiClient } from './client';

function clientReturning(response: Response) {
  return createApiClient({ fetch: () => Promise.resolve(response) });
}

async function errorOf(promise: Promise<unknown>) {
  try {
    await promise;
  } catch (e) {
    if (isApiError(e)) return e;
    throw e;
  }
  throw new Error('xato kutilgandi');
}

describe('createApiClient — ProblemDetails bo‘lmagan xato javoblari', () => {
  it('nginx 502 HTML → tushunarli o‘zbekcha matn ("Bad Gateway"/"HTTP 502" emas)', async () => {
    const res = new Response('<html><body>502 Bad Gateway</body></html>', {
      status: 502,
      statusText: 'Bad Gateway',
      headers: { 'content-type': 'text/html' },
    });
    const err = await errorOf(clientReturning(res).get('/api/x'));
    expect(err.kind).toBe('server');
    expect(err.message).toMatch(/Server vaqtincha javob bermayapti/);
  });

  it('429 → so‘rovlar ko‘pligi haqida', async () => {
    const err = await errorOf(clientReturning(new Response('', { status: 429 })).get('/api/x'));
    expect(err.kind).toBe('rate-limited');
    expect(err.message).toMatch(/juda ko'p/);
  });

  it('ProblemDetails bo‘lsa uning detail matni ishlatiladi', async () => {
    const res = new Response(
      JSON.stringify({ status: 500, title: 'Ichki xatolik', detail: 'Kutilmagan xatolik.' }),
      { status: 500, headers: { 'content-type': 'application/problem+json' } },
    );
    const err = await errorOf(clientReturning(res).get('/api/x'));
    expect(err.message).toBe('Kutilmagan xatolik.');
  });
});
