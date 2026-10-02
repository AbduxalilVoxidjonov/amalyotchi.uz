import type { ListParams } from '../shared/types';

/**
 * Admin · Xabarlar — Telegram'ni HEMIS ID bilan bog'lagan talabalarga bot orqali xabar yuborish.
 * Backend kontrakti: `GET/POST /api/admin/messages...` (§ api.ts). Sanalar — ISO satr.
 */

/** `GET /api/admin/messages/recipients` qatori — Telegram ulangan talaba. */
export interface MessageRecipientRow {
  /** Talabaning `User.Id`. */
  userId: string;
  fullName: string;
  hemisId: string | null;
  telegramUserId: number;
  telegramLinkedAt: string | null;
  /** Talaba botni bloklagan yoki ishga tushirmagan — xabar yetmaydi. */
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

/** Auditoriya: tanlanganlar · filtr bo'yicha · barcha ulanganlar. */
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

/** Yuborilgan xabar (ro'yxat, tafsilot, yuborish/retry javobi). `total = sent + failed + blocked + pending`. */
export interface MessageSummary {
  id: string;
  text: string;
  createdAt: string;
  createdByName: string;
  /** Server yasagan auditoriya tavsifi ("Tanlangan: 12 ta", "Barcha ulanganlar" ...). */
  audienceLabel: string;
  attachAppButton: boolean;
  status: MessageStatus;
  total: number;
  sent: number;
  failed: number;
  blocked: number;
  pending: number;
}

export type DeliveryStatus = 'pending' | 'sent' | 'failed' | 'blocked';

/** `GET /api/admin/messages/{id}/deliveries` qatori. */
export interface MessageDeliveryRow {
  userId: string;
  fullName: string;
  hemisId: string | null;
  status: DeliveryStatus;
  error: string | null;
  sentAt: string | null;
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
};

/** Ro'yxat/tafsilot hali yakunlanmagan — avtomatik yangilanadi. */
export function isMessageActive(status: MessageStatus | undefined): boolean {
  return status === 'queued' || status === 'sending';
}
