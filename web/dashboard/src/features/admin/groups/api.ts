import { api } from '@/shared/api';
import { toQuery, type ListParams, type Paged } from '../shared/types';
import type { Group } from './types';

/** Backend: `AdminGroupsController`. GET /api/admin/groups?q=&page=&pageSize= → Paged<Group> (`q`: guruh, yo'nalish, fakultet, tyutor). */
export const GROUPS_ENDPOINT = '/api/admin/groups';

export const groupsApi = {
  list: (params: ListParams) => api.get<Paged<Group>>(GROUPS_ENDPOINT, { query: toQuery(params) }),
};
