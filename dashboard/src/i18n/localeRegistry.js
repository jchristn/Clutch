// Central locale registry for the Clutch dashboard.
// Canonical BCP 47 identifiers with direction + display metadata.

import { STORAGE_KEYS } from '../utils/constants';

export const STORAGE_KEY = STORAGE_KEYS.locale;
export const DEFAULT_LOCALE = 'en';

/**
 * Supported locales:
 *  - en: English (source locale)
 *  - de: German (long-Latin expansion coverage)
 *  - ja: Japanese (CJK coverage)
 *  - zz: RTL / expansion pseudo-locale (bidi + accented expansion testing)
 */
export const LOCALES = [
  { code: 'en', englishName: 'English', nativeName: 'English', dir: 'ltr', fallback: 'en' },
  { code: 'de', englishName: 'German', nativeName: 'Deutsch', dir: 'ltr', fallback: 'en' },
  { code: 'ja', englishName: 'Japanese', nativeName: '日本語', dir: 'ltr', fallback: 'en' },
  { code: 'zz', englishName: 'Pseudo (RTL)', nativeName: '‏Pseudo·RTL‏', dir: 'rtl', fallback: 'en' }
];

export const SUPPORTED_CODES = LOCALES.map((l) => l.code);

// Alias normalization: map region variants and product wording to canonical codes.
const ALIASES = {
  'en-us': 'en',
  'en-gb': 'en',
  'de-de': 'de',
  'de-at': 'de',
  'de-ch': 'de',
  'ja-jp': 'ja',
  kanji: 'ja',
  japanese: 'ja',
  pseudo: 'zz',
  rtl: 'zz'
};

/** Normalize an arbitrary locale string to a supported canonical code. */
export function canonicalizeLocale(input) {
  if (!input) return DEFAULT_LOCALE;
  const lower = String(input).toLowerCase();
  if (SUPPORTED_CODES.includes(lower)) return lower;
  if (ALIASES[lower]) return ALIASES[lower];
  const base = lower.split('-')[0];
  if (SUPPORTED_CODES.includes(base)) return base;
  if (ALIASES[base]) return ALIASES[base];
  return DEFAULT_LOCALE;
}

/** Look up locale metadata (falls back to the default locale entry). */
export function getLocaleMeta(code) {
  const c = canonicalizeLocale(code);
  return LOCALES.find((l) => l.code === c) || LOCALES[0];
}

/** Direction for a locale ('ltr' | 'rtl'). */
export function getDirection(code) {
  return getLocaleMeta(code).dir;
}

/**
 * The BCP 47 tag actually handed to Intl formatters. The pseudo-locale 'zz' is
 * not a real Intl locale, so map it to Arabic ('ar') to exercise RTL numeral and
 * date shaping; every real locale maps to itself.
 */
export function toIntlLocale(code) {
  const c = canonicalizeLocale(code);
  return c === 'zz' ? 'ar' : c;
}
