import { fireEvent, screen, waitFor, within } from '@testing-library/react';
// `mockFace`/`lastFaceSubmission` mock ichida qayta tayinlanadi — namespace orqali o'qiladi.
import * as faceMocks from '@/features/face/mocks';
import { FACE_CONSENT_TEXT } from '@/features/face/types';
import { renderApp } from '@/test/render-app';

const { FACE_MESSAGES, MOCK_FACE_PHOTO_URL, setMockFace } = faceMocks;

/** Sarlavha (shell) va sahifa kontenti (lazy) yuklangach. */
async function title() {
  const h1 = await screen.findByRole('heading', { name: 'Yuzni tasdiqlash', level: 1 });
  await screen.findByRole('heading', { name: 'Yuz rasmi', level: 2 });
  return h1;
}
const tabBar = () => screen.queryByRole('navigation', { name: "Bo'limlar" });

function photoFile(name = 'face.jpg', type = 'image/jpeg', bytes = 64): File {
  return new File([new Uint8Array(bytes)], name, { type });
}

/** Yashirin `input[capture=user]` — old kamera rasm qaytargani bilan bir xil. */
async function capture(file: File = photoFile()) {
  fireEvent.change(screen.getByLabelText('Yuz rasmini olish'), { target: { files: [file] } });
  expect(await screen.findByAltText('Olingan yuz rasmi')).toBeInTheDocument();
}

const consent = () => screen.getByRole('checkbox', { name: FACE_CONSENT_TEXT });
const send = () => fireEvent.click(screen.getByRole('button', { name: 'Yuborish' }));

