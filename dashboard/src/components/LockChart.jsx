import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { LOCK_RANGES, DEFAULT_AUTO_REFRESH } from '../utils/constants';
import { formatAxisTime, formatDateTime, formatNumber } from '../i18n/formatters';
import useAutoRefresh from '../hooks/useAutoRefresh';
import RangeTabs from './RangeTabs';
import AutoRefreshSelect from './AutoRefreshSelect';
import ChartTooltip from './ChartTooltip';

const W = 1000;
const H = 300;
const PAD_TOP = 16;
const PAD_RIGHT = 20;
const PAD_BOTTOM = 68;
const PAD_LEFT = 48;
const PLOT_W = W - PAD_LEFT - PAD_RIGHT;
const PLOT_H = H - PAD_TOP - PAD_BOTTOM;

// Categorical series palette (readable in both themes).
const SERIES_COLORS = [
  '#6366f1', '#10b981', '#f59e0b', '#ef4444', '#3b82f6',
  '#a855f7', '#14b8a6', '#ec4899', '#84cc16', '#f97316'
];

const AXIS_LABELS = { hour: 12, day: 12, week: 7 };

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
 * Domain lock-activity chart. Consumes the /lock-audit/summary shape:
 * { bucketStartsUtc:[...], series:[{ lockKey, mode, label, counts:[...] }] }.
 * Toggles between a line chart (one line per series) and a stacked bar chart.
 */
export default function LockChart({
  summary,
  rangeId = 'day',
  onRangeChange,
  onRefresh,
  loading = false,
  title
}) {
  const { t, i18n } = useTranslation();
  const [mode, setMode] = useState('line'); // 'line' | 'bar'
  const [hover, setHover] = useState(null); // { index, x, y }
  const [autoSecs, setAutoSecs] = useState(DEFAULT_AUTO_REFRESH);
  useAutoRefresh(onRefresh, autoSecs);

  const starts = summary?.bucketStartsUtc || [];
  const series = useMemo(() => summary?.series || [], [summary]);
  const n = starts.length;

  const { axisMax, ticks } = useMemo(() => {
    let max = 1;
    if (mode === 'bar') {
      for (let i = 0; i < n; i += 1) {
        let sum = 0;
        for (const s of series) sum += Number(s.counts?.[i] || 0);
        if (sum > max) max = sum;
      }
    } else {
      for (const s of series) for (const c of s.counts || []) if (Number(c) > max) max = Number(c);
    }
    const tk = computeTicks(max);
    return { axisMax: tk[tk.length - 1], ticks: tk };
  }, [series, n, mode]);

  const labelIdx = axisLabelIndices(n, rangeId);
  const groupW = n > 0 ? PLOT_W / n : PLOT_W;
  const barW = Math.max(2, Math.min(26, groupW * 0.7));
  const ranges = Object.values(LOCK_RANGES);
  const yFor = (v) => PAD_TOP + PLOT_H - (v / axisMax) * PLOT_H;

  const hasData = n > 0 && series.length > 0;

  return (
    <div className="chart-card">
      <div className="chart-header">
        <div className="chart-title">{title || t('views.home.lockActivity')}</div>
        <div className="chart-controls">
          <div className="range-group" role="tablist" aria-label="chart mode">
            <button
              type="button"
              className={`range-btn ${mode === 'line' ? 'active' : ''}`}
              onClick={() => setMode('line')}
            >
              {t('components.chart.modes.line')}
            </button>
            <button
              type="button"
              className={`range-btn ${mode === 'bar' ? 'active' : ''}`}
              onClick={() => setMode('bar')}
            >
              {t('components.chart.modes.bar')}
            </button>
          </div>
          <RangeTabs ranges={ranges} value={rangeId} onChange={onRangeChange} onRefresh={onRefresh} loading={loading} />
          <AutoRefreshSelect value={autoSecs} onChange={setAutoSecs} />
        </div>
      </div>

      {!hasData ? (
        <div className="chart-empty">{t('components.chart.noData')}</div>
      ) : (
        <>
          <div className="chart-canvas">
            <svg viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="xMidYMid meet" role="img">
              {ticks.map((tick) => {
                const y = yFor(tick);
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

              {mode === 'bar'
                ? starts.map((_, i) => {
                    let acc = 0;
                    const x = PAD_LEFT + i * groupW + (groupW - barW) / 2;
                    return (
                      <g key={i}>
                        {series.map((s, si) => {
                          const v = Number(s.counts?.[i] || 0);
                          if (v === 0) return null;
                          const h = (v / axisMax) * PLOT_H;
                          const y = PAD_TOP + PLOT_H - acc - h;
                          acc += h;
                          return (
                            <rect
                              key={si}
                              x={x}
                              y={y}
                              width={barW}
                              height={h}
                              fill={SERIES_COLORS[si % SERIES_COLORS.length]}
                              opacity={hover?.index === i ? 1 : 0.85}
                            />
                          );
                        })}
                      </g>
                    );
                  })
                : series.map((s, si) => {
                    const pts = (s.counts || [])
                      .map((c, i) => `${PAD_LEFT + i * groupW + groupW / 2},${yFor(Number(c || 0))}`)
                      .join(' ');
                    return (
                      <polyline
                        key={si}
                        points={pts}
                        fill="none"
                        stroke={SERIES_COLORS[si % SERIES_COLORS.length]}
                        strokeWidth={2}
                        strokeLinejoin="round"
                        strokeLinecap="round"
                      />
                    );
                  })}

              {/* Hover hit areas per bucket */}
              {starts.map((_, i) => (
                <rect
                  key={`h${i}`}
                  x={PAD_LEFT + i * groupW}
                  y={PAD_TOP}
                  width={groupW}
                  height={PLOT_H}
                  fill="transparent"
                  onMouseMove={(e) => setHover({ index: i, x: e.clientX, y: e.clientY })}
                  onMouseLeave={() => setHover(null)}
                />
              ))}

              {/* X labels */}
              {starts.map((st, i) =>
                labelIdx.has(i) ? (
                  <text
                    key={`x${i}`}
                    className="chart-svg-text"
                    x={PAD_LEFT + i * groupW + groupW / 2}
                    y={H - 44}
                    textAnchor="middle"
                  >
                    {formatAxisTime(st, i18n.language)}
                  </text>
                ) : null
              )}
            </svg>

            {hover != null && (
              <ChartTooltip
                pos={{ x: hover.x, y: hover.y }}
                content={
                  <>
                    <div className="chart-tooltip-title">
                      {formatDateTime(starts[hover.index], i18n.language)}
                    </div>
                    {series.map((s, si) => (
                      <div key={si} className="chart-tooltip-row">
                        <span style={{ color: SERIES_COLORS[si % SERIES_COLORS.length] }}>
                          {s.label || s.lockKey}
                        </span>
                        <span>{formatNumber(Number(s.counts?.[hover.index] || 0))}</span>
                      </div>
                    ))}
                  </>
                }
              />
            )}
          </div>

          <div className="chart-legend">
            {series.map((s, si) => (
              <span key={si} className="chart-legend-item">
                <span
                  className="chart-legend-swatch"
                  style={{ background: SERIES_COLORS[si % SERIES_COLORS.length] }}
                />
                {s.label || s.lockKey}
              </span>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
