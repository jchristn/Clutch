import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { DEV_DEFAULTS } from '../utils/constants';
import LanguageSelector from './LanguageSelector';
import { EyeIcon, EyeOffIcon } from './Icons';

export default function Login() {
  const { t } = useTranslation();
  const { login } = useAuth();

  const [tab, setTab] = useState('key'); // 'key' | 'password'
  const [serverUrl, setServerUrl] = useState(DEV_DEFAULTS.serverUrl);
  const [accessKey, setAccessKey] = useState(DEV_DEFAULTS.accessKey);
  const [secretKey, setSecretKey] = useState(DEV_DEFAULTS.secretKey);
  const [tenantId, setTenantId] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showSecret, setShowSecret] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      if (tab === 'key') {
        await login(serverUrl, 'key', { accessKey, secretKey });
      } else {
        await login(serverUrl, 'password', { tenantId, email, password });
      }
    } catch (err) {
      setError(err?.message ? String(err.message) : t('login.errorGeneric'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-brand">
          <img src="/logo.png" alt={t('common.appName')} />
          <h1>{t('login.title')}</h1>
          <p>{t('login.subtitle')}</p>
        </div>

        <div className="login-tabs" role="tablist">
          <button
            type="button"
            role="tab"
            aria-selected={tab === 'key'}
            className={`login-tab ${tab === 'key' ? 'active' : ''}`}
            onClick={() => setTab('key')}
          >
            {t('login.tabs.appKey')}
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={tab === 'password'}
            className={`login-tab ${tab === 'password' ? 'active' : ''}`}
            onClick={() => setTab('password')}
          >
            {t('login.tabs.email')}
          </button>
        </div>

        <form className="login-form" onSubmit={submit}>
          <div className="field">
            <label htmlFor="serverUrl">{t('login.serverUrl')}</label>
            <input
              id="serverUrl"
              type="text"
              value={serverUrl}
              onChange={(e) => setServerUrl(e.target.value)}
              placeholder={t('login.serverUrlPlaceholder')}
              required
              disabled={loading}
            />
          </div>

          {tab === 'key' ? (
            <>
              <div className="field">
                <label htmlFor="accessKey">{t('login.accessKey')}</label>
                <input
                  id="accessKey"
                  type="text"
                  value={accessKey}
                  onChange={(e) => setAccessKey(e.target.value)}
                  placeholder={t('login.accessKeyPlaceholder')}
                  required
                  disabled={loading}
                  autoComplete="username"
                />
              </div>
              <div className="field">
                <label htmlFor="secretKey">{t('login.secretKey')}</label>
                <div className="password-wrap">
                  <input
                    id="secretKey"
                    type={showSecret ? 'text' : 'password'}
                    value={secretKey}
                    onChange={(e) => setSecretKey(e.target.value)}
                    placeholder={t('login.secretKeyPlaceholder')}
                    required
                    disabled={loading}
                    autoComplete="current-password"
                  />
                  <button
                    type="button"
                    className="icon-button"
                    onClick={() => setShowSecret((v) => !v)}
                    aria-label={showSecret ? t('login.hidePassword') : t('login.showPassword')}
                    title={showSecret ? t('login.hidePassword') : t('login.showPassword')}
                  >
                    {showSecret ? <EyeOffIcon /> : <EyeIcon />}
                  </button>
                </div>
              </div>
            </>
          ) : (
            <>
              <div className="field">
                <label htmlFor="tenantId">{t('login.tenantId')}</label>
                <input
                  id="tenantId"
                  type="text"
                  value={tenantId}
                  onChange={(e) => setTenantId(e.target.value)}
                  placeholder={t('login.tenantIdPlaceholder')}
                  required
                  disabled={loading}
                />
              </div>
              <div className="field">
                <label htmlFor="email">{t('login.email')}</label>
                <input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder={t('login.emailPlaceholder')}
                  required
                  disabled={loading}
                  autoComplete="username"
                />
              </div>
              <div className="field">
                <label htmlFor="password">{t('login.password')}</label>
                <div className="password-wrap">
                  <input
                    id="password"
                    type={showSecret ? 'text' : 'password'}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder={t('login.passwordPlaceholder')}
                    required
                    disabled={loading}
                    autoComplete="current-password"
                  />
                  <button
                    type="button"
                    className="icon-button"
                    onClick={() => setShowSecret((v) => !v)}
                    aria-label={showSecret ? t('login.hidePassword') : t('login.showPassword')}
                    title={showSecret ? t('login.hidePassword') : t('login.showPassword')}
                  >
                    {showSecret ? <EyeOffIcon /> : <EyeIcon />}
                  </button>
                </div>
              </div>
            </>
          )}

          {error && <div className="error-banner">{error}</div>}

          <button type="submit" className="button-primary" disabled={loading}>
            {loading ? t('login.connecting') : t('login.submit')}
          </button>
        </form>

        <div className="login-lang">
          <LanguageSelector />
        </div>
        <div className="login-footer">{t('login.footerHint')}</div>
      </div>
    </div>
  );
}
