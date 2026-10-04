import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminKeys } from '../../admin/shared/keys';
import { checkinQrApi } from './api';

/** Kalit: `['admin','company',id,'checkin-qr']`. */
export function checkinQrKey(companyId: string) {
  return adminKeys.companyCheckinQr(companyId);
}

export function useCheckinQrQuery(companyId: string) {
  return useQuery({
    queryKey: checkinQrKey(companyId),
    queryFn: () => checkinQrApi.get(companyId),
    enabled: companyId !== '',
  });
}

/** Rotatsiya: javob keshga darhol yoziladi va query invalidate qilinadi. */
export function useRotateCheckinQr(companyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['admin', 'companies', companyId, 'checkin-qr', 'rotate'],
    mutationFn: () => checkinQrApi.rotate(companyId),
    onSuccess: (data) => {
      const key = checkinQrKey(companyId);
      queryClient.setQueryData(key, data);
      void queryClient.invalidateQueries({ queryKey: key });
    },
  });
}
