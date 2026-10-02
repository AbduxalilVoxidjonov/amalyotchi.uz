import { api } from '@/shared/api';
import { toQuery, type Paged } from '../shared/types';
import type {
  DeliveryListParams,
  MessageDeliveryRow,
  MessageRecipientRow,
  MessageSummary,
  RecipientGroupOption,
  RecipientGroupParams,
  RecipientListParams,
  SendMessageInput,
} from './types';

/**
 * Backend: `AdminMessagesController` (barchasi admin token bilan).
 * GET  /api/admin/messages/recipients?q&facultyId&directionId&groupId&course&page&pageSize
 *                                             → Paged<MessageRecipientRow> (pageSize 1..500)
 * GET  /api/admin/messages/recipients/groups?facultyId&directionId&course → RecipientGroupOption[]
 * POST /api/admin/messages                    → 201 MessageSummary · 400 (bo'sh/uzun matn,
 *                                               auditoriyada Telegram ulangan talaba yo'q)
 * GET  /api/admin/messages?page&pageSize      → Paged<MessageSummary>
 * GET  /api/admin/messages/{id}               → MessageSummary · 404
 * GET  /api/admin/messages/{id}/deliveries?status&q&page&pageSize → Paged<MessageDeliveryRow>
 * POST /api/admin/messages/{id}/retry         → MessageSummary (failed → qayta navbat; blocked — yo'q)
 * Fakultet/yo'nalish/kurs variantlari — `GET /api/admin/students/filters` (talabalar feature'i).
 */
export const MESSAGES_ENDPOINT = '/api/admin/messages';
export const RECIPIENTS_ENDPOINT = `${MESSAGES_ENDPOINT}/recipients`;
export const RECIPIENT_GROUPS_ENDPOINT = `${RECIPIENTS_ENDPOINT}/groups`;

export const messageEndpoint = (id: string) => `${MESSAGES_ENDPOINT}/${encodeURIComponent(id)}`;

/** Sahifalar marshruti (sidebar, breadcrumb, yuborilgandan keyingi o'tish). */
export const MESSAGES_HREF = '/admin/messages';
export const SENT_MESSAGES_HREF = `${MESSAGES_HREF}?tab=sent`;
export const messageHref = (id: string) => `${MESSAGES_HREF}/${encodeURIComponent(id)}`;

export const messagesApi = {
  recipients: ({ facultyId, directionId, groupId, course, ...params }: RecipientListParams) =>
    api.get<Paged<MessageRecipientRow>>(RECIPIENTS_ENDPOINT, {
      query: {
        ...toQuery(params),
        facultyId: facultyId || undefined,
        directionId: directionId || undefined,
        groupId: groupId || undefined,
        course,
      },
    }),
  recipientGroups: ({ facultyId, directionId, course }: RecipientGroupParams) =>
    api.get<RecipientGroupOption[]>(RECIPIENT_GROUPS_ENDPOINT, {
      query: {
        facultyId: facultyId || undefined,
        directionId: directionId || undefined,
        course,
      },
    }),
  send: (input: SendMessageInput) => api.post<MessageSummary>(MESSAGES_ENDPOINT, input),
  list: (params: { page: number; pageSize: number }) =>
    api.get<Paged<MessageSummary>>(MESSAGES_ENDPOINT, { query: toQuery(params) }),
  detail: (id: string) => api.get<MessageSummary>(messageEndpoint(id)),
  deliveries: (id: string, { status, ...params }: DeliveryListParams) =>
    api.get<Paged<MessageDeliveryRow>>(`${messageEndpoint(id)}/deliveries`, {
      query: { ...toQuery(params), status: status || undefined },
    }),
  retry: (id: string) => api.post<MessageSummary>(`${messageEndpoint(id)}/retry`),
};
