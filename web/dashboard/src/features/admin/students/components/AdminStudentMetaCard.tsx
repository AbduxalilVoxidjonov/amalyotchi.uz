import { Link } from 'react-router-dom';
import { Badge, Button, Card, CardBody, CardHeader, FactGrid, type FactItem } from '@/shared/ui';
import { formatPhone } from '../../shared/format';
import { STUDENT_STATUS_LABEL, type ActiveCompanyRef, type AdminStudentDetail } from '../types';
import styles from './AdminStudentMetaCard.module.css';

const TELEGRAM_LABEL = {
  true: { label: "Bog'langan", kind: 'ok' },
  false: { label: "Bog'lanmagan", kind: 'neu' },
} as const;

/**
 * Korxona amali tugmasi matni: HOZIR aktiv korxona bo'lsa o'tkazish, aks holda birinchi biriktirish.
 * Tanlangan davrdagi (tarixiy) `company` hisobga olinmaydi.
 */
function companyActionLabel(activeCompany: ActiveCompanyRef | null | undefined): string {
  return activeCompany ? "Boshqa korxonaga o'tkazish" : 'Korxonaga biriktirish';
}

/** "Korxona" qatori — faqat aktiv korxona (davr nomi izoh sifatida); yo'q bo'lsa xira matn. */
function ActiveCompanyValue({ company }: { company: ActiveCompanyRef | null }) {
  if (!company) return <span className={styles.none}>Aktiv korxona yo'q</span>;
  return (
    <span className={styles.company}>
      <Link to={`/admin/companies/${encodeURIComponent(company.id)}`}>{company.name}</Link>
      <span className={styles.companyPeriod}>· {company.periodName}</span>
    </span>
  );
}

/**
 * Admin profilidagi qo'shimcha blok: HOZIRGI aktiv korxona — `activeCompany`, tanlangan davrdan
 * mustaqil (biriktirish / o'tkazish tugmasi bilan),
 * tyutor bilan aloqa, kafedra, Telegram holati va ro'yxatdagi holat. Qolgan bloklar tyutor
 * profilidagi `StudentDetailView` bilan bir xil.
 */
export function AdminStudentMetaCard({
  detail,
  onChangeCompany,
}: {
  detail: AdminStudentDetail;
  /** Berilsa — sarlavhada "Korxonaga biriktirish" / "Boshqa korxonaga o'tkazish" tugmasi. */
  onChangeCompany?: (() => void) | undefined;
}) {
  const status = STUDENT_STATUS_LABEL[detail.adminStatus];
  const telegram = TELEGRAM_LABEL[detail.telegramLinked ? 'true' : 'false'];

  const items: FactItem[] = [
    { k: 'Korxona', v: <ActiveCompanyValue company={detail.activeCompany ?? null} /> },
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
      <CardHeader
        title="Tashkiliy ma'lumot"
        subtitle={detail.faculty}
        actions={
          onChangeCompany && (
            <Button size="sm" onClick={onChangeCompany}>
              {companyActionLabel(detail.activeCompany)}
            </Button>
          )
        }
      />
      <CardBody>
        <FactGrid variant="detail" min={200} items={items} />
      </CardBody>
    </Card>
  );
}
