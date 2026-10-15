import { bearingDeg, distanceM, routeArrows, sortByTime } from './route';

const A = { lat: 41.3, lng: 69.28 };

describe('bearingDeg', () => {
  it("shimol 0°, sharq 90°, janub 180°, g'arb 270°", () => {
    expect(bearingDeg(A, { lat: 41.301, lng: 69.28 })).toBeCloseTo(0);
    expect(bearingDeg(A, { lat: 41.3, lng: 69.281 })).toBeCloseTo(90);
    expect(bearingDeg(A, { lat: 41.299, lng: 69.28 })).toBeCloseTo(180);
    expect(bearingDeg(A, { lat: 41.3, lng: 69.279 })).toBeCloseTo(270);
  });
});

describe('distanceM', () => {
  it('0.001° kenglik ≈ 111 m', () => {
    expect(distanceM(A, { lat: 41.301, lng: 69.28 })).toBeCloseTo(111.3, 0);
  });
});

describe('routeArrows', () => {
  it("bitta nuqta yoki bo'sh — strelka yo'q", () => {
    expect(routeArrows([])).toEqual([]);
    expect(routeArrows([A])).toEqual([]);
  });

  it("to'g'ri chiziq bo'ylab teng oraliqda, yo'nalish bo'yicha", () => {
    const pts = [A, { lat: 41.302, lng: 69.28 }, { lat: 41.302, lng: 69.283 }];
    const arrows = routeArrows(pts, { maxArrows: 2 });
    expect(arrows).toHaveLength(2);
    // Birinchisi shimolga ketayotgan bo'lakda, ikkinchisi sharqqa.
    expect(arrows[0]!.bearing).toBeCloseTo(0);
    expect(arrows[1]!.bearing).toBeCloseTo(90);
  });

  it("shovqin (juda qisqa bo'laklar) e'tiborga olinmaydi", () => {
    const jitter = [A, { lat: 41.30001, lng: 69.28 }, { lat: 41.3, lng: 69.28001 }];
    expect(routeArrows(jitter)).toEqual([]);
  });

  it('strelkalar soni chegaralangan', () => {
    const pts = Array.from({ length: 100 }, (_, i) => ({ lat: 41.3 + i * 0.0005, lng: 69.28 }));
    expect(routeArrows(pts, { maxArrows: 10 })).toHaveLength(10);
  });
});

describe('sortByTime', () => {
  it('eskidan yangiga', () => {
    const sorted = sortByTime([
      { occurredAt: '2026-10-14T05:10:00Z' },
      { occurredAt: '2026-10-14T05:00:00Z' },
    ]);
    expect(sorted.map((p) => p.occurredAt)).toEqual([
      '2026-10-14T05:00:00Z',
      '2026-10-14T05:10:00Z',
    ]);
  });
});
