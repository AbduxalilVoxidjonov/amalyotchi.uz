import { messagesApi } from './api';
import { mockRecipients } from './mocks';

/** Mock simulyatsiyasi: dev rejimida avtomatik yangilanish va retry oqimini ko'rish mumkin bo'lsin. */
describe('messages mock', () => {
  it('queued → sending → completed (bir necha so‘rovda); retry xatolarni qayta yuboradi', async () => {
    const created = await messagesApi.send({
      text: 'Salom',
      attachAppButton: true,
      audience: { kind: 'all' },
    });
    expect(created).toMatchObject({
      status: 'queued',
      total: mockRecipients.length,
      pending: 44,
      appOnly: 2,
    });

    const seen = new Set<string>();
    let current = created;
    for (let i = 0; i < 10 && current.status !== 'completed'; i += 1) {
      current = await messagesApi.detail(created.id);
      seen.add(current.status);
    }
    expect([...seen]).toEqual(['sending', 'completed']);
    expect(current).toMatchObject({ sent: 40, failed: 2, blocked: 2, pending: 0 });

    const retried = await messagesApi.retry(created.id);
    expect(retried).toMatchObject({ status: 'queued', failed: 0, pending: 2 });
    let after = retried;
    for (let i = 0; i < 5 && after.status !== 'completed'; i += 1) {
      after = await messagesApi.detail(created.id);
    }
    // Bloklaganlar qayta yuborilmaydi; 429 bo'lganlar ikkinchi urinishda yetkaziladi.
    expect(after).toMatchObject({ status: 'completed', sent: 42, failed: 0, blocked: 2 });
  });

  it('faqat Telegram’siz talabalar — navbatsiz, darhol yakunlangan (appOnly)', async () => {
    const unlinked = mockRecipients.filter((r) => !r.telegramLinked).map((r) => r.userId);
    const created = await messagesApi.send({
      text: 'Ilovaga',
      attachAppButton: false,
      audience: { kind: 'selected', userIds: unlinked },
    });
    expect(created).toMatchObject({ total: 2, appOnly: 2, pending: 0 });
    const detail = await messagesApi.detail(created.id);
    expect(detail).toMatchObject({ status: 'completed', sent: 0, appOnly: 2 });
  });

  it('auditoriyada hech kim yo‘q — 400', async () => {
    await expect(
      messagesApi.send({
        text: 'Salom',
        attachAppButton: false,
        audience: { kind: 'filter', groupId: 'yo‘q' },
      }),
    ).rejects.toMatchObject({ status: 400 });
  });
});
