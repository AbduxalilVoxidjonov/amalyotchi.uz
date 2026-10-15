/**
 * Talabaga beriladigan kirish ma'lumoti (v3.34 — asosiy kirish mobil ilova): "Ilovaga kirish ·
 * HEMIS ID: … · Parol: …". Veb (TWA) manzili bo'lsa — "Brauzerda: …" qo'shiladi.
 */
export function studentCredentialsText(hemisId: string, password: string, twaUrl: string | null) {
  const parts = ['Ilovaga kirish', `HEMIS ID: ${hemisId}`, `Parol: ${password}`];
  if (twaUrl) parts.push(`Brauzerda: ${twaUrl}`);
  return parts.join(' · ');
}
