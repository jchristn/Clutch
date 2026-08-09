import { useTranslation } from 'react-i18next';
import { AUTO_REFRESH_OPTIONS } from '../utils/constants';

/**
 * Auto-refresh interval selector. `value` is a number of seconds (0 = None).
 * Options: None, 15 (default), 30, 60, 120, 180, 300.
 */
export default function AutoRefreshSelect({ value, onChange }) {
  const { t } = useTranslation();
  return (
    <label className="auto-refresh-select" title={t('components.autoRefresh.label')}>
      <span className="auto-refresh-label">{t('components.autoRefresh.label')}</span>
      <select
        aria-label={t('components.autoRefresh.label')}
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
      >
        {AUTO_REFRESH_OPTIONS.map((s) =>
          s === 0 ? (
            <option key={s} value={s}>
              {t('components.autoRefresh.none')}
            </option>
          ) : (
            <option key={s} value={s}>
              {t('components.autoRefresh.seconds', { count: s })}
            </option>
          )
        )}
      </select>
    </label>
  );
}
