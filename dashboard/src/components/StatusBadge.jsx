import { statusClass, methodClass } from '../i18n/formatters';

/** HTTP status-code pill. */
export function StatusBadge({ code }) {
  return <span className={statusClass(code)}>{code}</span>;
}

/** HTTP method pill. */
export function MethodPill({ method }) {
  return <span className={methodClass(method)}>{method}</span>;
}

/** Generic labeled pill with a semantic tone. */
export function Badge({ tone = 'neutral', children }) {
  return <span className={`pill pill-${tone}`}>{children}</span>;
}

/** Boolean active/inactive pill. */
export function ActiveBadge({ active, activeLabel, inactiveLabel }) {
  return (
    <span className={`pill ${active ? 'pill-success' : 'pill-neutral'}`}>
      {active ? activeLabel : inactiveLabel}
    </span>
  );
}
