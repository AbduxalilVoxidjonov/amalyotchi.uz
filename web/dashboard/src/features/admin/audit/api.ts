import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../shared/types';
import type { AuditAction, AuditEntry } from './types';

/**
 * Backend: `AdminAuditController` (AdminOnly).
 *   GET /api/admin/audit?q=&page=&pageSize=&action= → Paged<AuditEntry>
 *   `q` — entity nomi/id, sabab, foydalanuvchi ismi; `action` — AuditAction (camelCase), yangisi birinchi.
 */
export const AUDIT_ENDPOINT = '/api/admin/audit';

export interface AuditListParams extends ListParams {
  action?: AuditAction;
}

export const auditApi = {
  list: (params: AuditListParams) =>
    api.get<Paged<AuditEntry>>(AUDIT_ENDPOINT, {
      query: { ...toQuery(params), action: params.action },
    }),
};
