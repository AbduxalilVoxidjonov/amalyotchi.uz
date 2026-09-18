# shared/ui — dizayn tizimi

Manba: `design/SPEC-TOKENS.md` (19 komponent). Vizual ko'rgazma: `npm run dev` → **`/dev/kit`** (faqat DEV).

## Yondashuv (tanlangan)

- **CSS Modules** (`X.module.css`), faqat `var(--...)` tokenlari (`@amaliyotchi/shared/styles/tokens.css`).
  Xom hex/px komponent CSS'ida **yo'q** (istisno: `1px`/`0` kabi struktura qiymatlari va sizing).
- **Variant/holat — `data-*` atributlar** (`data-variant`, `data-size`, `data-status`, `data-density`, `aria-pressed`,
  `aria-current`). Testlar shu atributlarga tayanadi (vitest'da CSS module class'lari hash'lanadi).
- Har komponent — alohida papka: `x/X.tsx` + `x/X.module.css` + `x/index.ts`; `index.ts` dan re-export.
- Komponentlar "dumb": faqat props, serverga murojaat yo'q. `forwardRef` — form control va tugmalarda.
- **Ikki qatlam**:
  - `@amaliyotchi/shared/ui` (`web/shared/src/ui/`) — ikkala paketda kerak bo'ladigan bazaviy: `Button`, `Badge`
    (+`STATUS`), `Card`(+`CardHeader/Body/Row/Footer`), `Input`/`Textarea`/`Select`/`Checkbox` (+`Field`),
    `ProgressBar` (+`pctColor`), `Avatar` (+`initialsOf`), `Eyebrow`, `cn`.
  - `@/shared/ui` (shu papka) — yuqoridagilarni re-export qiladi **+** dashboard'ga xos: `Pill`/`PillGroup`,
    `DataTable`/`PersonCell`, `StatTile`/`StatGrid`, `FactGrid`, `SidebarNav`, `Topbar`(=`PageHeader`),
    `Alert`/`AlertList`/`AlertRow`, `Chip`/`ChipRow`/`FileBox`, `MapPlaceholder`, `MapPicker`, `EmptyState`,
    `Modal`/`ConfirmDialog`, `Breadcrumb`.
- Import har doim `import { Button, DataTable } from '@/shared/ui'` (dashboard) yoki
  `from '@amaliyotchi/shared/ui'` (twa). Tree-shake ishlaydi (`sideEffects: false`).

## Tez ma'lumot

| Komponent                   | Asosiy props                                                                                                                                                                                           |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Button`                    | `variant` primary·secondary(default)·danger·dashed·alert·ghost-dark·score·checkin; `size` xs·sm·md·lg; `block`; `radius` md·md2; `asChild` (`<Button asChild><Link/></Button>`)                        |
| `Pill`                      | `active`, `shape` round·square·tab·wide, `count`; `PillGroup stretch`                                                                                                                                  |
| `Badge`                     | `status` ok·late·bad·neu·info; `size` sm·default·md·lg                                                                                                                                                 |
| `DataTable<T>`              | `columns: {key, header, width:'1.6fr', mono, strong, dim, align, render}`, `rows`, `rowKey`, `density` comfortable·compact, `actions(row)`, `toolbar`, `footer`, `emptyText`, `onRowClick`, `minWidth` |
| `Card`                      | `padded` true·'lg'·'form', `as`; `CardHeader title subtitle actions level`                                                                                                                             |
| `ProgressBar`               | `value` 0–100, `showValue`, `color` (token) — rang qoidasi avtomatik                                                                                                                                   |
| `StatTile` / `StatGrid`     | `label value note noteTone dot`; `StatGrid min={170                                                                                                                                                    | 180}` |
| `FactGrid`                  | `items:[{k,v}]`, `columns` 2·auto, `min`, `variant` mono·detail·portfolio                                                                                                                              |
| `Input`/`Textarea`/`Select` | `label labelEnd hint error mono variant` (login·form·diary·search·readonly) + native props                                                                                                             |
| `SidebarNav`                | `items:[{label,to,badge,end}]` — `NavLink`, `aria-current`                                                                                                                                             |
| `Topbar`                    | `crumb title clock actions onMenuClick`                                                                                                                                                                |
| `Alert`                     | `title` + `AlertList > AlertRow action`                                                                                                                                                                |
| `Avatar`                    | `name` yoki `initials`, `variant` table·card·detail·sidebar                                                                                                                                            |
| `Chip`                      | `variant` file·fmt; `FileBox name meta action`                                                                                                                                                         |
| `MapPlaceholder`            | `title coords note height` — faqat ko'rsatish (haqiqiy xarita emas)                                                                                                                                     |
| `MapPicker`                 | `value onChange radiusM label height disabled error` — Leaflet/OSM, klik yoki markerni sudrash bilan koordinata tanlash; Leaflet qatlami `lazy` chunk, testda `vi.mock('@/shared/ui/map-picker')`                                                                                                                                                                             |
| `Eyebrow`                   | `as spacing tone margin`                                                                                                                                                                               |
| `EmptyState`                | `title description action tone`                                                                                                                                                                        |
| `Modal`                     | `open onClose title description children footer width` — a11y: `role="dialog"`, Esc/overlay yopadi, fokus ichkarida qulflanadi, `body` scroll qulflanadi                                             |
| `ConfirmDialog`             | `Modal` ustida: `open title description confirmLabel cancelLabel danger isLoading error onConfirm onCancel`                                                                                            |
| `Breadcrumb`                | `items:[{label,to?}]` — oxirgi element havola emas, `aria-current="page"`; `nav aria-label="Yo'l"`                                                                                                      |

## Shell bilan ishlash (`src/app/layout`)

```tsx
import { usePageHeader, TopbarActions } from '@/app/layout';
usePageHeader({ title: 'Talabalarim' }); // faqat string — nav label'ini almashtiradi
<TopbarActions>
  <Button size="sm">Eksport</Button>
</TopbarActions>; // Topbar o'ng slotiga portal
```
