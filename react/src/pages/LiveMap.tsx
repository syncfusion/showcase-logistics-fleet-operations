import { useEffect, useMemo, useState, type ReactElement } from 'react';
import { RotateCcw, MapPin } from 'lucide-react';
import { DropDownListComponent } from '@syncfusion/ej2-react-dropdowns';
import { ButtonComponent } from '@syncfusion/ej2-react-buttons';
import { ListViewComponent } from '@syncfusion/ej2-react-lists';
import {
  MapsComponent, LayersDirective, LayerDirective, MarkersDirective, MarkerDirective,
  Inject, Marker, MapsTooltip, Zoom,
} from '@syncfusion/ej2-react-maps';
import { api, ApiError } from '../api/client.ts';
import type { DepotDto, DriverDto, RouteDto, RouteStatus, VehicleDto } from '../api/types.ts';
import { deriveVehiclePosition } from '../domain/mapPosition.ts';
import { routeTone, type Tone } from '../domain/status.ts';

// ej2-maps' LayerPanel (layer-panel.js) does a literal
// `.replace('level', ...).replace('tileX', ...).replace('tileY', ...)` on
// the template string, so the OpenStreetMap tile URL must use the literal
// words `level`/`tileX`/`tileY` rather than the standard {z}/{x}/{y} token
// syntax. Using {z}/{x}/{y} here silently produced an un-substituted,
// percent-encoded URL with no tile requests.
const OSM_TILE_URL = 'https://tile.openstreetmap.org/level/tileX/tileY.png';

interface VehicleMarker {
  id: string;
  latitude: number;
  longitude: number;
  routeLabel: string;
  driverName: string;
  vehicleLabel: string;
  status: RouteStatus;
}

const STATUS_OPTIONS = ['All', 'Planned', 'InProgress', 'Delayed', 'Completed'];
const ROUTE_LIST_FIELDS = { id: 'id', text: 'label' };

interface RouteListItem {
  id: string;
  label: string;
  status: RouteStatus;
  tone: Tone;
  driverName: string;
  vehicleLabel: string;
  stopCount: number;
}

function routeItemTemplate(item: RouteListItem): ReactElement {
  return (
    <div className="route-list-item">
      <h3>{item.label} <span className="status-badge" data-tone={item.tone}>{item.status}</span></h3>
      <p>Driver: {item.driverName}</p>
      <p>Vehicle: {item.vehicleLabel}</p>
      <p>{item.stopCount} stop(s)</p>
    </div>
  );
}

