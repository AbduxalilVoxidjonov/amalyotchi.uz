import { useState, type ReactNode } from 'react';
import { ChangePasswordForm } from '@/features/auth/components/ChangePasswordForm';
import { useLogout } from '@/features/auth/hooks';
import { useStudentFaceQuery } from '@/features/face/hooks';
import { FACE_PATH, FACE_STATUS } from '@/features/face/types';
import { useProfileQuery } from '@/features/profile/hooks';
import { PracticePeriodCard } from '@/features/profile/components/PracticePeriodCard';
import {
  formatHoursRange,
  isWorkHoursPending,
  profilePractices,
  tashkentToday,
} from '@/features/profile/lib';
import type {
  StudentProfileDto,
  StudentProfilePracticeDto,
  StudentWorkHoursDto,
} from '@/features/profile/types';
import { errorMessage } from '@/shared/api/client';
import { formatDate, formatPhone } from '@/shared/lib/format';
import { AppLink, Avatar, Badge, Button, Card, ErrorState, LoadingState } from '@/shared/ui';
import pages from './pages.module.css';
import styles from './ProfilePage.module.css';

function InfoRow({ k, children, mono }: { k: string; children: ReactNode; mono?: boolean }) {
  return (
    <div className={styles.row}>
      <dt className={styles.k}>{k}</dt>
      <dd className={`${styles.v}${mono ? ` ${styles.mono}` : ''}`}>{children}</dd>
    </div>
  );
}

const muted = (text: string) => <span className={styles.vMuted}>{text}</span>;

/** Profil: shaxsiy/o'quv ma'lumotlari · barcha amaliyot davrlari · ish vaqti · hisob (Telegram, parol, chiqish). */
export function ProfilePage() {
  const q = useProfileQuery();

  if (q.isPending) return <LoadingState height={360} />;
  if (q.isError) {
    return <ErrorState description={errorMessage(q.error)} onRetry={() => void q.refetch()} />;
  }
  const p = q.data;

  return (
    <div className={pages.stack}>
      <PersonalCard profile={p} />
      <PracticesCard practices={profilePractices(p)} />
      {p.workHours && <WorkHoursCard workHours={p.workHours} />}
      <FaceCard />
      <AccountCard profile={p} />
    </div>
  );
}

function PersonalCard({ profile: p }: { profile: StudentProfileDto }) {
  return (
    <Card padded aria-labelledby="profile-name">
      <div className={styles.head}>
        <Avatar name={p.fullName} variant="detail" />
        <div className={styles.headText}>
          <h2 id="profile-name" className={styles.name}>
            {p.fullName}
          </h2>
          <div className={styles.sub}>
            {p.group} · {p.course}-kurs
          </div>
        </div>
      </div>
      <h3 className={pages.groupTitle}>Shaxsiy va o'quv ma'lumotlari</h3>
      <dl className={styles.info} aria-label="Shaxsiy va o'quv ma'lumotlari">
        <InfoRow k="FISH">{p.fullName}</InfoRow>
        <InfoRow k="HEMIS ID" mono>
          {p.hemisId}
        </InfoRow>
        <InfoRow k="Telefon" mono={Boolean(p.phoneNumber)}>
          {p.phoneNumber ? formatPhone(p.phoneNumber) : muted("Ko'rsatilmagan")}
        </InfoRow>
        <InfoRow k="Fakultet">{p.faculty}</InfoRow>
        <InfoRow k="Kafedra">{p.department}</InfoRow>
        <InfoRow k="Yo'nalish">{p.direction}</InfoRow>
        <InfoRow k="Guruh">{p.group}</InfoRow>
        <InfoRow k="Kurs">{p.course}-kurs</InfoRow>
      </dl>
    </Card>
  );
}

function PracticesCard({ practices }: { practices: StudentProfilePracticeDto[] }) {
  return (
    <Card padded aria-labelledby="profile-practice">
      <div className={styles.sectionHead}>
        <h2 id="profile-practice" className={pages.sectionTitle}>
          Amaliyot xulosasi
        </h2>
        {practices.length > 1 && <span className={styles.count}>{practices.length} ta davr</span>}
      </div>
      {practices.length === 0 ? (
        <p className={styles.empty}>Amaliyot davri biriktirilmagan.</p>
      ) : (
        <div className={styles.periods}>
          {practices.map((practice) => (
            <PracticePeriodCard key={practice.period.id} practice={practice} />
          ))}
        </div>
      )}
    </Card>
  );
}

/**
 * "Ish vaqtim" (faqat o'qish uchun): bugun amaldagi kelish–ketish vaqti (admin belgilagan yoki davrniki) va
 * kutilayotgan o'zgarish. Ish vaqtini FAQAT admin belgilaydi — ilovada tahrirlash yo'q (kontrakt v3.32).
 */
