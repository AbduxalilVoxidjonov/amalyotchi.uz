/*
 * `shared` paketida vite/client tiplari yo'q — CSS Modules import'lari uchun deklaratsiya.
 * dashboard/twa tsc bu faylni ko'rmaydi (ularda vite/client bor), shuning uchun to'qnashuv yo'q.
 */
declare module '*.module.css' {
  const classes: { readonly [key: string]: string };
  export default classes;
}
