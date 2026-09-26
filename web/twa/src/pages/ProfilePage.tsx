import { useState, type ReactNode } from 'react';
import { ChangePasswordForm } from '@/features/auth/components/ChangePasswordForm';
import { useLogout } from '@/features/auth/hooks';
import { useProfileQuery } from '@/features/profile/hooks';
import { PracticePeriodCard } from '@/features/profile/components/PracticePeriodCard';
import { profilePractices } from '@/features/profile/lib';
import type { StudentProfileDto, StudentProfilePracticeDto } from '@/features/profile/types';
import { errorMessage } from '@/shared/api/client';
import { formatPhone } from '@/shared/lib/format';
import { Avatar, Badge, Button, Card, ErrorState, LoadingState } from '@/shared/ui';
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

/** Profil: shaxsiy/o'quv ma'lumotlari · barcha amaliyot davrlari · hisob (Telegram, parol, chiqish). */
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
        <InfoRow k="Tyutor">{p.tutor ? p.tutor.fullName : muted('Biriktirilmagan')}</InfoRow>
        {p.tutor?.phoneNumber && (
          <InfoRow k="Tyutor telefoni" mono>
            <a href={`tel:${p.tutor.phoneNumber}`}>{formatPhone(p.tutor.phoneNumber)}</a>
          </InfoRow>
        )}
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
            Parol hali berilmagan — web orqali kirish uchun tyutoringizga murojaat qiling.
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