function WorkHoursCard({ workHours: wh }: { workHours: StudentWorkHoursDto }) {
  const today = tashkentToday();
  const pending = isWorkHoursPending(wh, today);
  const periodRange = formatHoursRange(wh.periodStart, wh.periodEnd);
  const todayRange = formatHoursRange(wh.todayStart, wh.todayEnd) ?? '—';
  // Bugungi vaqt adminniki: kutilayotgan o'zgarish bo'lsa — davr vaqtidan farq qilsa; aks holda `start` bor.
  const customToday = pending
    ? Boolean(periodRange) && todayRange !== periodRange
    : wh.start !== null;

  return (
    <Card padded aria-labelledby="profile-work-hours">
      <div className={styles.sectionHead}>
        <h2 id="profile-work-hours" className={pages.sectionTitle}>
          Ish vaqtim
        </h2>
      </div>
      <dl className={styles.info}>
        <InfoRow k="Bugun" mono>
          {todayRange}
        </InfoRow>
        <InfoRow k="Manba">
          {customToday ? (
            <Badge status="info" size="sm">
              Admin belgilagan
            </Badge>
          ) : (
            <Badge status="neu" size="sm">
              Davr vaqti
            </Badge>
          )}
        </InfoRow>
        <InfoRow k="Davr vaqti" mono={Boolean(periodRange)}>
          {periodRange ?? muted('Davr biriktirilmagan')}
        </InfoRow>
      </dl>

      {pending && wh.effectiveFrom && (
        <p className={styles.pending} role="note">
          {wh.start && wh.end
            ? `${formatDate(wh.effectiveFrom)} dan yangi vaqt: ${formatHoursRange(wh.start, wh.end)}.`
            : `${formatDate(wh.effectiveFrom)} dan davr vaqtiga qaytadi${periodRange ? ` (${periodRange})` : ''}.`}
        </p>
      )}

      <p className={styles.note}>
        Ish vaqtini admin belgilaydi. O'zgartirish kerak bo'lsa, amaliyot rahbaringizga murojaat
        qiling.
      </p>
    </Card>
  );
}

/**
 * "Yuz tasdiqlash": etalon holati + `/face` ga havola. So'rov xatosi (eski server) — karta ko'rsatilmaydi.
 */
function FaceCard() {
  const q = useStudentFaceQuery();
  if (!q.data) return null;
  const face = q.data;
  const meta = FACE_STATUS[face.status];
  const action =
    face.status === 'none'
      ? 'Rasm yuborish'
      : face.status === 'rejected'
        ? 'Qayta yuborish'
        : "Ko'rish";
  return (
    <Card padded aria-labelledby="profile-face">
      <div className={styles.sectionHead}>
        <h2 id="profile-face" className={pages.sectionTitle}>
          Yuz tasdiqlash
        </h2>
        <Badge status={meta.badge} size="sm">
          {meta.label}
        </Badge>
      </div>
      <p className={styles.note}>
        {face.required
          ? 'Davomat selfisi tasdiqlangan yuz rasmingiz bilan solishtiriladi.'
          : 'Yuz rasmi davomatda shaxsingizni tasdiqlash uchun ishlatiladi.'}
        {face.status === 'rejected' && face.rejectReason ? ` Rad sababi: ${face.rejectReason}` : ''}
      </p>
      <Button asChild block className={styles.faceLink}>
        <AppLink to={FACE_PATH}>{action}</AppLink>
      </Button>
    </Card>
  );
}

function AccountCard({ profile: p }: { profile: StudentProfileDto }) {
  const logout = useLogout();
  const [pwOpen, setPwOpen] = useState(false);
  const [pwChanged, setPwChanged] = useState(false);

  return (
    <Card padded aria-labelledby="profile-account">
      <div className={styles.sectionHead}>
        <h2 id="profile-account" className={pages.sectionTitle}>
          Hisob
        </h2>
      </div>
      <dl className={styles.info}>
        <InfoRow k="Telegram">
          {p.telegramLinked ? (
            <Badge status="ok" size="sm">
              Bog'langan
            </Badge>
          ) : (
            <Badge status="neu" size="sm">
              Bog'lanmagan
            </Badge>
          )}
        </InfoRow>
      </dl>
      {!p.telegramLinked && (
        <p className={styles.note}>Bot tayyor bo'lgach Telegram orqali ham kira olasiz.</p>
      )}

      <div className={styles.accountActions}>
        {pwChanged && !pwOpen && (
          <p className={styles.success} role="status">
            Parol o'zgartirildi.
          </p>
        )}
        {p.hasPassword ? (
          pwOpen ? (
            <div className={styles.pwForm}>
              <ChangePasswordForm
                onSuccess={() => {
                  setPwOpen(false);
                  setPwChanged(true);
                }}
                onCancel={() => setPwOpen(false)}
              />
            </div>
          ) : (
            <Button
              block
              onClick={() => {
                setPwChanged(false);
                setPwOpen(true);
              }}
            >
              Parolni o'zgartirish
            </Button>
          )
        ) : (
          <p className={styles.note}>
            Parol hali berilmagan — web orqali kirish uchun administratorga murojaat qiling.
          </p>
        )}
        <Button variant="danger" block onClick={() => logout.mutate()} disabled={logout.isPending}>
          {logout.isPending ? 'Chiqilmoqda…' : 'Chiqish'}
        </Button>
      </div>
    </Card>
  );
}

export default ProfilePage;
