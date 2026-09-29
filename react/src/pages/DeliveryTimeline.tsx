import { useEffect, useMemo, useRef, useState } from 'react';
import { Info, RotateCcw } from 'lucide-react';
import { ButtonComponent } from '@syncfusion/ej2-react-buttons';
import { GanttComponent, ColumnsDirective, ColumnDirective, Inject, Selection, Sort, Toolbar, DayMarkers } from '@syncfusion/ej2-react-gantt';
import type { LabelSettingsModel, TaskFieldsModel, TimelineSettingsModel, TooltipSettingsModel } from '@syncfusion/ej2-gantt';
import { api, ApiError } from '../api/client.ts';
import type { DriverDto, RouteDto, ShipmentDto } from '../api/types.ts';
import { routeTone, type Tone } from '../domain/status.ts';

// Seeded routes last a few hours. The default day duration unit and
// 08:00–17:00 working window collapse those bars to zero, so the chart
// stays hour-based across the full day. The chart pane itself carries the
// schedule: route name and progress beside each bar, sequenced stops,
// a baseline for actual or elapsed time, and a Now marker.

interface GanttRow {
  RouteID: string;
  RouteName: string;
  StartDate: Date;
  EndDate: Date;
  Duration: number;
  Progress: number;
  CssClass: string;
  Status: string;
  Driver: string;
  Detail: string;
  Predecessor?: string;
  BaselineStart?: Date;
  BaselineEnd?: Date;
  BaselineDuration?: number;
  BaselineNote?: string;
  subtasks?: GanttRow[];
}

const taskFields: TaskFieldsModel = {
  id: 'RouteID', name: 'RouteName', startDate: 'StartDate', endDate: 'EndDate',
  duration: 'Duration', progress: 'Progress', cssClass: 'CssClass',
  dependency: 'Predecessor', child: 'subtasks',
  baselineStartDate: 'BaselineStart', baselineEndDate: 'BaselineEnd', baselineDuration: 'BaselineDuration',
};

const timelineSettings: TimelineSettingsModel = {
  timelineViewMode: 'Hour',
  topTier: { unit: 'Day', format: 'EEE, MMM dd' },
  bottomTier: { unit: 'Hour', format: 'h a' },
  timelineUnitSize: 56,
};

// Right-label text is read from a grid column. A custom template string
// renders blank, and a function template never receives the row fields.
const labelSettings: LabelSettingsModel = {
  rightLabel: 'Status',
};

const NY_TIME: Intl.DateTimeFormatOptions = {
  timeZone: 'America/New_York', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit',
};

function formatInstant(value: Date): string {
  return new Intl.DateTimeFormat('en-US', NY_TIME).format(value);
}

function asDate(value: unknown): Date | null {
  if (value instanceof Date && !Number.isNaN(value.getTime())) return value;
  if (typeof value === 'string' || typeof value === 'number') {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
  }
  return null;
}

function rowFromTooltip(props: Record<string, unknown>): Partial<GanttRow> {
  const nested = props.taskData;
  if (nested && typeof nested === 'object') return nested as Partial<GanttRow>;
  return props as Partial<GanttRow>;
}

function taskbarTooltip(props: Record<string, unknown>) {
  const row = rowFromTooltip(props);
  const start = asDate(row.StartDate);
  const end = asDate(row.EndDate);
  return <div className="gantt-tip">
    <strong>{row.RouteName}</strong>
    <p>{row.Status}{row.Driver ? ` · ${row.Driver}` : ''}</p>
    {start && end ? <p>{formatInstant(start)} – {formatInstant(end)} ET</p> : null}
    {row.Detail ? <p>{row.Detail}</p> : null}
  </div>;
}

function baselineTooltip(props: Record<string, unknown>) {
  const row = rowFromTooltip(props);
  const start = asDate(row.BaselineStart);
  const end = asDate(row.BaselineEnd);
  return <div className="gantt-tip">
    <strong>{row.BaselineNote ?? 'Actual or elapsed'}</strong>
    {start && end ? <p>{formatInstant(start)} – {formatInstant(end)} ET</p> : null}
  </div>;
}

