import { useState, type ReactNode } from 'react';
import { ChangePasswordForm } from '@/features/auth/components/ChangePasswordForm';
import { useLogout } from '@/features/auth/hooks';
import { useProfileQuery } from '@/features/profile/hooks';
import {
  PERIOD_STATUS_LABEL,
  type StudentProfileDto,
  type StudentProfilePracticeDto,
} from '@/features/profile/types';
import { errorMessage } from '@/shared/api/client';
import { formatDecimal, formatPercent, formatPeriod, formatPhone } from '@/shared/lib/format';
import {
  Avatar,
  Badge,
  Button,
  Card,
  ErrorState,
  FactGrid,
  LoadingState,
  ProgressBar,
  type StatusKind,
} from '@/shared/ui';
import pages from './pages.module.css';
import styles from './ProfilePage.module.css';

const PERIOD_STATUS_KIND: Record<StudentProfilePracticeDto['period']['status'], StatusKind> = {
  planned: 'info',
  active: 'ok',
  closed: 'neu',
};

function InfoRow({ k, children, mono }: { k: string; children: ReactNode; mono?: boolean }) {
  return (
    <div className={styles.row}>
      <dt className={styles.k}>{k}</dt>
      <dd className={`${styles.v}${mono ? ` ${styles.mono}` : ''}`}>{children}</dd>
    </div>
  );
}

const muted = (text: string) => <span className={styles.vMuted}>{text}</span>;

/** Profil: shaxsiy/o'quv ma'lumotlari · amaliyot xulosasi · hisob (Telegram, parol, chiqish). */
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
      <PracticeCard practice={p.practice} />
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

function PracticeCard({ practice }: { practice: StudentProfilePracticeDto | null }) {
  if (!practice) {
    return (
      <Card padded aria-labelledby="profile-practice">
        <h2 id="profile-practice" className={pages.sectionTitle}>
          Amaliyot xulosasi
        </h2>
        <p className={styles.empty}>Amaliyot davri biriktirilmagan.</p>
      </Card>
    );
  }
  const { period, company } = practice;
  return (
    <Card padded aria-labelledby="profile-practice">
      <div className={styles.sectionHead}>
        <h2 id="profile-practice" className={pages.sectionTitle}>
          Amaliyot xulosasi
        </h2>
        {!practice.finalized && (
          <Badge
            status="info"
            size="sm"
            title="Davr yakunlanmagan — ko'rsatkichlar o'zgarib boradi"
          >
            Joriy hisob
          </Badge>
        )}
      </div>

      <div className={`${styles.block} ${styles.blockFirst}`}>
        <div className={styles.blockTitle}>Davr</div>
        <div className={styles.blockMain}>
          <span>{period.name}</span>
          <Badge status={PERIOD_STATUS_KIND[period.status]} size="sm">
            {PERIOD_STATUS_LABEL[period.status]}
          </Badge>
        </div>
        <div className={`${styles.blockSub} ${styles.mono}`}>
          {formatPeriod(period.startDate, period.endDate)}
        </div>
      </div>

      <div className={styles.block}>
        <div className={styles.blockTitle}>Korxona</div>
        {company ? (
          <>
            <div className={styles.blockMain}>{company.name}</div>
            {company.address && <div className={styles.blockSub}>{company.address}</div>}
          </>
        ) : (
          <div className={styles.blockSub}>Korxona hali biriktirilmagan</div>
        )}
      </div>

      <div className={styles.block}>
        <div className={styles.blockTitle}>Davomat</div>
        <ProgressBar value={practice.attendancePct} label="Davomat" />
      </div>

      <FactGrid
        className={styles.facts}
        columns={3}
        items={[
          // Kontrakt v3.8: sababli (ruxsat berilgan) kunlar ham shu songa kiradi.
          {
            k: <span title="Sababli kunlar bilan birga">O'tgan ish kunlari</span>,
            v: String(practice.elapsedWorkDays),
          },
          {
            k: 'Shubhali kunlar',
            v: String(practice.suspiciousDays),
            tone: practice.suspiciousDays > 0 ? 'late' : 'default',
          },
          {
            k: practice.finalized ? 'Yakuniy ball' : 'Joriy ball',
            v: formatDecimal(practice.total),
          },
        ]}
      />

      <div className={styles.gradeLine}>
        {practice.grade !== null ? (
          <Badge
            status={practice.grade >= 4 ? 'ok' : practice.grade === 3 ? 'late' : 'bad'}
            size="lg"
          >
            Baho: {practice.grade}
            {practice.finalized ? '' : ' (joriy)'}
          </Badge>
        ) : (
          <Badge status="neu" size="lg">
            Baho hali yo'q
          </Badge>
        )}
        <span className={styles.blockSub}>Davomat {formatPercent(practice.attendancePct)}</span>
      </div>
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
