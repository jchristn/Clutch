import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { principalCaps } from '../utils/principal';
import {
  HomeIcon,
  LockIcon,
  ActivityIcon,
  BuildingIcon,
  UsersIcon,
  KeyIcon,
  ListIcon,
  PlayIcon,
  ServerIcon,
  SettingsIcon
} from './Icons';

const APP_VERSION = '0.1.0';

const ICONS = {
  home: HomeIcon,
  locks: LockIcon,
  lockActivity: ActivityIcon,
  tenants: BuildingIcon,
  users: UsersIcon,
  credentials: KeyIcon,
  requests: ListIcon,
  explorer: PlayIcon,
  settings: ServerIcon,
  settingsAdmin: SettingsIcon
};

// Grouped navigation. `requires` gates visibility (UX only).
const GROUPS = [
  { id: 'overview', items: [{ id: 'home' }] },
  { id: 'locks', items: [{ id: 'locks' }, { id: 'lockActivity' }] },
  {
    id: 'administration',
    items: [
      { id: 'tenants', requires: 'systemAdmin' },
      { id: 'users', requires: 'admin' },
      { id: 'credentials', requires: 'admin' }
    ]
  },
  { id: 'observability', items: [{ id: 'requests' }, { id: 'explorer' }] },
  { id: 'system', items: [{ id: 'settings' }, { id: 'settingsAdmin' }] }
];

function allowed(requires, caps) {
  if (!requires) return true;
  if (requires === 'systemAdmin') return caps.isSystemAdmin;
  if (requires === 'admin') return caps.isAdminOfSomething;
  return true;
}

export default function Sidebar({ activeSection, onNavigateMobile }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { principal } = useAuth();
  const caps = principalCaps(principal);

  const go = (id) => {
    navigate(`/dashboard/${id}`);
    onNavigateMobile?.();
  };

  return (
    <aside className="dashboard-sidebar">
      <div className="sidebar-brand">
        <img src="/logo.png" alt={t('common.appName')} />
        <span>{t('common.appName')}</span>
      </div>
      <nav className="sidebar-nav" aria-label={t('common.appName')}>
        {GROUPS.map((group) => {
          const items = group.items.filter((it) => allowed(it.requires, caps));
          if (items.length === 0) return null;
          return (
            <div className="sidebar-group" key={group.id}>
              <div className="sidebar-group-label">{t(`nav.groups.${group.id}`)}</div>
              {items.map((it) => {
                const Icon = ICONS[it.id];
                return (
                  <div
                    key={it.id}
                    className={`sidebar-item ${activeSection === it.id ? 'active' : ''}`}
                    onClick={() => go(it.id)}
                    role="link"
                    tabIndex={0}
                    onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && go(it.id)}
                    title={t(`nav.items.${it.id}.tip`)}
                  >
                    {Icon && <Icon size={18} />}
                    <span>{t(`nav.items.${it.id}.label`)}</span>
                  </div>
                );
              })}
            </div>
          );
        })}
      </nav>
      <div className="sidebar-footer">{t('nav.version', { version: APP_VERSION })}</div>
    </aside>
  );
}
