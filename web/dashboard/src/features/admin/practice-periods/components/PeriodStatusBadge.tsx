import { Badge } from '@/shared/ui';
import { PERIOD_STATUS_LABEL, type PracticePeriodStatus } from '../types';

export function PeriodStatusBadge({ status }: { status: PracticePeriodStatus }) {
  const { label, kind } = PERIOD_STATUS_LABEL[status];
  return <Badge status={kind}>{label}</Badge>;
}
