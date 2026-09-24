import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminKeys } from '../../admin/shared/keys';
import { tutorKeys } from '../../tutor/query-keys';
import { checkinQrApi } from './api';
import type { CheckInQrArea } from './types';

/** Mavjud konventsiya: admin — `['admin','company',id,'checkin-qr']`, tyutor — `tutorKeys.companies`. */
export function checkinQrKey(area: CheckInQrArea, companyId: string) {
  return area === 'admin'
    ? adminKeys.companyCheckinQr(companyId)
    : tutorKeys.companies.checkinQr(companyId);
}

export function useCheckinQrQuery(area: CheckInQrArea, companyId: string) {
  return useQuery({
    queryKey: checkinQrKey(area, companyId),
    queryFn: () => checkinQrApi.get(area, companyId),
    enabled: companyId !== '',
  });
}

/** Rotatsiya: javob keshga darhol yoziladi va query invalidate qilinadi. */
export function useRotateCheckinQr(area: CheckInQrArea, companyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [area, 'companies', companyId, 'checkin-qr', 'rotate'],
    mutationFn: () => checkinQrApi.rotate(area, companyId),
    onSuccess: (data) => {
      const key = checkinQrKey(area, companyId);
      queryClient.setQueryData(key, data);
      void queryClient.invalidateQueries({ queryKey: key });
    },
  });
}
