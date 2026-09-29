import { useEffect, useState } from 'react';
import { CheckCircle2, XCircle, AlertTriangle, RotateCcw, Wifi, WifiOff } from 'lucide-react';
import { ButtonComponent } from '@syncfusion/ej2-react-buttons';
import { api } from '../api/client.ts';
import type { HealthDto } from '../api/types.ts';
import { healthTone } from '../domain/status.ts';

// A dedicated, unauthenticated status page reachable directly at <base>/health,
// rendered bare (no app shell/sidebar) so it reads as an operations status page
// rather than a workspace tab. It reports this static frontend's own build info
// plus a LIVE fetch of the API's own /health (through /api/health -- the only
// path the same-origin proxy forwards to the backend), so opening it directly
// in a browser shows real, current data on both sides, not a placeholder that
// always says "ok".

function toneIcon(tone: ReturnType<typeof healthTone>) {
  if (tone === 'success') return <CheckCircle2 size={16} aria-hidden="true" />;
  if (tone === 'warning') return <AlertTriangle size={16} aria-hidden="true" />;
  return <XCircle size={16} aria-hidden="true" />;
}

function formatUptime(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  return [h && `${h}h`, m && `${m}m`, `${s}s`].filter(Boolean).join(' ');
}

export function Health() {
  const [api_, setApiHealth] = useState<HealthDto | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [checkedAt, setCheckedAt] = useState<Date | null>(null);
  const [loading, setLoading] = useState(true);

  const load = () => {
    setLoading(true);
    setApiError(null);
    api.getApiHealth()
      .then(result => { setApiHealth(result); setCheckedAt(new Date()); })
      .catch(() => { setApiHealth(null); setApiError('Unable to reach the API — it may be down, or unreachable from this network.'); setCheckedAt(new Date()); })
      .finally(() => setLoading(false));
  };
  useEffect(load, []);

  const frontendStatus: 'Healthy' = 'Healthy'; // this page rendered at all is proof the static bundle is live
  const apiTone = apiError ? 'danger' : api_ ? healthTone(api_.status) : 'neutral';

  return <main className="page-content health-page" aria-label="Application health status">
    <div className="page-heading"><div>
      <div className="eyebrow">Logistics Fleet &amp; Delivery Operations Dashboard</div>
      <h1>System health</h1>
      <p>Live status for the frontend bundle and the ASP.NET Core + PostgreSQL API. Not part of the workspace navigation — a direct monitoring URL.</p>
    </div></div>

    <section className="panel" aria-label="Frontend status">
      <div className="panel-heading"><div><h2>Frontend (this bundle)</h2></div></div>
      <div className="health-grid">
        <div className="health-row"><span className="status-badge" data-tone="success">{toneIcon('success')} {frontendStatus}</span></div>
        <div className="health-row"><span className="muted">Service</span><strong>logistics-fleet-operations-react</strong></div>
        <div className="health-row"><span className="muted">Version</span><strong>{__APP_VERSION__}</strong></div>
        <div className="health-row"><span className="muted">Build timestamp</span><strong>{__APP_BUILD_TIME__}</strong></div>
        <div className="health-row"><span className="muted">Page loaded</span><strong>{new Date().toLocaleString('en-US')}</strong></div>
      </div>
    </section>

    <section className="panel" aria-label="API status">
      <div className="panel-heading">
        <div><h2>API (live check)</h2><p>Fetched from /api/health {checkedAt ? `at ${checkedAt.toLocaleTimeString('en-US')}` : ''}</p></div>
        <ButtonComponent cssClass="action-button" onClick={load} disabled={loading}><RotateCcw size={14} aria-hidden="true" /> {loading ? 'Checking…' : 'Re-check now'}</ButtonComponent>
      </div>
      {loading && !checkedAt && <div className="skeleton" style={{ height: 160 }} />}
      {apiError && <div className="notice error" role="alert"><WifiOff size={14} aria-hidden="true" style={{ verticalAlign: '-2px', marginRight: 6 }} />{apiError}</div>}
      {api_ && <div className="health-grid">
        <div className="health-row"><span className="status-badge" data-tone={apiTone}>{toneIcon(apiTone)} {api_.status}</span></div>
        <div className="health-row"><span className="muted">Service</span><strong>{api_.service}</strong></div>
        <div className="health-row"><span className="muted">Version</span><strong>{api_.version}</strong></div>
        <div className="health-row"><span className="muted">Environment</span><strong>{api_.environment}</strong></div>
        <div className="health-row"><span className="muted">Uptime</span><strong>{formatUptime(api_.uptimeSeconds)}</strong></div>
        <div className="health-row"><span className="muted">Checked at (server)</span><strong>{new Date(api_.timestamp).toLocaleString('en-US')}</strong></div>
        <div className="health-row"><span className="muted">Total check duration</span><strong>{api_.totalDurationMs.toFixed(0)} ms</strong></div>
        <table className="health-checks-table">
          <thead><tr><th>Dependency</th><th>Status</th><th>Duration</th><th>Detail</th></tr></thead>
          <tbody>
            {api_.checks.map(check => <tr key={check.name}>
              <td>{check.name}</td>
              <td><span className="status-badge" data-tone={healthTone(check.status)}>{toneIcon(healthTone(check.status))} {check.status}</span></td>
              <td>{check.durationMs.toFixed(0)} ms</td>
              <td>{check.error ?? check.description ?? '—'}</td>
            </tr>)}
          </tbody>
        </table>
      </div>}
    </section>
    <p className="page-footnote"><Wifi size={12} aria-hidden="true" style={{ verticalAlign: '-1px', marginRight: 4 }} />Live status of the showcase frontend and its API. The demo data on other pages is synthetic.</p>
  </main>;
}