export function LiveMap() {
  const [routes, setRoutes] = useState<RouteDto[] | null>(null);
  const [depots, setDepots] = useState<DepotDto[] | null>(null);
  const [drivers, setDrivers] = useState<DriverDto[] | null>(null);
  const [vehicles, setVehicles] = useState<VehicleDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [mapError, setMapError] = useState(false);
  const [statusFilter, setStatusFilter] = useState('All');

  const load = () => {
    setError(null);
    Promise.all([api.getRoutes(), api.getDepots(), api.getDrivers(), api.getVehicles()])
      .then(([r, d, dr, v]) => { setRoutes(r); setDepots(d); setDrivers(dr); setVehicles(v); })
      .catch(err => setError(err instanceof ApiError ? err.message : 'Unable to reach the fleet API.'));
  };
  useEffect(load, []);

  // Real (if simple) signal for the disclosed "base map layer fails to
  // load" fallback: OSM tiles require network access, so offline is a
  // genuine failure mode for the tile layer, not a simulated one.
  useEffect(() => {
    const goOffline = () => setMapError(true);
    const goOnline = () => setMapError(false);
    setMapError(!navigator.onLine);
    window.addEventListener('offline', goOffline);
    window.addEventListener('online', goOnline);
    return () => { window.removeEventListener('offline', goOffline); window.removeEventListener('online', goOnline); };
  }, []);

  const filteredRoutes = useMemo(() => {
    if (!routes) return [];
    return statusFilter === 'All' ? routes : routes.filter(r => r.status === statusFilter);
  }, [routes, statusFilter]);

  const vehicleMarkers: VehicleMarker[] = useMemo(() => {
    if (!routes || !drivers || !vehicles) return [];
    const driverById = new Map(drivers.map(d => [d.id, d]));
    const vehicleById = new Map(vehicles.map(v => [v.id, v]));
    const now = '2026-09-22T16:45:00Z'; // scenario clock reference (see GET /api/meta)
    return filteredRoutes
      .filter(route => route.status === 'InProgress' || route.status === 'Delayed')
      .map(route => {
        const point = deriveVehiclePosition({
          plannedStart: route.plannedStart,
          plannedEnd: route.plannedEnd,
          stops: route.stops.map(s => ({ lat: s.lat, lng: s.lng })),
          now,
        });
        if (!point) return null;
        return {
          id: route.id,
          latitude: point.lat,
          longitude: point.lng,
          routeLabel: route.label,
          driverName: driverById.get(route.driverId)?.name ?? 'Unassigned',
          vehicleLabel: vehicleById.get(route.vehicleId)?.label ?? 'Unknown vehicle',
          status: route.status,
        };
      })
      .filter((m): m is VehicleMarker => m !== null);
  }, [routes, drivers, vehicles, filteredRoutes]);

  const depotMarkers = useMemo(() => (depots ?? []).map(d => ({ latitude: d.lat, longitude: d.lng, name: d.name })), [depots]);

  const routeItems = useMemo<RouteListItem[]>(() => {
    if (!drivers || !vehicles) return [];
    const driverById = new Map(drivers.map(driver => [driver.id, driver]));
    const vehicleById = new Map(vehicles.map(vehicle => [vehicle.id, vehicle]));
    return filteredRoutes.map(route => ({
      id: route.id,
      label: route.label,
      status: route.status,
      tone: routeTone(route.status),
      driverName: driverById.get(route.driverId)?.name ?? 'Unassigned',
      vehicleLabel: vehicleById.get(route.vehicleId)?.label ?? 'Unknown',
      stopCount: route.stops.length,
    }));
  }, [filteredRoutes, drivers, vehicles]);

  const center = depots && depots.length > 0
    ? { latitude: depots.reduce((s, d) => s + d.lat, 0) / depots.length, longitude: depots.reduce((s, d) => s + d.lng, 0) / depots.length }
    : { latitude: 39.7, longitude: -104.9 };

  if (error) return <div className="notice error" role="alert">
    {error}
    <div className="form-actions" style={{ marginTop: 12 }}><ButtonComponent cssClass="action-button" onClick={load}><RotateCcw size={14} aria-hidden="true" /> Retry</ButtonComponent></div>
  </div>;

  if (!routes || !depots || !drivers || !vehicles) return <div className="skeleton" style={{ height: 520 }} />;

  if (filteredRoutes.length === 0) return <div className="empty-state"><MapPin size={40} aria-hidden="true" /><h2>No active routes</h2><p>No routes match the selected status filter.</p></div>;

  return <>
    <div className="toolbar" style={{ border: 'none', padding: '0 0 var(--lg)' }}>
      <div className="field">
        <label htmlFor="route-status-filter">Route status</label>
        <DropDownListComponent id="route-status-filter" dataSource={STATUS_OPTIONS} value={statusFilter} change={(e: { value: string }) => setStatusFilter(e.value)} />
      </div>
      <span className="toolbar-note">{filteredRoutes.length} route(s) · {vehicleMarkers.length} vehicle marker(s) in progress</span>
    </div>

    <div className="map-layout">
      <section className="panel" aria-label="Live map">
        {mapError
          ? <div className="empty-state"><h2>Map view unavailable</h2><p>Falling back to the route list on the right — the base map layer failed to load.</p></div>
          : <div style={{ padding: 16 }}>
              <MapsComponent id="fleet-map" height="560px" background="transparent"
                zoomSettings={{ enable: true, zoomFactor: 11 }}
                centerPosition={center}>
                <Inject services={[Marker, MapsTooltip, Zoom]} />
                <LayersDirective>
                  <LayerDirective urlTemplate={OSM_TILE_URL}>
                    <MarkersDirective>
                      <MarkerDirective
                        visible dataSource={depotMarkers} shape="Balloon" fill="var(--chart-series-secondary, #027A48)"
                        height={24} width={24}
                        tooltipSettings={{ visible: true, valuePath: 'name' }}
                      />
                      <MarkerDirective
                        visible dataSource={vehicleMarkers} shape="Circle"
                        fill={vehicleMarkers.some(m => m.status === 'Delayed') ? '#B42318' : '#175CD3'}
                        height={16} width={16}
                        tooltipSettings={{ visible: true, format: '${routeLabel}<br/>Driver: ${driverName}<br/>Vehicle: ${vehicleLabel}<br/>Status: ${status}' }}
                      />
                    </MarkersDirective>
                  </LayerDirective>
                </LayersDirective>
              </MapsComponent>
            </div>}
      </section>

      <aside className="panel" aria-labelledby="route-list-heading">
        <div className="panel-heading"><div><h2 id="route-list-heading">Routes</h2><p>Keyboard-reachable list — also the narrow-viewport view</p></div></div>
        <ListViewComponent
          id="route-list"
          cssClass="route-list"
          dataSource={routeItems as unknown as { [key: string]: Object }[]}
          fields={ROUTE_LIST_FIELDS}
          template={routeItemTemplate}
          height="auto"
          width="100%"
          htmlAttributes={{ 'aria-labelledby': 'route-list-heading' }}
        />
      </aside>
    </div>
    <p className="page-footnote">Vehicle positions are synthetic, derived from each in-progress route's planned time window and stop sequence — not live GPS. Excluded per intake constraints.</p>
  </>;
}
