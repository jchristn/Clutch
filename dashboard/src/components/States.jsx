import { useTranslation } from 'react-i18next';
import { RefreshIcon } from './Icons';

/** Centered loading block. */
export function LoadingState({ label }) {
  const { t } = useTranslation();
  return (
    <div className="state-block">
      <div className="loading-spinner" />
      <p>{label || t('common.generic.loading')}</p>
    </div>
  );
}

/** Empty state with a title and body. */
export function EmptyState({ title, body, action }) {
  return (
    <div className="state-block">
      <h3>{title}</h3>
      {body && <p>{body}</p>}
      {action}
    </div>
  );
}

/** Dismissible/retryable error banner. */
export function ErrorBanner({ message, onRetry }) {
  const { t } = useTranslation();
  return (
    <div className="error-banner">
      <span>{message || t('common.errors.loadFailed')}</span>
      {onRetry && (
        <button type="button" className="button-secondary button-sm" onClick={onRetry}>
          <RefreshIcon /> {t('common.actions.retry')}
        </button>
      )}
    </div>
  );
}