describe('Yuzni tasdiqlash sahifasi (/face)', () => {
  it("'none': forma — old kamera, oval yo'naltiruvchi, rozilik matni", async () => {
    renderApp('/face');
    await title();
    expect(screen.getByText('Yuborilmagan')).toBeInTheDocument();
    expect(screen.getByLabelText('Yuz rasmini olish')).toHaveAttribute('capture', 'user');
    expect(screen.getByText('Yuzingiz oval ichida, yorug‘ joyda bo‘lsin')).toBeInTheDocument();
    expect(consent()).not.toBeChecked();
    expect(screen.getByText(/faqat davomatda shaxsimni tasdiqlash/)).toBeInTheDocument();
  });

  it('rasm va roziliksiz yuborilmaydi — ikkala xato ko‘rinadi', async () => {
    renderApp('/face');
    await title();
    send();
    expect(await screen.findByText('Avval yuzingizni suratga oling.')).toBeInTheDocument();
    expect(screen.getByText('Rozilik berilishi kerak.')).toBeInTheDocument();
    expect(faceMocks.lastFaceSubmission).toBeNull();

    await capture();
    send();
    expect(await screen.findByText('Rozilik berilishi kerak.')).toBeInTheDocument();
    expect(screen.queryByText('Avval yuzingizni suratga oling.')).not.toBeInTheDocument();
    expect(faceMocks.lastFaceSubmission).toBeNull();
  });

  it('muvaffaqiyat → "Tyutor tekshirmoqda", rasm va yuborilgan vaqt', async () => {
    renderApp('/face');
    await title();
    await capture();
    fireEvent.click(consent());
    send();
    expect(await screen.findByText('Tyutor tekshirmoqda')).toBeInTheDocument();
    expect(screen.getByText(/Rasmingiz yuborildi/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Yuborish' })).not.toBeInTheDocument();
    expect(screen.getByText('Yuborilgan')).toBeInTheDocument();
    expect(faceMocks.lastFaceSubmission).toMatchObject({ type: 'image/jpeg', consent: 'true' });
  });

  it('server 400 errors.Photo ("yuz topilmadi") — rasm ostida xabar', async () => {
    renderApp('/face');
    await title();
    await capture(photoFile('noface.jpg'));
    fireEvent.click(consent());
    send();
    expect(await screen.findByText(FACE_MESSAGES.noFace)).toBeInTheDocument();
    expect(faceMocks.mockFace.status).toBe('none');
  });

  it('server 400 errors.Photo ("faqat bitta yuz")', async () => {
    renderApp('/face');
    await title();
    await capture(photoFile('twofaces.jpg'));
    fireEvent.click(consent());
    send();
    expect(await screen.findByText(FACE_MESSAGES.manyFaces)).toBeInTheDocument();
  });

  it("'pending' — holat va rasm, forma yo'q", async () => {
    setMockFace({
      status: 'pending',
      photoUrl: MOCK_FACE_PHOTO_URL,
      submittedAt: '2026-10-12T04:00:00Z',
    });
    renderApp('/face');
    await title();
    expect(screen.getByText('Tyutor tekshirmoqda')).toBeInTheDocument();
    expect(screen.getByText('12.10.2026 09:00')).toBeInTheDocument();
    expect(screen.queryByLabelText('Yuz rasmini olish')).not.toBeInTheDocument();
  });

  it("'approved' — tasdiqlangan holat", async () => {
    setMockFace({
      status: 'approved',
      required: true,
      photoUrl: MOCK_FACE_PHOTO_URL,
      submittedAt: '2026-10-12T04:00:00Z',
      reviewedAt: '2026-10-12T06:30:00Z',
    });
    renderApp('/face');
    await title();
    expect(screen.getByText('Tasdiqlangan')).toBeInTheDocument();
    expect(screen.getByText('Ko‘rib chiqilgan')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Yuborish' })).not.toBeInTheDocument();
  });

  it("'rejected' — sabab ko'rinadi va qayta yuborish mumkin", async () => {
    setMockFace({
      status: 'rejected',
      photoUrl: MOCK_FACE_PHOTO_URL,
      rejectReason: 'Yuz aniq ko‘rinmayapti',
    });
    renderApp('/face');
    await title();
    expect(screen.getByText('Rad etilgan')).toBeInTheDocument();
    expect(screen.getByText(/Sabab: Yuz aniq ko‘rinmayapti/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Rasmni qayta yuborish' })).toBeInTheDocument();
    await capture();
    fireEvent.click(consent());
    send();
    expect(await screen.findByText('Tyutor tekshirmoqda')).toBeInTheDocument();
  });

  it('server 409 (allaqachon tasdiqlangan) — umumiy xabar', async () => {
    renderApp('/face');
    await title();
    await capture();
    fireEvent.click(consent());
    // Sahifa ochilgandan keyin tyutor tasdiqlagan.
    setMockFace({ status: 'approved' });
    send();
    expect(await screen.findByText(FACE_MESSAGES.alreadyApproved)).toBeInTheDocument();
  });
});

describe('Yuz darvozasi', () => {
  it("required + 'none' → istalgan sahifadan /face; tab-bar yo'q, 'Chiqish' bor; yuborilgach ochiladi", async () => {
    setMockFace({ required: true });
    const router = renderApp('/kundalik');
    await title();
    expect(router.state.location.pathname).toBe('/face');
    expect(tabBar()).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Chiqish' })).toBeInTheDocument();

    await capture();
    fireEvent.click(consent());
    send();
    expect(await screen.findByText('Tyutor tekshirmoqda')).toBeInTheDocument();
    await waitFor(() => expect(tabBar()).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Chiqish' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('link', { name: 'Bosh ekranga' }));
    expect(
      await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 }),
    ).toBeInTheDocument();
  });

  it("required + 'rejected' → /face", async () => {
    setMockFace({ required: true, status: 'rejected', rejectReason: 'Xira' });
    const router = renderApp('/');
    await title();
    expect(router.state.location.pathname).toBe('/face');
  });

  it("darvozada 'Chiqish' → hisobdan chiqiladi", async () => {
    setMockFace({ required: true });
    renderApp('/');
    await title();
    fireEvent.click(screen.getByRole('button', { name: 'Chiqish' }));
    expect(await screen.findByRole('heading', { name: 'Hisobdan chiqdingiz' })).toBeInTheDocument();
  });

  it.each(['pending', 'approved'] as const)(
    "required + '%s' → oddiy ilova (yo'naltirilmaydi)",
    async (status) => {
      setMockFace({ required: true, status, photoUrl: MOCK_FACE_PHOTO_URL });
      const router = renderApp('/kundalik');
      expect(
        await screen.findByRole('heading', { name: 'Kundaligim', level: 1 }),
      ).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/kundalik');
      expect(tabBar()).toBeInTheDocument();
    },
  );
});

describe('Profil — "Yuz tasdiqlash" kartasi', () => {
  it('holat va /face havolasi', async () => {
    setMockFace({ status: 'rejected', rejectReason: 'Xira rasm' });
    const router = renderApp('/profil');
    const card = within(
      (await screen.findByRole('heading', { name: 'Yuz tasdiqlash' })).closest('section') ??
        document.body,
    );
    expect(card.getByText('Rad etilgan')).toBeInTheDocument();
    expect(card.getByText(/Rad sababi: Xira rasm/)).toBeInTheDocument();
    fireEvent.click(card.getByRole('link', { name: 'Qayta yuborish' }));
    await title();
    expect(router.state.location.pathname).toBe('/face');
  });
});