function connectorTooltip() {
  return <div className="gantt-tip"><strong>Stop sequence</strong><p>The next stop starts when the previous one finishes.</p></div>;
}

const tooltipSettings: TooltipSettingsModel = {
  showTooltip: true,
  taskbar: taskbarTooltip,
  baseline: baselineTooltip,
  connectorLine: connectorTooltip,
};

const FULL_DAY = [{ from: 0, to: 24 }];
const ALL_WEEK = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

function parseInstant(value: string): Date {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? new Date(0) : date;
}

function hoursBetween(start: Date, end: Date): number {
  const hours = (end.getTime() - start.getTime()) / 3_600_000;
  if (!Number.isFinite(hours) || hours <= 0) return 0.25;
  return Math.max(0.25, Math.round(hours * 10) / 10);
}

function slaTone(state: string | undefined): Tone {
  switch (state) {
    case 'DeliveredOnTime': return 'success';
    case 'DeliveredLate':
    case 'Breached': return 'danger';
    case 'AtRisk': return 'warning';
    default: return 'info';
  }
}

function routeStatusLabel(status: string, delivered: number, stops: number): string {
  const name = status === 'InProgress' ? 'In progress' : status;
  return stops ? `${name} · ${delivered}/${stops}` : name;
}

function chartSla(state: string | undefined): string {
  switch (state) {
    case 'DeliveredOnTime': return 'On time';
    case 'DeliveredLate': return 'Late';
    case 'AtRisk': return 'At risk';
    case 'Breached': return 'Breached';
    case 'OnTrack': return 'On track';
    default: return 'Open';
  }
}

function slaLabel(state: string | undefined): string {
  switch (state) {
    case 'DeliveredOnTime': return 'Delivered on time';
    case 'DeliveredLate': return 'Delivered late';
    case 'AtRisk': return 'At risk';
    case 'Breached': return 'Breached';
    case 'OnTrack': return 'On track';
    default: return 'No shipment';
  }
}

