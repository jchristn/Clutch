import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { GITHUB_URL } from '../utils/constants';
import { roleKey } from '../utils/principal';
import LanguageSelector from './LanguageSelector';
import CopyButton from './CopyButton';
import { MenuIcon, SunIcon, MoonIcon, GithubIcon, LogoutIcon } from './Icons';

/** Top bar: context chips on the left, utility actions on the right. */
export default function Topbar({ onToggleSidebar, health }) {
  const { t } = useTranslation();
  const { serverUrl, principal, theme, toggleTheme, logout } = useAuth();

  const healthState = health === true ? 'healthy' : health === false ? 'unhealthy' : 'unknown';

  return (
    <header className="dashboard-header">
      <div className="topbar">
        <div className="topbar-left">
          <button
            type="button"
            className="icon-button"
            onClick={onToggleSidebar}
            aria-label={t('nav.collapseSidebar')}
            title={t('nav.collapseSidebar')}
          >
            <MenuIcon />
          </button>
          <span className="topbar-chip role">{t(roleKey(principal))}</span>
          {principal?.principalName && (
            <span className="topbar-chip" title={principal.principalName}>
              <code>{principal.principalName}</code>
            </span>
          )}
          <span className="topbar-chip" title={serverUrl}>
            <span
              className={`health-dot ${healthState}`}
              title={t(`topbar.health.${healthState}`)}
            />
            <code>{serverUrl}</code>
            <CopyButton value={serverUrl} title={t('topbar.copyServer')} />
          </span>
        </div>

        <div className="topbar-right">
          <LanguageSelector compact />
          <a
            className="icon-button"
            href={GITHUB_URL}
            target="_blank"
            rel="noopener noreferrer"
            aria-label={t('common.actions.openGithub')}
            title={t('common.actions.openGithub')}
          >
            <GithubIcon />
          </a>
          <button
            type="button"
            className="icon-button"
            onClick={toggleTheme}
            aria-label={theme === 'dark' ? t('common.theme.switchToLight') : t('common.theme.switchToDark')}
            title={theme === 'dark' ? t('common.theme.switchToLight') : t('common.theme.switchToDark')}
          >
            {theme === 'dark' ? <SunIcon /> : <MoonIcon />}
          </button>
          <button
            type="button"
            className="icon-button"
            onClick={logout}
            aria-label={t('common.actions.logout')}
            title={t('common.actions.logout')}
          >
            <LogoutIcon />
          </button>
        </div>
      </div>
    </header>
  );
}
