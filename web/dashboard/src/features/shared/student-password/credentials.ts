/** Talabaga beriladigan kirish ma'lumoti (TWA manzili bo'lmasa "Kirish" qismi tushiriladi). */
export function studentCredentialsText(hemisId: string, password: string, twaUrl: string | null) {
  const parts = [`HEMIS ID: ${hemisId}`, `Parol: ${password}`];
  if (twaUrl) parts.push(`Kirish: ${twaUrl}`);
  return parts.join(' · ');
}
