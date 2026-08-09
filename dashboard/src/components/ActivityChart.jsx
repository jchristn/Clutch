import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { REQUEST_RANGES, DEFAULT_AUTO_REFRESH } from '../utils/constants';
import { formatAxisTime, formatDateTime, formatNumber, formatDuration } from '../i18n/formatters';
import useAutoRefresh from '../hooks/useAutoRefresh';
import RangeTabs from './RangeTabs';
import AutoRefreshSelect from './AutoRefreshSelect';
import ChartTooltip from './ChartTooltip';

const W = 1000;
const H = 276;
const PAD_TOP = 16;
const PAD_RIGHT = 20;
const PAD_BOTTOM = 68;
const PAD_LEFT = 48;
const PLOT_W = W - PAD_LEFT - PAD_RIGHT;
const PLOT_H = H - PAD_TOP - PAD_BOTTOM;

const AXIS_LABELS = { hour: 12, day: 12, week: 7, month: 12 };

function computeTicks(max) {
  const step = Math.max(1, Math.ceil(max / 4));
  return [0, step, step * 2, step * 3, step * 4];
}

function axisLabelIndices(count, rangeId) {
  const maxLabels = AXIS_LABELS[rangeId] || 12;
  if (count <= maxLabels) return new Set(Array.from({ length: count }, (_, i) => i));
  const last = count - 1;
  const set = new Set();
  for (let i = 0; i < maxLabels; i += 1) set.add(Math.round((i * last) / (maxLabels - 1)));
  return set;
}

/**
 * Stacked success/failure bar chart for request history. `summary` is the
 * RequestHistorySummary shape; missing buckets are normalized to zeros.
 */
