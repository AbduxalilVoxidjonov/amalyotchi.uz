import {
  minutesTextToSeconds,
  secondsToMinutesText,
  TRACKING_ERRORS,
  validateTracking,
} from './tracking';

const KEYS = new Set(['trackingEnabled', 'trackingIntervalSec', 'trackingDistanceFilterM']);

describe('validateTracking', () => {
  it('oraliq ichida — xato yo‘q', () => {
    expect(
      validateTracking({ trackingIntervalSec: '60', trackingDistanceFilterM: '500' }, KEYS),
    ).toEqual({});
    expect(
      validateTracking({ trackingIntervalSec: '900', trackingDistanceFilterM: '10' }, KEYS),
    ).toEqual({});
  });

  it('chegaradan tashqari, bo‘sh yoki kasr — xato', () => {
    expect(
      validateTracking({ trackingIntervalSec: '59', trackingDistanceFilterM: '501' }, KEYS),
    ).toEqual({
      trackingIntervalSec: [TRACKING_ERRORS.trackingIntervalSec],
      trackingDistanceFilterM: [TRACKING_ERRORS.trackingDistanceFilterM],
    });
    expect(
      Object.keys(
        validateTracking({ trackingIntervalSec: '', trackingDistanceFilterM: '9.5' }, KEYS),
      ),
    ).toEqual(['trackingIntervalSec', 'trackingDistanceFilterM']);
  });

  it('kalit serverda yo‘q bo‘lsa tekshirilmaydi', () => {
    expect(validateTracking({}, new Set(['geofenceRadius']))).toEqual({});
  });
});

describe('daqiqa ↔ soniya', () => {
  it('konvertatsiya', () => {
    expect(secondsToMinutesText('300')).toBe('5');
    expect(secondsToMinutesText('90')).toBe('1.5');
    expect(minutesTextToSeconds('5')).toBe('300');
    expect(minutesTextToSeconds('1,5')).toBe('90');
    expect(minutesTextToSeconds('')).toBe('');
    expect(minutesTextToSeconds('abc')).toBe('abc');
  });
});
