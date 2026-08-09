import { useTranslation } from 'react-i18next';
import { LOCALES, canonicalizeLocale } from '../i18n/localeRegistry';

/** Locale <select>, rendering autonyms. Used in login, topbar, and settings. */
export default function LanguageSelector({ compact = false, className = '' }) {
  const { i18n, t } = useTranslation();
  const current = canonicalizeLocale(i18n.resolvedLanguage || i18n.language);

  return (
    <label className={`topbar-lang ${className}`} style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem' }}>
      {!compact && <span>{t('common.language.label')}</span>}
      <select
        aria-label={t('common.language.label')}
        value={current}
        onChange={(e) => i18n.changeLanguage(e.target.value)}
      >
        {LOCALES.map((l) => (
          <option key={l.code} value={l.code}>
            {l.nativeName}
          </option>
        ))}
      </select>
    </label>
  );
}
