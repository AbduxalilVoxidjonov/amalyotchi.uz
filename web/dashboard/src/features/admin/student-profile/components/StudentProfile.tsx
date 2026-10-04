import type { ReactNode } from 'react';
import { StudentFaceCard } from '../../face/components/StudentFaceCard';
import { todayInTashkent } from '../../shared/fmt';
import type { StudentProfileDetail } from '../types';
import { StudentAttendanceSection } from './StudentAttendanceSection';
import { StudentDetailView } from './StudentDetailView';
import { StudentPeriodSwitcher } from './StudentPeriodSwitcher';
import styles from './StudentProfile.module.css';

export interface StudentProfileProps {
  detail: StudentProfileDetail;
  /** URL'dagi `?period=` (null — backend sukut davri, `detail.selectedPeriodId`). */
  requestedPeriodId: string | null;
  onSelectPeriod: (periodId: string) => void;
  /** Yangi davr yuklanmoqda — eski ma'lumot xiralashgan holda ko'rinadi. */
  isPlaceholderData: boolean;
  /** Profil bloklari va davomat jadvali orasidagi qo'shimcha bloklar (admin: tashkiliy ma'lumot). */
  children?: ReactNode;
  /** Sarlavha kartasidagi amallar (parol o'rnatish). */
  headerActions?: ReactNode;
  /**
   * Korxona blokini yopilgan/tugagan davrda (aktiv korxonadan farq qilsa) "tanlangan davr" deb
   * belgilash — admin profilida "joriy korxona" alohida ko'rsatilgani uchun. Sukut: false.
   */
  labelPeriodCompany?: boolean;
}

/**
 * Talaba profili (admin): davr tanlagichi + tanlangan davr bo'yicha bloklar.
 * Tanlagich darhol yangi davrni ko'rsatadi; bloklar yangi javob kelguncha eskisini xira ko'rsatadi.
 */
export function StudentProfile({
  detail,
  requestedPeriodId,
  onSelectPeriod,
  isPlaceholderData,
  children,
  headerActions,
  labelPeriodCompany = false,
}: StudentProfileProps) {
  const today = todayInTashkent();
  const selectedId = requestedPeriodId ?? detail.selectedPeriodId;
  // Bloklar — javobdagi (yuklangan) davr bo'yicha; tanlagich esa so'ralgan davrni belgilaydi.
  const shownPeriod = detail.periods.find((p) => p.id === detail.selectedPeriodId) ?? null;

  return (
    <>
      <StudentPeriodSwitcher
        periods={detail.periods}
        selectedId={selectedId}
        onSelect={onSelectPeriod}
        today={today}
        pending={isPlaceholderData}
      />
      <div
        className={styles.body}
        data-stale={isPlaceholderData || undefined}
        aria-busy={isPlaceholderData || undefined}
      >
        <StudentDetailView
          detail={detail}
          selectedPeriod={shownPeriod}
          today={today}
          actions={headerActions}
          labelPeriodCompany={labelPeriodCompany}
        />
        {/* v3.27: "Yuz" kartasi — server `face` yuborsa; v3.28 dan amallar adminda. */}
        {detail.face && (
          <StudentFaceCard
            studentId={detail.id}
            studentName={detail.name}
            face={detail.face}
            canManage
          />
        )}
        {children}
        <StudentAttendanceSection detail={detail} period={shownPeriod} />
      </div>
    </>
  );
}
