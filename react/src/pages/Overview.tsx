import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { CheckCircle2, ShieldCheck, AlertTriangle, Truck, ArrowUp, ArrowDown, RotateCcw, ArrowLeftRight } from 'lucide-react';
import { DashboardLayoutComponent, type PanelModel } from '@syncfusion/ej2-react-layouts';
// The all-in-one tailwind3.css theme bundle (imported in theme.ts) does NOT
// include Dashboard Layout's positioning CSS (verified: zero `.e-panel`
// rules in that bundle) — without it, panels render as stacked static divs
// despite correct inline position:absolute coordinates. Import the
// component-specific stylesheet here, matching the skill's getting-started
// guidance, but note the real installed path is singular
// (`dashboard-layout/index.css`), not `dashboard-layouts/` as the skill
// reference names it.
import '@syncfusion/ej2-tailwind3-theme/styles/dashboard-layout/index.css';
import { ButtonComponent } from '@syncfusion/ej2-react-buttons';
import {
  ChartComponent, SeriesCollectionDirective, SeriesDirective, Inject,
  ColumnSeries, LineSeries, Category, Tooltip, Legend, DataLabel, DateTime,
} from '@syncfusion/ej2-react-charts';
import { api, ApiError } from '../api/client.ts';
import type { OverviewDto } from '../api/types.ts';
import { useActiveExceptionDelta } from '../state/sessionOverrides.ts';

// Dashboard Layout is the Overview page's content region per DESIGN.md; the
// four KPI Card panels follow the Syncfusion KPI Card pattern (compact left
// inset, top-right square status icon, actionable cards using native link
// semantics, no duplicate "View" control).

const PANEL_ORDER_KEY = ['on-time', 'otif', 'exceptions', 'utilization'] as const;
type PanelId = (typeof PANEL_ORDER_KEY)[number];

function tone(value: number, warnBelow: number, dangerBelow: number): 'success' | 'warning' | 'danger' {
  if (value < dangerBelow) return 'danger';
  if (value < warnBelow) return 'warning';
  return 'success';
}

