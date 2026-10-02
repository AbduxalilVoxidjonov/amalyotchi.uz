import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminKeys } from '../shared/keys';
import { messagesApi } from './api';
import {
  isMessageActive,
  type DeliveryListParams,
  type MessageSummary,
  type RecipientGroupParams,
  type RecipientListParams,
  type SendMessageInput,
} from './types';

/** Yuborish davom etayotganda ro'yxat/tafsilot shu oraliqda qayta so'raladi (tugagach to'xtaydi). */
export const MESSAGE_POLL_MS = 3000;

/** Telegram ulangan talabalar (filtr + sahifa). */
export function useRecipientsQuery(params: RecipientListParams) {
  return useQuery({
    queryKey: adminKeys.messageRecipients(params),
    queryFn: () => messagesApi.recipients(params),
    placeholderData: keepPreviousData,
  });
}

/** Guruh variantlari — fakultet/yo'nalish/kurs tanloviga bog'liq. */
export function useRecipientGroups(params: RecipientGroupParams) {
  return useQuery({
    queryKey: adminKeys.messageRecipientGroups(params),
    queryFn: () => messagesApi.recipientGroups(params),
    staleTime: 5 * 60_000,
    placeholderData: keepPreviousData,
  });
}

/** Yuborilganlar ro'yxati. Navbatda/yuborilayotgan xabar bo'lsa — har 3 s yangilanadi. */
export function useSentMessagesQuery(params: { page: number; pageSize: number }) {
  return useQuery({
    queryKey: adminKeys.messagesSent(params),
    queryFn: () => messagesApi.list(params),
    placeholderData: keepPreviousData,
    refetchInterval: (query) =>
      query.state.data?.items.some((m) => isMessageActive(m.status)) ? MESSAGE_POLL_MS : false,
  });
}

/** Xabar tafsiloti — yakunlanmaguncha har 3 s yangilanadi. */
export function useMessageQuery(id: string) {
  return useQuery({
    queryKey: adminKeys.message(id),
    queryFn: () => messagesApi.detail(id),
    enabled: id !== '',
    refetchInterval: (query) =>
      isMessageActive(query.state.data?.status) ? MESSAGE_POLL_MS : false,
  });
}

/** Yetkazishlar jadvali. `live` — xabar hali yuborilmoqda (holatlar o'zgarib turadi). */
export function useDeliveriesQuery(id: string, params: DeliveryListParams, live: boolean) {
  return useQuery({
    queryKey: adminKeys.messageDeliveries(id, params),
    queryFn: () => messagesApi.deliveries(id, params),
    enabled: id !== '',
    placeholderData: keepPreviousData,
    refetchInterval: live ? MESSAGE_POLL_MS : false,
  });
}

/** Javobni tafsilot keshiga yozish + ro'yxat/yetkazishlarni yangilash (yuborish va retry uchun umumiy). */
function useApplySummary() {
  const queryClient = useQueryClient();
  return (summary: MessageSummary) => {
    queryClient.setQueryData(adminKeys.message(summary.id), summary);
    void queryClient.invalidateQueries({ queryKey: adminKeys.messagesSentAll() });
    void queryClient.invalidateQueries({ queryKey: adminKeys.messageDeliveriesAll(summary.id) });
  };
}

/** Yangi xabar yuborish (navbatga qo'yish). */
export function useSendMessage() {
  const apply = useApplySummary();
  return useMutation({
    mutationKey: ['admin', 'messages', 'send'],
    mutationFn: (input: SendMessageInput) => messagesApi.send(input),
    onSuccess: apply,
  });
}

/** Xato bilan tugaganlarni qayta yuborish (bloklaganlar qayta yuborilmaydi). */
export function useRetryMessage(id: string) {
  const apply = useApplySummary();
  return useMutation({
    mutationKey: ['admin', 'message', id, 'retry'],
    mutationFn: () => messagesApi.retry(id),
    onSuccess: apply,
  });
}
