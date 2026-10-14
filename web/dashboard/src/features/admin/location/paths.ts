/** Iz sahifasiga qayerdan kelingani — breadcrumb shunga qarab quriladi (sukut: "Bugun"). */
export type LocationTrackFrom = 'today' | 'student';

/** Kunlik joylashuv izi sahifasi (Bugun ostida — sidebar'da "Bugun" faol qoladi). */
export function locationTrackHref(
  studentId: string,
  opts: {
    name?: string | undefined;
    date?: string | undefined;
    from?: LocationTrackFrom | undefined;
  } = {},
): string {
  const sp = new URLSearchParams();
  if (opts.date) sp.set('date', opts.date);
  // Javobda talaba ismi yo'q — sarlavha uchun havolada uzatiladi (yangilashda ham saqlanadi).
  if (opts.name) sp.set('name', opts.name);
  if (opts.from === 'student') sp.set('from', 'student');
  const qs = sp.toString();
  return `/admin/today/students/${encodeURIComponent(studentId)}/location${qs ? `?${qs}` : ''}`;
}