export function Overview({ onNavigate }: { onNavigate: (page: string, event?: React.MouseEvent<HTMLAnchorElement>) => void }) {
  const [data, setData] = useState<OverviewDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [order, setOrder] = useState<PanelId[]>([...PANEL_ORDER_KEY]);
  const [reorderMode, setReorderMode] = useState(false);
  const sessionDelta = useActiveExceptionDelta();

  // Dashboard Layout sizes a cell as width/cellAspectRatio, so a fixed ratio
  // grows the panel with the container and leaves empty space under the card.
  // Measure the rendered card and set the ratio so the panel border box matches
  // that card (plus the panel's own border). Below the component's 600px
  // mediaQuery it stacks to one column; use that same column count here.
  const dashboardWrapRef = useRef<HTMLDivElement>(null);
  const [kpiColumns, setKpiColumns] = useState(4);
  const [kpiAspectRatio, setKpiAspectRatio] = useState(1.3);
  const CELL_SPACING = 16;
  useEffect(() => {
    const el = dashboardWrapRef.current;
    if (!el) return;
    let frame = 0;
    let attempts = 0;
    const compute = () => {
      const containerWidth = el.offsetWidth;
      if (containerWidth <= 0) return;
      const columns = containerWidth <= 600 ? 1 : containerWidth <= 1024 ? 2 : 4;
      const cellWidth = (containerWidth - (columns - 1) * CELL_SPACING) / columns;
      const panel = el.querySelector<HTMLElement>('.e-panel');
      const cards = [...el.querySelectorAll<HTMLElement>('.e-panel .e-card')];
      if (!panel || cards.length === 0 || cellWidth <= 0) {
        if (attempts++ < 30) frame = requestAnimationFrame(compute);
        return;
      }
      const panelStyle = getComputedStyle(panel);
      const borderY = (parseFloat(panelStyle.borderTopWidth) || 0) + (parseFloat(panelStyle.borderBottomWidth) || 0);
      const cardHeight = Math.max(...cards.map(card => card.getBoundingClientRect().height));
      const targetHeight = cardHeight + borderY;
      setKpiColumns(columns);
      setKpiAspectRatio(prev => {
        if (Math.abs(cellWidth / prev - targetHeight) < 0.5) return prev;
        return Math.max(0.5, cellWidth / targetHeight);
      });
    };
    compute();
    const observer = new ResizeObserver(() => {
      cancelAnimationFrame(frame);
      attempts = 0;
      frame = requestAnimationFrame(compute);
    });
    observer.observe(el);
    return () => {
      cancelAnimationFrame(frame);
      observer.disconnect();
    };
    // Re-run once `data` flips truthy and the dashboard-wrap div (holding the
    // ref) actually mounts — on first render it's still behind the loading
    // skeleton, so an empty dependency array would fire this effect while
    // dashboardWrapRef.current is still null and never re-run.
  }, [data]);

  const load = () => {
    setLoading(true);
    setError(null);
    api.getOverview()
      .then(setData)
      .catch(err => setError(err instanceof ApiError ? err.message : 'Unable to reach the fleet API.'))
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  function movePanel(id: PanelId, direction: -1 | 1) {
    setOrder(prev => {
      const index = prev.indexOf(id);
      const target = index + direction;
      if (target < 0 || target >= prev.length) return prev;
      const next = [...prev];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  const panelBody = useMemo(() => {
    if (!data) return { panels: {} as Record<PanelId, React.ReactNode>, chartNode: null as React.ReactNode };
    const onTimeTone = tone(data.onTimeRate, 90, 75);
    const otifTone = tone(data.otifRate, 85, 70);
    const effectiveExceptionCount = Math.max(0, data.activeExceptionCount + sessionDelta);
    const exceptionsTone: 'success' | 'warning' | 'danger' = effectiveExceptionCount === 0 ? 'success' : effectiveExceptionCount > 5 ? 'danger' : 'warning';
    const utilTone = tone(data.fleetUtilization, 60, 40);

    const kpiCard = (opts: { label: string; value: string; note: string; icon: React.ReactNode; toneName: string; actionable?: string }) => (
      opts.actionable
        ? <a className="e-card metric metric-interactive" href="#" role="link" tabIndex={0}
            aria-label={`${opts.label}: ${opts.value}. ${opts.note}`}
            onClick={event => { event.preventDefault(); onNavigate(opts.actionable!); }}
            onKeyDown={(event: KeyboardEvent<HTMLAnchorElement>) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onNavigate(opts.actionable!); } }}>
            <div className="e-card-content">
              <span className="metric-label">{opts.label}</span>
              <span className="metric-icon" data-tone={opts.toneName} aria-hidden="true">{opts.icon}</span>
              <span className="metric-number">{opts.value}</span>
              <span className="metric-note">{opts.note}</span>
            </div>
          </a>
        : <div className="e-card metric">
            <div className="e-card-content">
              <span className="metric-label">{opts.label}</span>
              <span className="metric-icon" data-tone={opts.toneName} aria-hidden="true">{opts.icon}</span>
              <span className="metric-number">{opts.value}</span>
              <span className="metric-note">{opts.note}</span>
            </div>
          </div>
    );

    const trend = data.onTimeTrend.map(point => ({ date: new Date(point.date), rate: point.onTimeRate }));
    const severity = data.exceptionsBySeverity.map(bucket => ({ severity: bucket.severity, count: bucket.count }));

    const chartNode = <div className="chart-wrap">
      <ChartComponent id="overview-chart" height="300px" background="transparent"
        // Without an explicit interval, the DateTime axis auto-computes a sub-day tick
        // spacing for this 7-point daily series, rendering each date's label twice (two
        // ticks landing on the same calendar day). Force exactly one tick per day.
        primaryXAxis={{ valueType: 'DateTime', labelFormat: 'MMM d', intervalType: 'Days', interval: 1, majorGridLines: { width: 0 } }}
        primaryYAxis={{ minimum: 0, maximum: 100, interval: 20, labelFormat: '{value}%', lineStyle: { width: 0 }, majorTickLines: { width: 0 } }}
        chartArea={{ border: { width: 0 } }} tooltip={{ enable: true, shared: true }} legendSettings={{ visible: true, position: 'Top' }}>
        {/* Line, not Spline: a cubic spline fitted through this sparse,
            spiky daily-rate data (mostly 0%, occasional spikes) overshoots
            below the flat zero segments between points -- clipped invisible
            by primaryYAxis's minimum: 0, it looked like the line was broken
            into disconnected pieces even though the underlying data has no
            gaps at all. A straight-line series can't overshoot, and is the
            more honest representation anyway: there's no real in-between
            value for a rate measured once per day. */}
        <Inject services={[ColumnSeries, LineSeries, Category, DateTime, Tooltip, Legend, DataLabel]} />
        <SeriesCollectionDirective>
          <SeriesDirective dataSource={trend} type="Line" xName="date" yName="rate" name="On-time rate" width={2} fill="var(--chart-series-primary)" marker={{ visible: true, width: 6, height: 6 }} />
        </SeriesCollectionDirective>
      </ChartComponent>
      <p className="page-footnote" style={{ padding: '0 var(--lg) var(--md)' }}>
        Exception mix this scenario: {severity.map(s => `${s.severity} ${s.count}`).join(' · ') || 'none'}
      </p>
    </div>;

    return {
      panels: {
        'on-time': kpiCard({ label: 'On-Time Rate', value: `${data.onTimeRate.toFixed(1)}%`, note: 'Delivered within the SLA window', icon: <CheckCircle2 size={17} />, toneName: onTimeTone }),
        'otif': kpiCard({ label: 'OTIF', value: `${data.otifRate.toFixed(1)}%`, note: 'On-time, in-full, no open exceptions', icon: <ShieldCheck size={17} />, toneName: otifTone }),
        'exceptions': kpiCard({ label: 'Active Exceptions', value: String(effectiveExceptionCount), note: sessionDelta !== 0 ? `${sessionDelta > 0 ? '+' : ''}${sessionDelta} from session-only changes` : 'Open, investigating, or escalated', icon: <AlertTriangle size={17} />, toneName: exceptionsTone, actionable: 'exceptions' }),
        'utilization': kpiCard({ label: 'Fleet Utilization', value: `${data.fleetUtilization.toFixed(1)}%`, note: 'Active vehicles currently on route', icon: <Truck size={17} />, toneName: utilTone, actionable: 'schedules' }),
      } satisfies Record<PanelId, React.ReactNode>,
      chartNode,
    };
  }, [data, onNavigate, sessionDelta]);

  // KPI panels get no Dashboard Layout header — the Card inside already
  // shows its own label per the KPI Card pattern (design-system.md), so a
  // second header would duplicate it and waste the panel's vertical space.
  // `moveLabel` still gives the "Move panel" keyboard buttons a real name.
  const panelMeta: Record<PanelId, { moveLabel: string }> = {
    'on-time': { moveLabel: 'On-Time Rate' },
    'otif': { moveLabel: 'OTIF' },
    'exceptions': { moveLabel: 'Active Exceptions' },
    'utilization': { moveLabel: 'Fleet Utilization' },
  };

  if (loading) return <>
    <div className="metrics">{Array.from({ length: 4 }).map((_, i) => <div key={i} className="skeleton" />)}</div>
    <div className="skeleton" style={{ height: 340 }} />
  </>;

  if (error) return <div className="notice error" role="alert">
    {error}
    <div className="form-actions" style={{ marginTop: 12 }}><ButtonComponent cssClass="action-button" onClick={load}><RotateCcw size={14} aria-hidden="true" /> Retry</ButtonComponent></div>
  </div>;

  if (!data) return <div className="empty-state"><h2>No fleet data for this scenario</h2></div>;

  return <>
    <div className="toolbar" style={{ border: 'none', padding: '0 0 var(--md)' }}>
      <span className="toolbar-note" style={{ marginLeft: 0 }}>Panels are draggable and resizable this session (not persisted).</span>
      <ButtonComponent cssClass="action-button" aria-pressed={reorderMode} onClick={() => setReorderMode(v => !v)}>
        <ArrowLeftRight size={14} aria-hidden="true" /> {reorderMode ? 'Done reordering' : 'Reorder with keyboard'}
      </ButtonComponent>
    </div>
    {reorderMode && <div className="toolbar" style={{ border: 'none', padding: '0 0 var(--md)', flexWrap: 'wrap' }} role="group" aria-label="Move panel earlier or later">
      {order.map(id => <span key={id} className="inline" style={{ gap: 4 }}>
        <span className="toolbar-note" style={{ marginLeft: 0 }}>{panelMeta[id].moveLabel}</span>
        <ButtonComponent cssClass="icon-button" aria-label={`Move ${panelMeta[id].moveLabel} earlier`} onClick={() => movePanel(id, -1)}><ArrowUp size={14} /></ButtonComponent>
        <ButtonComponent cssClass="icon-button" aria-label={`Move ${panelMeta[id].moveLabel} later`} onClick={() => movePanel(id, 1)}><ArrowDown size={14} /></ButtonComponent>
      </span>)}
    </div>}
    <div className="dashboard-wrap" ref={dashboardWrapRef}>
      <DashboardLayoutComponent id="overview-dashboard" columns={kpiColumns} cellSpacing={[16, 16]} cellAspectRatio={kpiAspectRatio} allowDragging allowResizing key={`${order.join(',')}-${kpiColumns}-${kpiAspectRatio.toFixed(2)}`}
        panels={order.map((id, index): PanelModel => {
          const body = panelBody.panels[id];
          // Dashboard Layout's PanelModel.content wants a template function
          // (rendered as a component), not a pre-built element — passing an
          // element directly makes the wrapper try to use it as a React
          // component type and throw "Element type is invalid".
          return { id, row: Math.floor(index / kpiColumns), col: index % kpiColumns, sizeX: 1, sizeY: 1, content: () => body };
        })}
      />
    </div>
    <section className="panel" aria-label="On-time trend and exception mix">
      <div className="panel-heading"><div><h2>On-time trend & exception mix</h2></div></div>
      {panelBody.chartNode}
    </section>
    <p className="page-footnote">Metrics are computed server-side from the full current dataset, not a filtered page. All names, routes, and shipments are synthetic.</p>
  </>;
}
