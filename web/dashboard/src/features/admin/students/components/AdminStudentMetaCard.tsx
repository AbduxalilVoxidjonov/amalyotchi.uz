import { Link } from 'react-router-dom';
import { Badge, Card, CardBody, CardHeader, FactGrid, type FactItem } from '@/shared/ui';
import { formatPhone } from '../../shared/format';
import { STUDENT_STATUS_LABEL, type AdminStudentDetail } from '../types';

const TELEGRAM_LABEL = {
  true: { label: "Bog'langan", kind: 'ok' },
  false: { label: "Bog'lanmagan", kind: 'neu' },
} as const;

/**
 * Admin profilidagi qo'shimcha blok: tyutor bilan aloqa, kafedra, Telegram holati va
 * ro'yxatdagi holat. Qolgan bloklar tyutor profilidagi `StudentDetailView` bilan bir xil.
 */
export function AdminStudentMetaCard({ detail }: { detail: AdminStudentDetail }) {
  const status = STUDENT_STATUS_LABEL[detail.adminStatus];
  const telegram = TELEGRAM_LABEL[detail.telegramLinked ? 'true' : 'false'];

  const items: FactItem[] = [
    {
      k: 'Tyutor',
      v: detail.tutor ? (
        <Link to={`/admin/tutors/${detail.tutor.id}`}>{detail.tutor.fullName}</Link>
      ) : (
        'Biriktirilmagan'
      ),
    },
    { k: 'Tyutor telefoni', v: detail.tutor?.phone ? formatPhone(detail.tutor.phone) : '—' },
    { k: 'Kafedra', v: detail.department },
    { k: "Yo'nalish", v: detail.direction },
    { k: 'Telegram', v: <Badge status={telegram.kind}>{telegram.label}</Badge> },
    { k: "Ro'yxatdagi holat", v: <Badge status={status.kind}>{status.label}</Badge> },
  ];

  return (
    <Card as="section" aria-label="Tashkiliy ma'lumot">
      <CardHeader title="Tashkiliy ma'lumot" subtitle={detail.faculty} />
      <CardBody>
        <FactGrid variant="detail" min={200} items={items} />
      </CardBody>
    </Card>
  );
}
