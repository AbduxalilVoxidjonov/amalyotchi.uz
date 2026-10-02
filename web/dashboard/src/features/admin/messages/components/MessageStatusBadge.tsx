import { Badge } from '@/shared/ui';
import { MESSAGE_STATUS_LABEL, type MessageStatus } from '../types';

export function MessageStatusBadge({ status }: { status: MessageStatus }) {
  const s = MESSAGE_STATUS_LABEL[status];
  return <Badge status={s.kind}>{s.label}</Badge>;
}
