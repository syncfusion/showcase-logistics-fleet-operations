import { useEffect, useState, type FormEvent, type MouseEvent } from 'react';
import { Truck, LayoutDashboard, Map, CalendarClock, GanttChartSquare, AlertTriangle, Moon, Sun, Menu, X, LogIn, LogOut, RotateCcw } from 'lucide-react';
import { ButtonComponent } from '@syncfusion/ej2-react-buttons';
import { BreadcrumbComponent, BreadcrumbItemsDirective, BreadcrumbItemDirective, SidebarComponent } from '@syncfusion/ej2-react-navigations';
import { DialogComponent } from '@syncfusion/ej2-react-popups';
import { TextBoxComponent } from '@syncfusion/ej2-react-inputs';
import { applyTheme, type Theme } from './theme.ts';
import { useAuth } from './state/auth.ts';
import { api, ApiError } from './api/client.ts';
import { Overview } from './pages/Overview.tsx';
import { LiveMap } from './pages/LiveMap.tsx';
import { DriverSchedules } from './pages/DriverSchedules.tsx';
import { DeliveryTimeline } from './pages/DeliveryTimeline.tsx';
import { Exceptions } from './pages/Exceptions.tsx';
import { Health } from './pages/Health.tsx';

const routes = [
  { id: '', label: 'Overview', title: 'Fleet overview', description: 'KPIs, trend, and exception mix for the current scenario.', icon: LayoutDashboard },
  { id: 'map', label: 'Live Map', title: 'Live map', description: 'Depots, active routes, and synthetic vehicle positions.', icon: Map },
  { id: 'schedules', label: 'Driver Schedules', title: 'Driver schedules', description: 'One resource row per driver, shifts and route assignments.', icon: CalendarClock },
  { id: 'timeline', label: 'Delivery Timeline', title: 'Delivery timeline', description: 'Planned windows, stop sequence, and actual progress.', icon: GanttChartSquare },
  { id: 'exceptions', label: 'Exceptions', title: 'Exceptions', description: 'Triage shipment exceptions from Open through Resolved.', icon: AlertTriangle },
] as const;

const href = (id: string) => `/${id}`;
const readRoute = () => location.pathname.slice(1).replace(/\/$/, '');
// Matches the shell breakpoint in styles.css. Syncfusion Auto mode keys off
// the device user-agent, so a desktop window narrowed below this still stays
// Push — switch type from the viewport instead.
const NARROW_SHELL = '(max-width: 800px)';

function useNarrowShell() {
  const [narrow, setNarrow] = useState(() => window.matchMedia(NARROW_SHELL).matches);
  useEffect(() => {
    const media = window.matchMedia(NARROW_SHELL);
    const sync = () => setNarrow(media.matches);
    media.addEventListener('change', sync);
    return () => media.removeEventListener('change', sync);
  }, []);
  return narrow;
}

// The scenario's demo data is always seeded relative to "now" (see backend DemoDataSeeder), so this
// label must track the real current date rather than a fixed string that goes stale between reseeds.
const demoDateShort = new Intl.DateTimeFormat('en-US', { timeZone: 'America/New_York', month: 'short', day: 'numeric', year: 'numeric' });
const demoDateLong = new Intl.DateTimeFormat('en-US', { timeZone: 'America/New_York', month: 'long', day: 'numeric', year: 'numeric' });

