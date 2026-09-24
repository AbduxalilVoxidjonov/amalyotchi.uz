import {
  forwardRef,
  type ButtonHTMLAttributes,
  type CSSProperties,
  type Ref,
  type MouseEvent,
  type ReactNode,
} from 'react';
import {
  Link,
  NavLink,
  useMatch,
  useNavigate,
  useResolvedPath,
  type LinkProps,
  type NavLinkProps,
  type To,
} from 'react-router-dom';
import { authMode } from '@/shared/auth/mode';
import styles from './AppLink.module.css';

/**
 * Ilova ICHIDAGI marshrutlar uchun havola.
 *
 * Nega: Telegram-Android (12.10.x) Mini App ichida `<a href="/…">` bosilganda — React Router `preventDefault`
 * qilsa ham — Telegram buni tashqi havola deb ushlaydi ("Oops! Failed to load" + "Open in…" varag'i).
 * Shuning uchun Telegram rejimida `href`siz `<button role="link">` chiziladi, navigatsiya `useNavigate()`
 * orqali. Web rejimida — oddiy `Link`/`NavLink` (o'zgarishsiz).
 *
 * Tashqi havolalar (`tel:`, `https://`, PDF) bu yerga TEGISHLI EMAS — ular `<a>`/`openLink` da qoladi.
 */

type ButtonRest = Omit<
  ButtonHTMLAttributes<HTMLButtonElement>,
  'onClick' | 'className' | 'style' | 'children' | 'type'
>;

interface TelegramLinkButtonProps extends ButtonRest {
  to: To;
  replace?: boolean | undefined;
  state?: unknown;
  className?: string | undefined;
  style?: CSSProperties | undefined;
  children?: ReactNode;
  onClick?: ((e: MouseEvent<HTMLButtonElement>) => void) | undefined;
}

const TelegramLinkButton = forwardRef<HTMLButtonElement, TelegramLinkButtonProps>(
  function TelegramLinkButton({ to, replace, state, className, onClick, ...rest }, ref) {
    const navigate = useNavigate();
    const go = () => void navigate(to, replace ? { replace, state } : { state });
    return (
      <button
        ref={ref}
        type="button"
        role="link"
        className={className ? `${styles.link} ${className}` : styles.link}
        // `<button>` Enter/Space'ni o'zi click'ga aylantiradi — klaviatura qo'shimcha ishlovsiz ishlaydi.
        onClick={(e) => {
          onClick?.(e);
          if (!e.defaultPrevented) go();
        }}
        {...rest}
      />
    );
  },
);

/** Telegram rejimida `href`siz tugma chizilsinmi (har renderda — rejim sessiya davomida o'zgarmaydi). */
function useButtonLinks(): boolean {
  return authMode() === 'telegram';
}

export type AppLinkProps = Omit<LinkProps, 'onClick'> & {
  onClick?: (e: MouseEvent<HTMLElement>) => void;
};

export const AppLink = forwardRef<HTMLElement, AppLinkProps>(function AppLink(props, ref) {
  const asButton = useButtonLinks();
  if (!asButton) return <Link ref={ref as Ref<HTMLAnchorElement>} {...props} />;
  const {
    to,
    replace,
    state,
    className,
    style,
    children,
    onClick,
    // `<a>`/Link'ga xos prop'lar — tugmaga uzatilmaydi.
    reloadDocument: _r,
    preventScrollReset: _p,
    relative: _rel,
    viewTransition: _v,
    discover: _d,
    prefetch: _pf,
    target: _t,
    download: _dl,
    hrefLang: _hl,
    media: _m,
    ping: _pg,
    referrerPolicy: _rp,
    ...rest
  } = props;
  return (
    <TelegramLinkButton
      ref={ref as Ref<HTMLButtonElement>}
      to={to}
      replace={replace}
      state={state}
      className={className}
      style={style}
      onClick={onClick}
      {...(rest as ButtonRest)}
    >
      {children}
    </TelegramLinkButton>
  );
});

export type AppNavLinkProps = Omit<NavLinkProps, 'onClick'> & {
  onClick?: (e: MouseEvent<HTMLElement>) => void;
};

/**
 * `NavLink` o'rnini bosuvchi: Telegram rejimida faol holat `useResolvedPath` + `useMatch` orqali,
 * `aria-current="page"` va `className`/`style`/`children` render-prop'lari NavLink kabi.
 */
export const AppNavLink = forwardRef<HTMLElement, AppNavLinkProps>(function AppNavLink(props, ref) {
  const { to, end = false, caseSensitive = false, className, style, children, ...rest } = props;
  const asButton = useButtonLinks();
  const resolved = useResolvedPath(to);
  const match = useMatch({ path: resolved.pathname, end, caseSensitive });

  if (!asButton) return <NavLink ref={ref as Ref<HTMLAnchorElement>} {...props} />;

  const renderProps = { isActive: match != null, isPending: false, isTransitioning: false };
  const cls = typeof className === 'function' ? className(renderProps) : className;
  const st = typeof style === 'function' ? style(renderProps) : style;
  const kids = typeof children === 'function' ? children(renderProps) : children;
  const { 'aria-current': ariaCurrent, ...linkRest } = rest;

  return (
    <AppLink
      ref={ref}
      to={to}
      className={cls}
      style={st}
      aria-current={renderProps.isActive ? (ariaCurrent ?? 'page') : undefined}
      {...linkRest}
    >
      {kids}
    </AppLink>
  );
});
