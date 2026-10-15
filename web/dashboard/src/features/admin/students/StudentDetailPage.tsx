import { Breadcrumb, Button, EmptyState, type BreadcrumbItem } from '@/shared/ui';
import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { isApiError } from '@/shared/api';
import { StudentProfile } from '@/features/admin/student-profile/components/StudentProfile';
import { todayInTashkent } from '@/features/admin/shared/fmt';
import { locationTrackHref } from '@/features/admin/location/paths';
import { periodPhase, usePeriodParam } from '@/features/admin/student-profile/periods';
import { LoadingState } from '../components/PageStatus';
import { HierarchyListPage } from '../faculties/components/HierarchyListPage';
import { AdminStudentMetaCard } from './components/AdminStudentMetaCard';
import { StudentAppCard } from './components/StudentAppCard';
import { StudentCompanyModal } from './components/StudentCompanyModal';
import { StudentWorkHoursCard, StudentWorkHoursModal } from './components/StudentWorkHoursCard';
import { useStudentQuery } from './hooks';
import type { AdminStudentDetail } from './types';
import styles from './StudentDetailPage.module.css';

const ROOT: BreadcrumbItem[] = [{ label: 'Talabalar', to: '/admin/students' }];

/**
 * Biriktirish/o'tkazish tugmasi faqat ochiq davr ko'rinishida: POST har doim guruhning ochiq
 * davriga ta'sir qiladi, shuning uchun yopilgan, tugagan yoki rejadagi davrda yashiriladi.
 * Davr umuman yo'q bo'lsa — faqat sukut ko'rinishida (server 409 bilan sababini aytadi).
 */
function canChangeCompany(
  detail: AdminStudentDetail,
  requestedPeriodId: string | null,
  today: string,
): boolean {
  const shown = detail.periods.find((p) => p.id === detail.selectedPeriodId) ?? null;
  if (!shown) return requestedPeriodId === null;
  return shown.status === 'active' && periodPhase(shown, today) === 'active';
}

/**
 * Admin · Talaba profili (`/admin/students/:studentId`): profil bloklari (davr tanlagichi,
 * sarlavha kartasi, korxona, ariza, davr, kundalik jadval) + "Ilova" kartasi (v3.34: login, parol
 * holati va parol berish, oxirgi kirish, yuz etaloni — tasdiqlash/rad etish/qayta olish) + tashkiliy
 * ma'lumot (kafedra, Telegram). Davomat/kundalik `/api/admin/students/:id/...` dan so'raladi. Tashkiliy blokdagi tugma talabani korxonaga
 * biriktiradi yoki boshqa korxonaga o'tkazadi (faqat shu sahifada — ommaviy biriktirish alohida).
 * Sarlavhadagi "Joylashuv izi" — bugungi kunlik iz sahifasiga (LIVE-JOYLASHUV.md; sana o'sha yerda tanlanadi).
 * "Ish vaqti" kartasi (v3.32) — talabaning kelish/ketish vaqtini FAQAT admin shu yerda belgilaydi.
 */
export function StudentDetailPage() {
  const { studentId = '' } = useParams<{ studentId: string }>();
  const [periodId, setPeriodId] = usePeriodParam();
  const query = useStudentQuery(studentId, periodId);
  const detail = query.data;
  const [companyModalOpen, setCompanyModalOpen] = useState(false);
  const [workHoursModalOpen, setWorkHoursModalOpen] = useState(false);
  const [flash, setFlash] = useState<string | null>(null);
  const today = todayInTashkent();

  function openCompanyModal() {
    setFlash(null);
    setCompanyModalOpen(true);
  }

  function openWorkHoursModal() {
    setFlash(null);
    setWorkHoursModalOpen(true);
  }

  // URL'dagi davr talabaga tegishli emas (backend 404 "Amaliyot davri topilmadi.").
  if (periodId !== null && isApiError(query.error) && query.error.status === 404) {
    return (
      <div className={styles.stack}>
        <Breadcrumb items={ROOT} />
        <EmptyState
          tone="plain"
          title="Amaliyot davri topilmadi."
          description="Havoladagi davr bu talabaga tegishli emas yoki o'chirilgan."
          action={<Button onClick={() => setPeriodId(null)}>Joriy davrni ko'rsatish</Button>}
        />
      </div>
    );
  }

  return (
    <HierarchyListPage
      detailQuery={query}
      rootBreadcrumb={ROOT}
      buildBreadcrumb={(s) => [...ROOT, { label: s.name }]}
      loadingBreadcrumb={[...ROOT, { label: '…' }]}
      notFoundTitle="Talaba topilmadi."
      backTo="/admin/students"
      backLabel="Talabalarga qaytish"
      pageTitle={(s) => s.name}
    >
      {detail ? (
        <div className={styles.stack}>
          {flash && (
            <div className={styles.flash} role="status">
              <span>{flash}</span>
              <button
                type="button"
                className={styles.flashClose}
                aria-label="Xabarni yopish"
                onClick={() => setFlash(null)}
              >
                ×
              </button>
            </div>
          )}
          <StudentProfile
            detail={detail}
            requestedPeriodId={periodId}
            onSelectPeriod={setPeriodId}
            isPlaceholderData={query.isPlaceholderData}
            labelPeriodCompany
            headerActions={
              <div className={styles.headerActions}>
                <Button asChild size="xs">
                  <Link
                    to={locationTrackHref(detail.id, {
                      name: detail.name,
                      date: today,
                      from: 'student',
                    })}
                  >
                    Joylashuv izi
                  </Link>
                </Button>
              </div>
            }
          >
            <StudentAppCard detail={detail} onMessage={setFlash} />
            <AdminStudentMetaCard
              detail={detail}
              onChangeCompany={
                !query.isPlaceholderData && canChangeCompany(detail, periodId, today)
                  ? openCompanyModal
                  : undefined
              }
            />
            {detail.workHours && (
              <StudentWorkHoursCard
                workHours={detail.workHours}
                today={today}
                onEdit={query.isPlaceholderData ? undefined : openWorkHoursModal}
              />
            )}
          </StudentProfile>
          {companyModalOpen && (
            <StudentCompanyModal
              studentId={detail.id}
              studentName={detail.name}
              currentCompany={detail.activeCompany ?? null}
              onClose={() => setCompanyModalOpen(false)}
              onDone={(updated) => {
                const name = updated.activeCompany?.name ?? updated.company?.name;
                setCompanyModalOpen(false);
                setFlash(
                  detail.activeCompany
                    ? `Talaba ${name ? `«${name}» korxonasiga ` : 'boshqa korxonaga '}o'tkazildi.`
                    : `Talaba ${name ? `«${name}» korxonasiga ` : 'korxonaga '}biriktirildi.`,
                );
              }}
            />
          )}
          {workHoursModalOpen && detail.workHours && (
            <StudentWorkHoursModal
              studentId={detail.id}
              studentName={detail.name}
              workHours={detail.workHours}
              today={today}
              onClose={() => setWorkHoursModalOpen(false)}
              onDone={(_updated, message) => {
                setWorkHoursModalOpen(false);
                setFlash(message);
              }}
            />
          )}
        </div>
      ) : (
        <LoadingState />
      )}
    </HierarchyListPage>
  );
}

export default StudentDetailPage;
