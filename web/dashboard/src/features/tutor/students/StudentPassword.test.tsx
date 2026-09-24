import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { server } from '@/mocks/server';
import { renderTutorRoute } from '../test-utils';

async function profile(name: string) {
  return within(await screen.findByRole('article', { name: `Talaba: ${name}` }));
}

describe("Tyutor talaba profili · Parol o'rnatish", () => {
  it("parol o'rnatilgan talaba — belgi va tugma", async () => {
    renderTutorRoute('/tutor/students/s-341030');
    const p = await profile('Aliyev Akmal');
    expect(p.getByText("Parol o'rnatilgan")).toBeInTheDocument();
    expect(p.getByRole('button', { name: "Parol o'rnatish" })).toBeInTheDocument();
  });

  it("parol yo'q talaba → o'rnatish (tyutor endpoint'i) → muvaffaqiyat → belgi yangilanadi", async () => {
    const user = userEvent.setup();
    const urls: string[] = [];
    server.events.on('request:start', ({ request }) => {
      if (request.method === 'POST') urls.push(new URL(request.url).pathname);
    });
    renderTutorRoute('/tutor/students/s-341031');
    const p = await profile('Karimov Bekzod');
    expect(p.getByText("Parol yo'q")).toBeInTheDocument();

    await user.click(p.getByRole('button', { name: "Parol o'rnatish" }));
    const dialog = await screen.findByRole('dialog', { name: "Parol o'rnatish" });
    await user.click(within(dialog).getByRole('button', { name: 'Parol yaratish' }));
    const generated = (within(dialog).getByLabelText('Yangi parol') as HTMLInputElement).value;
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    const done = await screen.findByRole('dialog', { name: "Parol o'rnatildi" });
    expect(within(done).getByTestId('student-credentials')).toHaveTextContent(
      `HEMIS ID: 341031 · Parol: ${generated}`,
    );
    expect(urls).toContain('/api/tutor/students/s-341031/password');

    await user.click(within(done).getByRole('button', { name: 'Tayyor' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(
      await (await profile('Karimov Bekzod')).findByText("Parol o'rnatilgan"),
    ).toBeInTheDocument();
    server.events.removeAllListeners();
  });
});
