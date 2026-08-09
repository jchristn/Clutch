import { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import Sidebar from './Sidebar';
import Topbar from './Topbar';
import HomeView from '../views/HomeView';
import LocksView from '../views/LocksView';
import LockActivityView from '../views/LockActivityView';
import TenantsView from '../views/TenantsView';
import UsersView from '../views/UsersView';
import CredentialsView from '../views/CredentialsView';
import RequestHistoryView from '../views/RequestHistoryView';
import ApiExplorerView from '../views/ApiExplorerView';
import SettingsView from '../views/SettingsView';

const VIEWS = {
  home: HomeView,
  locks: LocksView,
  lockActivity: LockActivityView,
  tenants: TenantsView,
  users: UsersView,
  credentials: CredentialsView,
  requests: RequestHistoryView,
  explorer: ApiExplorerView,
  settings: SettingsView
};

export default function Dashboard() {
  const { section = 'home' } = useParams();
  const { apiClient } = useAuth();
  const [collapsed, setCollapsed] = useState(false);
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const [health, setHealth] = useState(null);

  // Poll health for the topbar status dot.
  useEffect(() => {
    if (!apiClient) return undefined;
    let active = true;
    const check = () => {
      apiClient
        .healthCheck()
        .then(() => active && setHealth(true))
        .catch(() => active && setHealth(false));
    };
    check();
    const id = setInterval(check, 30000);
    return () => {
      active = false;
      clearInterval(id);
    };
  }, [apiClient]);

  const View = VIEWS[section] || HomeView;

  const cls = ['dashboard'];
  if (collapsed) cls.push('sidebar-collapsed');
  if (mobileNavOpen) cls.push('mobile-nav-open');

  return (
    <div className={cls.join(' ')}>
      <Sidebar activeSection={section} onNavigateMobile={() => setMobileNavOpen(false)} />
      <Topbar
        onToggleSidebar={() => {
          setCollapsed((c) => !c);
          setMobileNavOpen((m) => !m);
        }}
        health={health}
      />
      <main className="dashboard-content">
        <View apiClient={apiClient} />
      </main>
    </div>
  );
}
