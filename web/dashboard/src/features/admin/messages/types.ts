import type { ListParams } from '../shared/types';

/**
 * Admin · Xabarlar — faol talabalarga xabar yuborish. Xabar har bir qabul qiluvchining mobil ilova
 * qutisiga tushadi; Telegram bog'langanlarga qo'shimcha ravishda bot orqali ham yuboriladi.
 * Backend kontrakti: `GET/POST /api/admin/messages...` (§ api.ts). Sanalar — ISO satr.
 */

/** `GET /api/admin/messages/recipients` qatori — faol talaba (Telegram bog'langanligi — belgi). */
export interface MessageRecipientRow {
  /** Talabaning `User.Id`. */
  userId: string;
  fullName: string;
  hemisId: string | null;
  /** Telegram bog'langan — xabar bot orqali ham yuboriladi; aks holda faqat ilova qutisida. */
  telegramLinked: boolean;
  /** Bog'lanmagan bo'lsa null. */
  telegramUserId: number | null;
  telegramLinkedAt: string | null;
  /** Talaba botni bloklagan yoki ishga tushirmagan — Telegram xabari yetmaydi (ilova qutisiga baribir tushadi). */
  botBlocked: boolean;
  facultyName: string | null;
  directionName: string | null;
  groupName: string | null;
  course: number | null;
}

/** Oluvchilar filtrlari (barchasi ixtiyoriy, AND). `q` — FISH / HEMIS ID / Telegram ID. */
export interface RecipientFilter {
  q?: string;
  facultyId?: string;
  directionId?: string;
  groupId?: string;
  course?: number;
}

export interface RecipientListParams extends ListParams, RecipientFilter {}

/** `GET /api/admin/messages/recipients/groups` → guruh variantlari (fakultet/yo'nalish/kursga bog'liq). */
export interface RecipientGroupOption {
  id: string;
  name: string;
}

export interface RecipientGroupParams {
  facultyId?: string;
  directionId?: string;
  course?: number;
}

/** Auditoriya: tanlanganlar · filtr bo'yicha · barcha faol talabalar. */
export type MessageAudience =
  | { kind: 'selected'; userIds: string[] }
  | ({ kind: 'filter' } & RecipientFilter)
  | { kind: 'all' };

/** Xabar matni chegarasi (backend validatori bilan bir xil). */
export const MESSAGE_TEXT_MAX = 4000;

/** `POST /api/admin/messages` tanasi. */
export interface SendMessageInput {
  text: string;
  /** Xabar ostiga "Ilovani ochish" (Web App) tugmasi qo'shiladi. */
  attachAppButton: boolean;
  audience: MessageAudience;
}

export type MessageStatus = 'queued' | 'sending' | 'completed';

/**
 * Yuborilgan xabar (ro'yxat, tafsilot, yuborish/retry javobi).
 * `total = sent + failed + blocked + pending + appOnly` — barcha qabul qiluvchilar (ilova qutisi);
 * `appOnly` — Telegram bog'lamagan, xabar faqat ilovada. `status` faqat Telegram yetkazishlari bo'yicha.
 */
export interface MessageSummary {
  id: string;
  text: string;
  createdAt: string;
  createdByName: string;
  /** Server yasagan auditoriya tavsifi ("Tanlangan: 12 ta", "Barcha talabalar" ...). */
  audienceLabel: string;
  attachAppButton: boolean;
  status: MessageStatus;
  total: number;
  sent: number;
  failed: number;
  blocked: number;
  pending: number;
  appOnly: number;
}

/** `appOnly` — Telegram'siz talaba: xabar faqat ilova qutisida (yakuniy holat, navbatga tushmaydi). */
export type DeliveryStatus = 'pending' | 'sent' | 'failed' | 'blocked' | 'appOnly';

/** `GET /api/admin/messages/{id}/deliveries` qatori. */
export interface MessageDeliveryRow {
  userId: string;
  fullName: string;
  hemisId: string | null;
  status: DeliveryStatus;
  error: string | null;
  sentAt: string | null;
  /** Talaba ilova qutisida o'qigan vaqt (null — o'qilmagan). */
  readAt: string | null;
}

export interface DeliveryListParams extends ListParams {
  status?: DeliveryStatus;
}

export const MESSAGE_STATUS_LABEL: Record<
  MessageStatus,
  { label: string; kind: 'ok' | 'info' | 'neu' }
> = {
  queued: { label: 'Navbatda', kind: 'neu' },
  sending: { label: 'Yuborilmoqda', kind: 'info' },
  completed: { label: 'Yakunlangan', kind: 'ok' },
};

export const DELIVERY_STATUS_LABEL: Record<
  DeliveryStatus,
  { label: string; kind: 'ok' | 'bad' | 'late' | 'neu' }
> = {
  pending: { label: 'Navbatda', kind: 'neu' },
  sent: { label: 'Yetkazildi', kind: 'ok' },
  failed: { label: 'Xato', kind: 'bad' },
  blocked: { label: 'Bloklangan', kind: 'late' },
  appOnly: { label: 'Faqat ilovada', kind: 'neu' },
};

/** Ro'yxat/tafsilot hali yakunlanmagan — avtomatik yangilanadi. */
export function isMessageActive(status: MessageStatus | undefined): boolean {
  return status === 'queued' || status === 'sending';
}