export default function ActivityChart({
  summary,
  rangeId = 'day',
  onRangeChange,
  onRefresh,
  onBucketClick,
  loading = false,
  title
}) {
  const { t, i18n } = useTranslation();
  const [hover, setHover] = useState(null); // { index, x, y }
  const [autoSecs, setAutoSecs] = useState(DEFAULT_AUTO_REFRESH);
  useAutoRefresh(onRefresh, autoSecs);

  const buckets = useMemo(() => {
    const raw = summary?.buckets || [];
    return raw.map((b) => ({
      bucketStartUtc: b.bucketStartUtc,
      bucketEndUtc: b.bucketEndUtc,
      successCount: Number(b.successCount || 0),
      failureCount: Number(b.failureCount || 0),
      averageDurationMs: Number(b.averageDurationMs || 0)
    }));
  }, [summary]);

  const maxCount = Math.max(1, ...buckets.map((b) => b.successCount + b.failureCount));
  const ticks = computeTicks(maxCount);
  const axisMax = ticks[ticks.length - 1];
  const labelIdx = axisLabelIndices(buckets.length, rangeId);
  const groupW = buckets.length > 0 ? PLOT_W / buckets.length : PLOT_W;
  const barW = Math.max(2, Math.min(28, groupW * 0.72));

  const ranges = Object.values(REQUEST_RANGES);
  const hovered = hover != null ? buckets[hover.index] : null;

  return (
    <div className="chart-card">
      <div className="chart-header">
        <div>
          <div className="chart-title">{title || t('views.home.requestActivity')}</div>
          <div className="chart-legend">
            <span className="chart-legend-item">
              <strong>{formatNumber(summary?.totalCount ?? 0)}</strong> {t('components.chart.total')}
            </span>
            <span className="chart-legend-item" style={{ color: 'var(--color-success)' }}>
              {formatNumber(summary?.totalSuccess ?? 0)} {t('components.chart.success')}
            </span>
            <span className="chart-legend-item" style={{ color: 'var(--color-danger)' }}>
              {formatNumber(summary?.totalFailure ?? 0)} {t('components.chart.failure')}
            </span>
          </div>
        </div>
        <div className="chart-controls">
          <RangeTabs ranges={ranges} value={rangeId} onChange={onRangeChange} onRefresh={onRefresh} loading={loading} />
          <AutoRefreshSelect value={autoSecs} onChange={setAutoSecs} />
        </div>
      </div>

      {buckets.length === 0 ? (
        <div className="chart-empty">{t('components.chart.noData')}</div>
      ) : (
        <div className="chart-canvas">
          <svg viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="xMidYMid meet" role="img">
            {/* Y grid + labels */}
            {ticks.map((tick) => {
              const y = PAD_TOP + PLOT_H - (tick / axisMax) * PLOT_H;
              return (
                <g key={tick}>
                  <line
                    className="chart-grid-line"
                    x1={PAD_LEFT}
                    y1={y}
                    x2={W - PAD_RIGHT}
                    y2={y}
                    strokeDasharray={tick === 0 ? '0' : '4 4'}
                  />
                  <text className="chart-svg-text" x={PAD_LEFT - 8} y={y + 4} textAnchor="end">
                    {formatNumber(tick)}
                  </text>
                </g>
              );
            })}

            {/* Bars */}
            {buckets.map((b, i) => {
              const total = b.successCount + b.failureCount;
              const failH = (b.failureCount / axisMax) * PLOT_H;
              const succH = (b.successCount / axisMax) * PLOT_H;
              const x = PAD_LEFT + i * groupW + (groupW - barW) / 2;
              const failY = PAD_TOP + PLOT_H - failH;
              const succY = PAD_TOP + PLOT_H - failH - succH;
              const active = hover?.index === i;
              return (
                <g
                  key={i}
                  onMouseMove={(e) => setHover({ index: i, x: e.clientX, y: e.clientY })}
                  onMouseLeave={() => setHover(null)}
                  onClick={() => onBucketClick?.(b)}
                  style={{ cursor: onBucketClick ? 'pointer' : 'default' }}
                >
                  <rect
                    x={PAD_LEFT + i * groupW}
                    y={PAD_TOP}
                    width={groupW}
                    height={PLOT_H + PAD_BOTTOM}
                    fill="transparent"
                  />
                  {b.failureCount > 0 && (
                    <rect x={x} y={failY} width={barW} height={failH} rx={2} fill="var(--color-danger)" opacity={active ? 1 : 0.9} />
                  )}
                  {b.successCount > 0 && (
                    <rect x={x} y={succY} width={barW} height={succH} rx={2} fill="var(--color-success)" opacity={active ? 1 : 0.88} />
                  )}
                  {total === 0 && <rect x={x} y={PAD_TOP + PLOT_H - 1} width={barW} height={1} fill="var(--color-border)" />}
                </g>
              );
            })}

            {/* X labels */}
            {buckets.map((b, i) =>
              labelIdx.has(i) ? (
                <text
                  key={`x${i}`}
                  className="chart-svg-text"
                  x={PAD_LEFT + i * groupW + groupW / 2}
                  y={H - 44}
                  textAnchor="middle"
                >
                  {formatAxisTime(b.bucketStartUtc, i18n.language)}
                </text>
              ) : null
            )}
          </svg>

          {hovered && (
            <ChartTooltip
              pos={{ x: hover.x, y: hover.y }}
              content={
                <>
                  <div className="chart-tooltip-title">
                    {formatDateTime(hovered.bucketStartUtc, i18n.language)}
                  </div>
                  <div className="chart-tooltip-row">
                    <span>{t('components.chart.total')}</span>
                    <span>{formatNumber(hovered.successCount + hovered.failureCount)}</span>
                  </div>
                  <div className="chart-tooltip-row" style={{ color: 'var(--color-success)' }}>
                    <span>{t('components.chart.success')}</span>
                    <span>{formatNumber(hovered.successCount)}</span>
                  </div>
                  <div className="chart-tooltip-row" style={{ color: 'var(--color-danger)' }}>
                    <span>{t('components.chart.failure')}</span>
                    <span>{formatNumber(hovered.failureCount)}</span>
                  </div>
                  <div className="chart-tooltip-row">
                    <span>{t('components.chart.avgDuration')}</span>
                    <span>{formatDuration(hovered.averageDurationMs, i18n.language)}</span>
                  </div>
                </>
              }
            />
          )}
        </div>
      )}
    </div>
  );
}
