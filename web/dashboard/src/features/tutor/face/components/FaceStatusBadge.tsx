import { Badge } from '@/shared/ui';
import { FACE_STATUS_LABEL, type FaceStatus } from '../types';

export function FaceStatusBadge({ status }: { status: FaceStatus }) {
  const s = FACE_STATUS_LABEL[status];
  return <Badge status={s.kind}>{s.label}</Badge>;
}
