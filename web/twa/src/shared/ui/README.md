# twa/src/shared/ui

`@amaliyotchi/shared/ui` (Button, Badge, Card, Field oilasi, ProgressBar, Avatar, Eyebrow) re-export
qilinadi + TWA'ga kerak bo'lgan, hozircha `shared` da yo'q komponentlar **lokal** turadi:

| Komponent                                  | Manba (SPEC-TOKENS) | Izoh                                                                                        |
| ------------------------------------------ | ------------------- | ------------------------------------------------------------------------------------------- |
| `FactGrid`                                 | 4.7 FactGrid        | 1px-gap fakt katakchalari (checkinFacts, portfolioStats). Dashboard'dagi bilan bir xil API. |
| `MapPlaceholder`                           | 4.15                | Xarita o'rnidagi dashed blok — xarita kutubxonasi qo'shilmaydi (bundle).                    |
| `Chip`, `ChipRow`, `FileBox`               | 4.14                | Fayl chip'lari va shartnoma fayl bloki.                                                     |
| `EmptyState`, `ErrorState`, `LoadingState` | 4.18 ❓             | Dizaynda yo'q — map-ph uslubida. Har sahifa loading/error/empty shu orqali.                 |

Barchasi CSS Modules + faqat `var(--...)` tokenlar. Dashboard'da ham xuddi shu nomdagi
komponentlar bor (`dashboard/src/shared/ui`) — **keyin `shared/ui` ga ko'chirish mumkin**
(ikkala paketda API bir xil qilib yozilgan).