export function App({ initialTheme }: { initialTheme: Theme }) {
  const [route, setRoute] = useState(readRoute);
  const [theme, setTheme] = useState(initialTheme);
  const [themeBusy, setThemeBusy] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [sidebarAnimated, setSidebarAnimated] = useState(false);
  const narrow = useNarrowShell();
  const [signInOpen, setSignInOpen] = useState(false);
  const [signInEmail, setSignInEmail] = useState('demo@logistics-fleet-operations.showcase');
  const [signInPassword, setSignInPassword] = useState('');
  const [resetOpen, setResetOpen] = useState(false);
  const [resetBusy, setResetBusy] = useState(false);
  const [resetError, setResetError] = useState<string | null>(null);
  const [demoNow, setDemoNow] = useState(() => new Date());
  const auth = useAuth();
  const current = routes.find(item => item.id === route);

  useEffect(() => {
    const id = setInterval(() => setDemoNow(new Date()), 5 * 60_000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    const pop = () => setRoute(readRoute());
    window.addEventListener('popstate', pop);
    return () => window.removeEventListener('popstate', pop);
  }, []);

  useEffect(() => { setSidebarAnimated(true); }, []);
  useEffect(() => { if (!narrow) setMenuOpen(false); }, [narrow]);

  useEffect(() => {
    if (!narrow || !menuOpen || signInOpen || resetOpen) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      setMenuOpen(false);
      document.getElementById('workspace-menu-button')?.focus();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [narrow, menuOpen, signInOpen, resetOpen]);

  useEffect(() => {
    if (!narrow || !menuOpen) return;
    const onPointer = (event: Event) => {
      const target = event.target;
      if (!(target instanceof Node)) return;
      if (document.getElementById('workspace-sidebar')?.contains(target)) return;
      if (document.getElementById('workspace-menu-button')?.contains(target)) return;
      setMenuOpen(false);
    };
    document.addEventListener('mousedown', onPointer);
    document.addEventListener('touchstart', onPointer);
    return () => {
      document.removeEventListener('mousedown', onPointer);
      document.removeEventListener('touchstart', onPointer);
    };
  }, [narrow, menuOpen]);

  useEffect(() => {
    if (!narrow || !menuOpen) return;
    const frame = requestAnimationFrame(() => {
      document.querySelector<HTMLElement>('#workspace-sidebar .navigation a[aria-current="page"]')?.focus();
    });
    return () => cancelAnimationFrame(frame);
  }, [narrow, menuOpen]);

  function onSidebarOpen() {
    if (window.matchMedia(NARROW_SHELL).matches) setMenuOpen(true);
  }
  function onSidebarClose() {
    if (window.matchMedia(NARROW_SHELL).matches) setMenuOpen(false);
  }

  function navigate(id: string, event?: MouseEvent<HTMLAnchorElement>) {
    if (event && (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey)) return;
    event?.preventDefault();
    history.pushState({}, '', href(id));
    setRoute(id);
    setMenuOpen(false);
    requestAnimationFrame(() => document.getElementById('main-heading')?.focus());
  }

  function onBreadcrumbClick(args: { cancel: boolean; item: { id?: string }; event?: Event }) {
    const mouse = args.event as MouseEvent | undefined;
    if (mouse && (mouse.metaKey || mouse.ctrlKey || mouse.shiftKey || mouse.altKey)) return;
    args.cancel = true;
    if (args.item?.id === 'workspace') navigate('');
  }

  async function toggleTheme() {
    setThemeBusy(true);
    try { const next = theme === 'light' ? 'dark' : 'light'; await applyTheme(next); setTheme(next); }
    finally { setThemeBusy(false); }
  }

  async function submitSignIn(event: FormEvent) {
    event.preventDefault();
    const ok = await auth.signIn(signInEmail, signInPassword);
    if (ok) { setSignInOpen(false); setSignInPassword(''); }
  }

  // Undoes in-app writes (an exception resolved, a schedule edit, a route reassignment) that
  // persisted to PostgreSQL and have no other way to revert — reloads afterward so every page
  // re-fetches the restored baseline rather than showing stale in-memory state.
  async function confirmResetDemoData() {
    if (!auth.token) return;
    setResetBusy(true);
    setResetError(null);
    try {
      await api.resetDemoData(auth.token);
      location.reload();
    } catch (err) {
      setResetError(err instanceof ApiError ? err.message : 'Unable to reset demo data. Please try again.');
      setResetBusy(false);
    }
  }

  const navigation = <nav className="navigation" aria-label="Main navigation">
    {routes.map(({ id, label, icon: Icon }) =>
      <a key={id || 'overview'} href={href(id)} aria-current={route === id ? 'page' : undefined} onClick={event => navigate(id, event)}>
        <Icon size={18} aria-hidden="true" />{label}
      </a>)}
  </nav>;

  // Bare, unauthenticated status page (not in `routes`/the sidebar nav) --
  // reads as a standalone monitoring URL.
  if (route === 'health') return <Health />;

  return <>
    <a className="skip-link" href="#main-content" onClick={() => requestAnimationFrame(() => document.getElementById('main-heading')?.focus())}>Skip to content</a>
    <div className="app-shell">
      {/* No `target`: a target makes the sidebar absolute inside a clipping
          container, so a phone drawer scrolls off-screen with the page. */}
      <SidebarComponent
        key={narrow ? 'narrow' : 'wide'}
        id="workspace-sidebar"
        width="224px"
        type={narrow ? 'Over' : 'Push'}
        position="Left"
        isOpen={!narrow || menuOpen}
        showBackdrop={narrow}
        closeOnDocumentClick={false}
        enableGestures={narrow}
        animate={sidebarAnimated}
        zIndex={1001}
        open={onSidebarOpen}
        close={onSidebarClose}
      >
        <div className="brand"><span className="brand-icon"><Truck size={24} /></span><div><strong>FLEET OPS</strong><small>Logistics dashboard</small></div><ButtonComponent cssClass="icon-button sidebar-close" type="button" aria-label="Close navigation" onClick={() => setMenuOpen(false)}><X size={18} /></ButtonComponent></div>
        <div><div className="nav-label">Workspace</div>{navigation}</div>
        <div className="sidebar-footer"><strong>Built with Syncfusion</strong></div>
      </SidebarComponent>
      <div className="workspace">
        <header className="topbar">
          <div className="topbar-left">
            <ButtonComponent id="workspace-menu-button" cssClass="icon-button mobile-menu" aria-label={menuOpen ? 'Close navigation' : 'Open navigation'} aria-controls="workspace-sidebar" aria-expanded={menuOpen} onClick={() => setMenuOpen(open => !open)}><Menu size={18} /></ButtonComponent>
            <BreadcrumbComponent cssClass="workspace-breadcrumb" enableNavigation overflowMode="Menu" itemClick={onBreadcrumbClick}>
              <BreadcrumbItemsDirective>
                <BreadcrumbItemDirective id="workspace" text="Workspace" url={href('')} iconCss="e-icons e-home" />
                <BreadcrumbItemDirective id={route || 'overview'} text={current?.label ?? 'Not found'} url={href(route)} />
              </BreadcrumbItemsDirective>
            </BreadcrumbComponent>
          </div>
          <div className="topbar-actions">
            <div className="demo-clock"><strong>Demo time · {demoDateShort.format(demoNow)}</strong>America/New_York</div>
            {auth.token
              ? <span className="profile-pill" title={auth.email ?? undefined}>Signed in · {auth.email}</span>
              : <span className="profile-pill" data-signed-out="true">Public session</span>}
            {auth.token
              && <ButtonComponent cssClass="action-button" title="Undo writes made this session and restore the fixed demo dataset" onClick={() => { setResetError(null); setResetOpen(true); }}><RotateCcw size={14} aria-hidden="true" /> Reset demo data</ButtonComponent>}
            {auth.token
              ? <ButtonComponent cssClass="action-button" onClick={auth.signOut}><LogOut size={14} aria-hidden="true" /> Sign out</ButtonComponent>
              : <ButtonComponent cssClass="action-button" onClick={() => setSignInOpen(true)}><LogIn size={14} aria-hidden="true" /> Sign in</ButtonComponent>}
            <ButtonComponent cssClass="icon-button" aria-label={`Switch to ${theme === 'light' ? 'dark' : 'light'} theme`} disabled={themeBusy} onClick={toggleTheme}>{theme === 'light' ? <Moon size={17} /> : <Sun size={17} />}</ButtonComponent>
          </div>
        </header>
        <main id="main-content" className="page-content">
          <div className="page-heading"><div><div className="eyebrow">Logistics Fleet &amp; Delivery Operations Dashboard</div><h1 id="main-heading" tabIndex={-1}>{current?.title ?? 'Page not found'}</h1><p>{current?.description ?? 'Choose a page from the workspace navigation.'}</p></div></div>
          {route === '' && <Overview onNavigate={navigate} />}
          {route === 'map' && <LiveMap />}
          {route === 'schedules' && <DriverSchedules />}
          {route === 'timeline' && <DeliveryTimeline />}
          {route === 'exceptions' && <Exceptions auth={auth} />}
          <p className="page-footnote">Public visitors: interactions this UI offers are simulated in this tab. Demo clock: {demoDateLong.format(demoNow)}, America/New_York.</p>
        </main>
      </div>
    </div>
    {signInOpen && <DialogComponent header="Sign in to the demo account" visible isModal showCloseIcon width="420px" target="#root" close={() => setSignInOpen(false)} animationSettings={{ effect: 'None' }}>
      <form onSubmit={submitSignIn}>
        <p className="form-help">Sign in with the demo account to save changes. This demonstration authentication is for the showcase only.</p>
        <div className="form-grid">
          <div className="field full">
            <label htmlFor="signin-email">Email</label>
            <TextBoxComponent id="signin-email" value={signInEmail} change={(e: { value: string }) => setSignInEmail(e.value)} />
          </div>
          <div className="field full">
            <label htmlFor="signin-password">Password</label>
            <TextBoxComponent id="signin-password" type="password" value={signInPassword} change={(e: { value: string }) => setSignInPassword(e.value)} />
          </div>
        </div>
        {auth.error && <div className="notice error" role="alert">{auth.error}</div>}
        <div className="form-actions">
          <ButtonComponent cssClass="action-button" type="button" onClick={() => setSignInOpen(false)}>Cancel</ButtonComponent>
          <ButtonComponent cssClass="primary-button" type="submit" disabled={auth.signingIn}>{auth.signingIn ? 'Signing in…' : 'Sign in'}</ButtonComponent>
        </div>
      </form>
    </DialogComponent>}
    {resetOpen && <DialogComponent header="Reset demo data?" visible isModal showCloseIcon width="420px" target="#root" close={() => !resetBusy && setResetOpen(false)} animationSettings={{ effect: 'None' }}>
      <p className="form-help">This restores the fixed demo dataset and undoes every write made this session — exception status changes, schedule edits, route reassignments. It cannot be undone. Public/read-only data is unaffected.</p>
      {resetError && <div className="notice error" role="alert">{resetError}</div>}
      <div className="form-actions">
        <ButtonComponent cssClass="action-button" type="button" disabled={resetBusy} onClick={() => setResetOpen(false)}>Cancel</ButtonComponent>
        <ButtonComponent cssClass="primary-button" type="button" disabled={resetBusy} onClick={confirmResetDemoData}>{resetBusy ? 'Resetting…' : 'Reset data'}</ButtonComponent>
      </div>
    </DialogComponent>}
  </>;
}
