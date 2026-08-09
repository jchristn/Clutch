// Locale-aware formatting helpers. Every helper receives an explicit locale
// (falling back to the active i18next language) and routes through Intl.

import i18n from './index.js';
import { toIntlLocale } from './localeRegistry.js';

function resolveLocale(locale) {
  const raw = locale || i18n.resolvedLanguage || i18n.language || 'en';
  return toIntlLocale(raw);
}

function t(key, options) {
  return i18n.t(key, options);
}

/** Format a number. */
export function formatNumber(value, options = {}, locale) {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return new Intl.NumberFormat(resolveLocale(locale), options).format(value);
}

/** Format a percentage from a 0..1 ratio (or a raw number with `alreadyPercent`). */
export function formatPercent(value, locale, { alreadyPercent = false, digits = 1 } = {}) {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  const ratio = alreadyPercent ? value / 100 : value;
  return new Intl.NumberFormat(resolveLocale(locale), {
    style: 'percent',
    minimumFractionDigits: 0,
    maximumFractionDigits: digits
  }).format(ratio);
}

/** Format a date (medium). */
export function formatDate(utcString, locale, options = { dateStyle: 'medium' }) {
  const d = toDate(utcString);
  if (!d) return '—';
  return new Intl.DateTimeFormat(resolveLocale(locale), options).format(d);
}

/** Format a time (short). */
export function formatTime(utcString, locale, options = { timeStyle: 'short' }) {
  const d = toDate(utcString);
  if (!d) return '—';
  return new Intl.DateTimeFormat(resolveLocale(locale), options).format(d);
}

/** Format a combined date + time (medium date, short time). */
export function formatDateTime(utcString, locale, options = { dateStyle: 'medium', timeStyle: 'short' }) {
  const d = toDate(utcString);
  if (!d) return '—';
  return new Intl.DateTimeFormat(resolveLocale(locale), options).format(d);
}

/** Format a compact time axis label (HH:MM style, locale aware). */
export function formatAxisTime(utcString, locale) {
  const d = toDate(utcString);
  if (!d) return '';
  return new Intl.DateTimeFormat(resolveLocale(locale), { hour: '2-digit', minute: '2-digit' }).format(d);
}

/** Format relative time such as "5 minutes ago". */
export function formatRelativeTime(utcString, locale) {
  const d = toDate(utcString);
  if (!d) return '—';
  const rtf = new Intl.RelativeTimeFormat(resolveLocale(locale), { numeric: 'auto' });
  const diffMs = d.getTime() - Date.now();
  const abs = Math.abs(diffMs);
  const sec = diffMs / 1000;
  if (abs < 5000) return t('common.relativeTime.now');
  if (abs < 60_000) return rtf.format(Math.round(sec), 'second');
  if (abs < 3_600_000) return rtf.format(Math.round(sec / 60), 'minute');
  if (abs < 86_400_000) return rtf.format(Math.round(sec / 3600), 'hour');
  return rtf.format(Math.round(sec / 86_400), 'day');
}

/** Format a duration in milliseconds with a short unit label. */
export function formatDuration(ms, locale) {
  if (ms === null || ms === undefined || Number.isNaN(ms)) return '—';
  const nf = new Intl.NumberFormat(resolveLocale(locale), { maximumFractionDigits: ms < 10 ? 1 : 0 });
  if (ms < 1000) return `${nf.format(ms)} ${t('common.units.msShort')}`;
  const s = ms / 1000;
  if (s < 60) {
    return `${new Intl.NumberFormat(resolveLocale(locale), { maximumFractionDigits: 2 }).format(s)} ${t('common.units.sShort')}`;
  }
  const m = s / 60;
  return `${new Intl.NumberFormat(resolveLocale(locale), { maximumFractionDigits: 1 }).format(m)} ${t('common.units.mShort')}`;
}

/** Format a byte count. */
export function formatBytes(value, locale) {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let v = value;
  let i = 0;
  while (v >= 1024 && i < units.length - 1) {
    v /= 1024;
    i += 1;
  }
  const nf = new Intl.NumberFormat(resolveLocale(locale), { maximumFractionDigits: i === 0 ? 0 : 1 });
  return `${nf.format(v)} ${units[i]}`;
}

/** Join a list with locale-aware conjunction. */
export function formatList(values, locale, options = { style: 'long', type: 'conjunction' }) {
  const arr = (values || []).filter((x) => x !== null && x !== undefined && x !== '');
  if (arr.length === 0) return '';
  return new Intl.ListFormat(resolveLocale(locale), options).format(arr.map(String));
}

/** Localized yes/no. */
export function formatBoolean(value) {
  return value ? t('common.boolean.yes') : t('common.boolean.no');
}

/** Truncate a long string, appending an ellipsis. */
export function truncate(value, len = 60) {
  if (value === null || value === undefined) return '';
  const s = String(value);
  return s.length > len ? `${s.slice(0, len)}…` : s;
}

/** CSS class for an HTTP status code pill. */
export function statusClass(code) {
  const n = Number(code);
  if (n >= 200 && n < 400) return 'pill pill-success';
  if (n >= 400) return 'pill pill-danger';
  return 'pill pill-neutral';
}

/** CSS class for an HTTP method pill. */
export function methodClass(method) {
  return `method-pill method-${String(method || '').toLowerCase()}`;
}

function toDate(value) {
  if (!value) return null;
  const d = value instanceof Date ? value : new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}
