import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { adminKeys } from '../shared/keys';
import { auditApi, type AuditListParams } from './api';

export function useAuditQuery(params: AuditListParams) {
  return useQuery({
    queryKey: adminKeys.audit(params),
    queryFn: () => auditApi.list(params),
    placeholderData: keepPreviousData,
  });
}
