/**
 * Domain `CalendarDayStatus` (o'qishda hisoblanadi): future — hali kelmagan kun · pending — bugun,
 * oyna hali yopilmagan · present/late/absent/excused · dayOff — ish kuni emas / bayram.
 * SPEC-TOKENS 1.5 ranglari/glyph'lari `DAY_STATUS` da. Bosh ekrandagi davr kunlari (`period-days`)
 * shu enum va yorliqlardan foydalanadi (alohida "Kalendarim" bo'limi olib tashlangan).
 */
export type CalendarDayStatus =
  'future' | 'pending' | 'present' | 'late' | 'absent' | 'excused' | 'dayOff';

export const DAY_STATUS: Record<CalendarDayStatus, { label: string; glyph: string }> = {
  present: { label: 'Keldi', glyph: 'K' },
  late: { label: 'Kech keldi', glyph: 'k' },
  absent: { label: 'Kelmadi', glyph: '×' },
  excused: { label: 'Sababli', glyph: 'S' },
  dayOff: { label: 'Dam olish', glyph: '·' },
  pending: { label: 'Kutilmoqda', glyph: '…' },
  future: { label: 'Kelgusi kun', glyph: '' },
};
