// Application-wide constants for the Clutch dashboard.

export const STORAGE_KEYS = {
  serverUrl: 'clutch_server_url',
  token: 'clutch_token',
  theme: 'clutch_theme',
  locale: 'clutch.locale',
  pageSize: 'clutch_page_size',
  apiExplorerHistory: 'clutch_api_explorer_history'
};

export const GITHUB_URL = 'https://github.com/jchristn/Clutch';

// Dev defaults pre-filled into the login form for local development.
export const DEV_DEFAULTS = {
  serverUrl: 'http://localhost:8080',
  accessKey: 'clutch-default-access-key',
  secretKey: 'clutch-default-secret-key'
};

export const PAGE_SIZE_OPTIONS = [10, 25, 50, 100, 250];
export const DEFAULT_PAGE_SIZE = 25;

// Auto-refresh interval options, in seconds. 0 = None (disabled).
export const AUTO_REFRESH_OPTIONS = [0, 15, 30, 60, 120, 180, 300];
export const DEFAULT_AUTO_REFRESH = 15;

// Lock modes (match backend LockModeEnum string values).
export const LOCK_MODES = ['Read', 'Write', 'Delete'];

// Lock audit event types (match backend LockEventTypeEnum string values).
export const LOCK_EVENT_TYPES = [
  'PolicyCreated',
  'Acquired',
  'Released',
  'Waited',
  'Denied',
  'Expired',
  'Revoked',
  'HeartbeatRenewed'
];

export const HTTP_METHODS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD'];

/**
 * Request-history chart range presets. Bucket counts and minutes match the
 * backend's supported intervals exactly.
 */
export const REQUEST_RANGES = {
  hour: { id: 'hour', bucketMinutes: 1, bucketCount: 60, windowMs: 60 * 60 * 1000 },
  day: { id: 'day', bucketMinutes: 15, bucketCount: 96, windowMs: 24 * 60 * 60 * 1000 },
  week: { id: 'week', bucketMinutes: 120, bucketCount: 84, windowMs: 7 * 24 * 60 * 60 * 1000 },
  month: { id: 'month', bucketMinutes: 360, bucketCount: 120, windowMs: 30 * 24 * 60 * 60 * 1000 }
};

/**
 * Lock-activity chart range presets with EXACT bucket counts required by the spec:
 * Last Hour = 60 (1/min), Last Day = 96 (4/hour), Last Week = 84 (12/day).
 */
export const LOCK_RANGES = {
  hour: { id: 'hour', bucketCount: 60, windowMs: 60 * 60 * 1000 },
  day: { id: 'day', bucketCount: 96, windowMs: 24 * 60 * 60 * 1000 },
  week: { id: 'week', bucketCount: 84, windowMs: 7 * 24 * 60 * 60 * 1000 }
};

/** Compute { fromUtc, toUtc, bucketMinutes } for a request-history range preset. */
export function requestRangeToParams(rangeId) {
  const range = REQUEST_RANGES[rangeId] || REQUEST_RANGES.day;
  const to = new Date();
  const from = new Date(to.getTime() - range.windowMs);
  return {
    fromUtc: from.toISOString(),
    toUtc: to.toISOString(),
    bucketMinutes: range.bucketMinutes
  };
}

/** Compute { fromUtc, toUtc, bucketCount } for a lock-activity range preset. */
export function lockRangeToParams(rangeId) {
  const range = LOCK_RANGES[rangeId] || LOCK_RANGES.day;
  const to = new Date();
  const from = new Date(to.getTime() - range.windowMs);
  return {
    fromUtc: from.toISOString(),
    toUtc: to.toISOString(),
    bucketCount: range.bucketCount
  };
}
