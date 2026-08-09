import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import CopyButton from '../components/CopyButton';
import CodeBlock from '../components/CodeBlock';
import ConfirmModal from '../components/ConfirmModal';
import { MethodPill, StatusBadge } from '../components/StatusBadge';
import { LoadingState, EmptyState } from '../components/States';
import useApiExplorer from '../hooks/useApiExplorer';
import { isDestructive, buildCodeSnippets } from '../utils/openApi';
import { formatDuration, formatBytes, truncate } from '../i18n/formatters';

function ParamInputs({ params, location, values, onChange, requiredLabel }) {
  const list = params.filter((p) => p.in === location);
  if (list.length === 0) return null;
  return (
    <div className="modal-form">
      {list.map((p) => (
        <div className="field" key={p.name}>
          <label>
            {p.name}
            {p.required ? ` (${requiredLabel})` : ''}
          </label>
          <input
            value={values[p.name] ?? ''}
            placeholder={p.schema?.type || 'string'}
            onChange={(e) => onChange({ ...values, [p.name]: e.target.value })}
          />
        </div>
      ))}
    </div>
  );
}

export default function ApiExplorerView({ apiClient }) {
  const { t, i18n } = useTranslation();
  const ex = useApiExplorer(apiClient);
  const [search, setSearch] = useState('');
  const [snippetTab, setSnippetTab] = useState('curl');
  const [confirmOpen, setConfirmOpen] = useState(false);

  const filteredGroups = useMemo(() => {
    const q = search.trim().toLowerCase();
    const out = {};
    for (const [tag, ops] of Object.entries(ex.grouped)) {
      const matched = q
        ? ops.filter((o) => `${o.method} ${o.path} ${o.summary} ${o.id}`.toLowerCase().includes(q))
        : ops;
      if (matched.length) out[tag] = matched;
    }
    return out;
  }, [ex.grouped, search]);

  const snippets = useMemo(() => {
    if (!ex.operation) return { curl: '', fetch: '', csharp: '' };
    const hasBody = !['GET', 'HEAD'].includes(ex.operation.method) && ex.body;
    return buildCodeSnippets({
      method: ex.operation.method,
      url: ex.resolvedUrl,
      headers: ex.headers,
      body: hasBody ? ex.body : ''
    });
  }, [ex.operation, ex.resolvedUrl, ex.headers, ex.body]);

  const requestExecution = () => {
    if (ex.operation && isDestructive(ex.operation)) setConfirmOpen(true);
    else ex.execute();
  };

  const bodyAllowed = ex.operation && !['GET', 'HEAD'].includes(ex.operation.method);

  return (
    <div>
      <PageHeader title={t('views.explorer.title')} subtitle={t('views.explorer.subtitle')} />

      {ex.loadingSpec ? (
        <LoadingState />
      ) : ex.specError ? (
        <div className="panel">
          <EmptyState title={t('views.explorer.noSpecTitle')} body={t('views.explorer.noSpecBody')} />
        </div>
      ) : (
        <div className="explorer-layout">
          {/* Operation list */}
          <div className="explorer-list">
            <div className="explorer-search">
              <input placeholder={t('views.explorer.searchOperations')} value={search} onChange={(e) => setSearch(e.target.value)} />
            </div>
            {Object.entries(filteredGroups).map(([tag, ops]) => (
              <div key={tag}>
                <div className="explorer-tag-label">{tag}</div>
                {ops.map((op) => (
                  <div
                    key={op.id}
                    className={`explorer-op ${ex.operationId === op.id ? 'active' : ''}`}
                    onClick={() => ex.setOperationId(op.id)}
                    title={op.summary}
                  >
                    <MethodPill method={op.method} />
                    <code>{op.path}</code>
                  </div>
                ))}
              </div>
            ))}
          </div>

          {/* Request builder + response */}
          <div className="explorer-detail">
            {!ex.operation ? (
              <div className="explorer-panel">
                <EmptyState title={t('views.explorer.title')} body={t('views.explorer.selectOperation')} />
              </div>
            ) : (
              <>
                <div className="explorer-panel">
                  <div className="flex items-center gap-sm" style={{ marginBottom: 'var(--spacing-sm)' }}>
                    <MethodPill method={ex.operation.method} />
                    <code className="monospace">{ex.operation.path}</code>
                  </div>
                  {ex.operation.summary && <p className="text-muted" style={{ fontSize: 'var(--font-size-sm)' }}>{ex.operation.summary}</p>}

                  <h3 style={{ marginTop: 'var(--spacing-md)' }}>{t('views.explorer.resolvedUrl')}</h3>
                  <div className="explorer-url">
                    <span style={{ flex: 1 }}>{ex.resolvedUrl}</span>
                    <CopyButton value={ex.resolvedUrl} title={t('views.explorer.copyUrl')} />
                  </div>

                  {ex.operation.parameters.some((p) => p.in === 'path') && (
                    <>
                      <h3 style={{ marginTop: 'var(--spacing-md)' }}>{t('views.explorer.pathParams')}</h3>
                      <ParamInputs params={ex.operation.parameters} location="path" values={ex.pathParams} onChange={ex.setPathParams} requiredLabel={t('views.explorer.required')} />
                    </>
                  )}
                  {ex.operation.parameters.some((p) => p.in === 'query') && (
                    <>
                      <h3 style={{ marginTop: 'var(--spacing-md)' }}>{t('views.explorer.queryParams')}</h3>
                      <ParamInputs params={ex.operation.parameters} location="query" values={ex.queryParams} onChange={ex.setQueryParams} requiredLabel={t('views.explorer.required')} />
                    </>
                  )}

                  <h3 style={{ marginTop: 'var(--spacing-md)' }}>{t('views.explorer.headers')}</h3>
                  <div className="modal-form">
                    {Object.entries(ex.headers).map(([k, v]) => (
                      <div className="explorer-kv-row" key={k}>
                        <input value={k} readOnly />
                        <input value={v} onChange={(e) => ex.setHeaders({ ...ex.headers, [k]: e.target.value })} />
                        <button type="button" className="button-ghost button-sm" onClick={() => {
                          const next = { ...ex.headers };
                          delete next[k];
                          ex.setHeaders(next);
                        }}>×</button>
                      </div>
                    ))}
                    <button
                      type="button"
                      className="button-secondary button-sm"
                      onClick={() => ex.setHeaders({ ...ex.headers, '': '' })}
                    >
                      {t('views.explorer.addHeader')}
                    </button>
                  </div>

                  {bodyAllowed && (
                    <>
                      <h3 style={{ marginTop: 'var(--spacing-md)' }}>{t('views.explorer.body')}</h3>
                      <textarea rows={8} value={ex.body} onChange={(e) => ex.setBody(e.target.value)} />
                    </>
                  )}

                  <div style={{ marginTop: 'var(--spacing-md)' }}>
                    <button type="button" className="button-primary" onClick={requestExecution} disabled={ex.running}>
                      {ex.running ? t('views.explorer.executing') : t('views.explorer.execute')}
                    </button>
                  </div>
                </div>

                {/* Response */}
                {ex.response && (
                  <div className="explorer-panel">
                    <h3>{t('views.explorer.response')}</h3>
                    <div className="flex items-center gap-md" style={{ marginBottom: 'var(--spacing-sm)' }}>
                      {typeof ex.response.status === 'number' ? <StatusBadge code={ex.response.status} /> : <span className="pill pill-danger">{ex.response.status}</span>}
                      <span className="text-muted">{formatDuration(ex.response.durationMs, i18n.language)}</span>
                      <span className="text-muted">{formatBytes(ex.response.size || 0)}</span>
                    </div>
                    <h3>{t('views.explorer.responseBody')}</h3>
                    <CodeBlock value={ex.response.body} prettyJson />
                    <h3 style={{ marginTop: 'var(--spacing-md)' }}>{t('views.explorer.responseHeaders')}</h3>
                    <CodeBlock value={ex.response.headers} prettyJson />
                  </div>
                )}

                {/* Code snippets */}
                <div className="explorer-panel">
                  <h3>{t('views.explorer.snippets')}</h3>
                  <div className="snippet-tabs">
                    {['curl', 'fetch', 'csharp'].map((s) => (
                      <button key={s} type="button" className={`snippet-tab ${snippetTab === s ? 'active' : ''}`} onClick={() => setSnippetTab(s)}>
                        {s === 'csharp' ? 'C#' : s}
                      </button>
                    ))}
                  </div>
                  <CodeBlock value={snippets[snippetTab]} />
                </div>

                {/* History */}
                <div className="explorer-panel">
                  <div className="flex items-center justify-between" style={{ marginBottom: 'var(--spacing-sm)' }}>
                    <h3 style={{ margin: 0 }}>{t('views.explorer.history')}</h3>
                    {ex.history.length > 0 && (
                      <button type="button" className="button-ghost button-sm" onClick={ex.clearHistory}>
                        {t('views.explorer.clearHistory')}
                      </button>
                    )}
                  </div>
                  {ex.history.length === 0 ? (
                    <p className="text-muted" style={{ fontSize: 'var(--font-size-sm)' }}>{t('views.explorer.historyEmpty')}</p>
                  ) : (
                    ex.history.map((h, i) => (
                      <div className="explorer-history-item" key={i}>
                        <span className="flex items-center gap-sm">
                          <MethodPill method={h.method} />
                          <code title={h.path}>{truncate(h.path, 32)}</code>
                          {typeof h.status === 'number' && <StatusBadge code={h.status} />}
                        </span>
                        <span className="flex items-center gap-sm">
                          <button type="button" className="button-ghost button-sm" onClick={() => ex.loadFromHistory(h)}>
                            {t('views.explorer.loadHistory')}
                          </button>
                          <button type="button" className="button-ghost button-sm" onClick={() => ex.deleteHistory(i)}>
                            {t('views.explorer.deleteHistory')}
                          </button>
                        </span>
                      </div>
                    ))
                  )}
                </div>
              </>
            )}
          </div>
        </div>
      )}

      <ConfirmModal
        open={confirmOpen}
        danger
        title={t('views.explorer.confirmTitle')}
        message={t('views.explorer.confirmMessage', { method: ex.operation?.method })}
        confirmLabel={t('views.explorer.execute')}
        onConfirm={() => {
          setConfirmOpen(false);
          ex.execute();
        }}
        onCancel={() => setConfirmOpen(false)}
      />
    </div>
  );
}
