import type { CheckinFlow } from '../hooks';
import styles from './CheckinSteps.module.css';

type StepState = 'done' | 'current' | 'todo' | 'error';

interface Step {
  key: 'qr' | 'location' | 'selfie';
  label: string;
  state: StepState;
}

const STATE_LABEL: Record<StepState, string> = {
  done: 'bajarildi',
  current: 'joriy qadam',
  todo: 'navbatda',
  error: 'xato',
};

/** Qadamlar holati `useCheckinFlow` dan hisoblanadi: 1 QR → 2 Selfi → 3 Joylashuv. */
function buildSteps(flow: CheckinFlow): Step[] {
  const { phase, requirements, qr, location, photo } = flow;
  const steps: Step[] = [];
  if (requirements.qrRequired) {
    steps.push({ key: 'qr', label: 'QR', state: qr ? 'done' : 'current' });
  }
  const afterQr = (!requirements.qrRequired || qr !== null) && phase !== 'qr';
  const selfieState: StepState =
    phase === 'location' || phase === 'done' || (phase === 'preview' && photo)
      ? 'done'
      : afterQr
        ? 'current'
        : 'todo';
  steps.push({ key: 'selfie', label: 'Selfi', state: selfieState });
  // Joylashuv fonda olinishi mumkin, lekin qadam sifatida faqat selfidan keyin ko'rsatiladi.
  const locationState: StepState =
    phase === 'done'
      ? 'done'
      : phase !== 'location'
        ? 'todo'
        : location === 'error'
          ? 'error'
          : location === 'ok'
            ? 'done'
            : 'current';
  steps.push({ key: 'location', label: 'Joylashuv', state: locationState });
  return steps;
}

/** Belgilanish qadamlari: 1 QR · 2 Selfi · 3 Joylashuv (QR talab qilinmasa — 2 qadam). */
export function CheckinSteps({ flow }: { flow: CheckinFlow }) {
  const steps = buildSteps(flow);
  return (
    <ol className={styles.steps} aria-label="Belgilanish qadamlari">
      {steps.map((step, i) => (
        <li
          key={step.key}
          className={styles.step}
          data-state={step.state}
          aria-current={step.state === 'current' ? 'step' : undefined}
        >
          <span className={styles.num} aria-hidden="true">
            {step.state === 'done' ? '✓' : i + 1}
          </span>
          <span className={styles.label}>{step.label}</span>
          <span className={styles.srOnly}>— {STATE_LABEL[step.state]}</span>
        </li>
      ))}
    </ol>
  );
}