export function DeliveryTimeline() {
  const [routes, setRoutes] = useState<RouteDto[] | null>(null);
  const [drivers, setDrivers] = useState<DriverDto[] | null>(null);
  const [shipments, setShipments] = useState<ShipmentDto[] | null>(null);
  const [clock, setClock] = useState<Date | null>(null);
  const [error, setError] = useState<string | null>(null);
  const ganttRef = useRef<GanttComponent>(null);
  const scrolledToNow = useRef(false);

  const load = () => {
    setError(null);
    Promise.all([api.getRoutes(), api.getDrivers(), api.getShipments({ page: 1, pageSize: 100 }), api.getMeta()])
      .then(([r, d, s, meta]) => {
        setRoutes(r);
        setDrivers(d);
        setShipments(s.items);
        setClock(parseInstant(meta.scenarioClockReference));
      })
      .catch(err => setError(err instanceof ApiError ? err.message : 'Unable to reach the fleet API.'));
  };
  useEffect(load, []);

  const rows: GanttRow[] = useMemo(() => {
    if (!routes || !drivers || !shipments || !clock) return [];
    const driverById = new Map(drivers.map(d => [d.id, d]));
    const shipmentById = new Map(shipments.map(s => [s.id, s]));
    let nextId = 1;
    const ordered = [...routes].sort((a, b) => parseInstant(a.plannedStart).getTime() - parseInstant(b.plannedStart).getTime());
    return ordered.map(route => {
      const start = parseInstant(route.plannedStart);
      const end = parseInstant(route.plannedEnd);
      const finish = end.getTime() > start.getTime() ? end : new Date(start.getTime() + 3_600_000);
      const driver = driverById.get(route.driverId)?.name ?? 'Unassigned';
      const stops = [...route.stops].sort((a, b) => a.sequence - b.sequence);
      const span = finish.getTime() - start.getTime();
      let previousStopId = '';
      let delivered = 0;
      const subtasks: GanttRow[] = stops.map((stop, index) => {
        const shipment = stop.shipmentId ? shipmentById.get(stop.shipmentId) : undefined;
        if (shipment?.actualDeliveredAt) delivered += 1;
        const sliceStart = new Date(start.getTime() + (index / Math.max(stops.length, 1)) * span);
        const sliceEnd = new Date(start.getTime() + ((index + 1) / Math.max(stops.length, 1)) * span);
        const current = clock.getTime() >= sliceStart.getTime() && clock.getTime() < sliceEnd.getTime();
        const id = String(nextId++);
        const predecessor = previousStopId ? `${previousStopId}FS` : undefined;
        previousStopId = id;
        // const label = slaLabel(shipment?.slaState);
        const customer = shipment?.customerLabel ?? 'Open capacity';
        const actual = shipment?.actualDeliveredAt ? parseInstant(shipment.actualDeliveredAt) : null;
        const eta = shipment?.plannedEta ? parseInstant(shipment.plannedEta) : null;
        return {
          RouteID: id,
          RouteName: `Stop ${stop.sequence} · ${customer}`,
          StartDate: sliceStart,
          EndDate: sliceEnd,
          Duration: hoursBetween(sliceStart, sliceEnd),
          Progress: actual ? 100 : current ? 55 : 0,
          CssClass: `route-tone-${shipment ? slaTone(shipment.slaState) : routeTone(route.status)}`,
          Status: chartSla(shipment?.slaState),
          Driver: driver,
          Detail: [
            slaLabel(shipment?.slaState) + '.',
            'Shown in stop order across the planned window.',
            eta ? `Planned ETA ${formatInstant(eta)} ET.` : '',
            actual ? `Delivered ${formatInstant(actual)} ET.` : '',
          ].filter(Boolean).join(' '),
          Predecessor: predecessor,
        };
      });
      let progress = stops.length === 0
        ? (route.status === 'Completed' ? 100 : route.status === 'InProgress' || route.status === 'Delayed' ? 40 : 0)
        : Math.round((delivered / stops.length) * 100);
      if (delivered === 0 && (route.status === 'InProgress' || route.status === 'Delayed') && span > 0) {
        progress = Math.round(Math.min(1, Math.max(0, (clock.getTime() - start.getTime()) / span)) * 100);
      }
      const latestActual = stops.reduce<Date | null>((latest, stop) => {
        const shipment = stop.shipmentId ? shipmentById.get(stop.shipmentId) : undefined;
        if (!shipment?.actualDeliveredAt) return latest;
        const at = parseInstant(shipment.actualDeliveredAt);
        return !latest || at.getTime() > latest.getTime() ? at : latest;
      }, null);
      let baseline: Pick<GanttRow, 'BaselineStart' | 'BaselineEnd' | 'BaselineDuration' | 'BaselineNote'> | undefined;
      if (latestActual && latestActual.getTime() > start.getTime()) {
        baseline = {
          BaselineStart: start, BaselineEnd: latestActual, BaselineDuration: hoursBetween(start, latestActual),
          BaselineNote: 'Last delivery on this route',
        };
      } else if ((route.status === 'InProgress' || route.status === 'Delayed') && clock.getTime() > start.getTime()) {
        baseline = {
          BaselineStart: start, BaselineEnd: clock, BaselineDuration: hoursBetween(start, clock),
          BaselineNote: clock.getTime() > finish.getTime() ? 'Still running past the planned end' : 'Elapsed through now',
        };
      }
      const id = String(nextId++);
      return {
        RouteID: id,
        RouteName: route.label,
        StartDate: start,
        EndDate: finish,
        Duration: hoursBetween(start, finish),
        Progress: progress,
        CssClass: `route-tone-${routeTone(route.status)}`,
        Status: routeStatusLabel(route.status, delivered, stops.length),
        Driver: driver,
        Detail: `${stops.length} stop${stops.length === 1 ? '' : 's'} with ${driver}. The bar is the planned window.`,
        BaselineStart: baseline?.BaselineStart,
        BaselineEnd: baseline?.BaselineEnd,
        BaselineDuration: baseline?.BaselineDuration,
        BaselineNote: baseline?.BaselineNote,
        subtasks: subtasks.length ? subtasks : undefined,
      };
    });
  }, [routes, drivers, shipments, clock]);

  const projectRange = useMemo(() => {
    if (rows.length === 0) return null;
    const instants = rows.flatMap(row => [
      row.StartDate.getTime(), row.EndDate.getTime(),
      row.BaselineStart?.getTime(), row.BaselineEnd?.getTime(),
      ...(row.subtasks ?? []).flatMap(stop => [stop.StartDate.getTime(), stop.EndDate.getTime()]),
    ]).filter((value): value is number => value !== undefined);
    if (clock) instants.push(clock.getTime());
    const pad = 3 * 3_600_000;
    return { start: new Date(Math.min(...instants) - pad), end: new Date(Math.max(...instants) + pad) };
  }, [rows, clock]);

  if (error) return <div className="notice error" role="alert">
    {error}
    <div className="form-actions" style={{ marginTop: 12 }}><ButtonComponent cssClass="action-button" onClick={load}><RotateCcw size={14} aria-hidden="true" /> Retry</ButtonComponent></div>
  </div>;

  if (!routes || !drivers || !shipments || !clock) return <div className="skeleton" style={{ height: 560 }} />;

  if (rows.length === 0 || !projectRange) return <div className="empty-state"><h2>No routes scheduled for this window</h2></div>;

  return <>
    <div className="notice info"><Info size={14} aria-hidden="true" style={{ verticalAlign: '-2px', marginRight: 6 }} />
      Read-only. Each route bar is the planned window. Stops underneath run in order, the thin bar is actual or elapsed time, and the vertical line is now.
    </div>
    <section className="panel control-panel" aria-label="Delivery timeline">
      <ul className="gantt-legend">
        <li><span className="gantt-swatch" data-tone="info" /> Planned or on track</li>
        <li><span className="gantt-swatch" data-tone="warning" /> At risk</li>
        <li><span className="gantt-swatch" data-tone="danger" /> Delayed or breached</li>
        <li><span className="gantt-swatch" data-tone="success" /> Completed on time</li>
        <li><span className="gantt-swatch gantt-swatch-actual" /> Actual or elapsed</li>
        <li><span className="gantt-swatch gantt-swatch-now" /> Now</li>
      </ul>
      <GanttComponent ref={ganttRef} dataSource={rows} taskFields={taskFields} height="640px" gridLines="Both" allowSorting
        durationUnit="Hour" taskMode="Manual" includeWeekend
        workWeek={ALL_WEEK} dayWorkingTime={FULL_DAY}
        timelineSettings={timelineSettings} labelSettings={labelSettings} tooltipSettings={tooltipSettings}
        eventMarkers={[{ day: clock, label: 'Now', cssClass: 'gantt-now-marker' }]}
        enablePredecessorValidation={false} connectorLineWidth={1}
        rowHeight={46} taskbarHeight={16} treeColumnIndex={0}
        toolbar={['ExpandAll', 'CollapseAll', 'ZoomIn', 'ZoomOut', 'ZoomToFit']}
        projectStartDate={projectRange.start} projectEndDate={projectRange.end}
        splitterSettings={{ columnIndex: 3 }}
        dataBound={() => {
          if (scrolledToNow.current) return;
          scrolledToNow.current = true;
          // Opening on the clock hour hides routes that ran earlier. Fit the
          // whole planned window so every bar, link, and the Now line are in view.
          ganttRef.current?.fitToProject();
        }}>
        <ColumnsDirective>
          <ColumnDirective field="RouteName" headerText="Route" width="180" clipMode="EllipsisWithTooltip" />
          <ColumnDirective field="Driver" headerText="Driver" width="130" clipMode="EllipsisWithTooltip" />
          <ColumnDirective field="Status" headerText="Status" width="140" clipMode="EllipsisWithTooltip" />
        </ColumnsDirective>
        <Inject services={[Selection, Sort, Toolbar, DayMarkers]} />
      </GanttComponent>
    </section>
    <p className="page-footnote">{rows.length} routes. Expand a route to see its stops in finish-to-start order. Hover a bar for the driver, window, and shipment ETA. Timezone: America/New_York.</p>
  </>;
}
