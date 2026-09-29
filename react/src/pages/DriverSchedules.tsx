import { useEffect, useMemo, useState } from 'react';
import { Info, RotateCcw } from 'lucide-react';
import { ButtonComponent } from '@syncfusion/ej2-react-buttons';
import {
  ScheduleComponent, ResourcesDirective, ResourceDirective, ViewsDirective, ViewDirective,
  Inject, TimelineViews, Day, Week,
} from '@syncfusion/ej2-react-schedule';
import { api, ApiError } from '../api/client.ts';
import type { DriverDto, ScheduleEntryDto } from '../api/types.ts';

// Work day is 11:00–20:00 on every time-based view. Bounded/read-only at
// this prototype stage (driver shifts and schedule entries are display-only).

interface ScheduleEvent {
  Id: string;
  Subject: string;
  StartTime: Date;
  EndTime: Date;
  DriverId: string;
  Status: string;
}

export function DriverSchedules() {
  const [drivers, setDrivers] = useState<DriverDto[] | null>(null);
  const [schedule, setSchedule] = useState<ScheduleEntryDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = () => {
    setError(null);
    Promise.all([api.getDrivers(), api.getSchedule()])
      .then(([d, s]) => { setDrivers(d); setSchedule(s); })
      .catch(err => setError(err instanceof ApiError ? err.message : 'Unable to reach the fleet API.'));
  };
  useEffect(load, []);

  const driverResources = useMemo(() => (drivers ?? []).map(d => ({
    text: d.name, id: d.id, color: d.status === 'OnShift' ? '#175CD3' : d.status === 'OnBreak' ? '#B54708' : '#94A3B8',
  })), [drivers]);

  const events: ScheduleEvent[] = useMemo(() => (schedule ?? []).map(entry => ({
    Id: entry.id,
    Subject: `Shift · ${entry.status}${entry.routeId ? ' · route assigned' : ''}`,
    StartTime: new Date(entry.shiftStart),
    EndTime: new Date(entry.shiftEnd),
    DriverId: entry.driverId,
    Status: entry.status,
  })), [schedule]);

  const selectedDate = useMemo(() => {
    const first = schedule?.[0];
    return first ? new Date(first.shiftStart) : new Date('2026-09-22T00:00:00');
  }, [schedule]);

  if (error) return <div className="notice error" role="alert">
    {error}
    <div className="form-actions" style={{ marginTop: 12 }}><ButtonComponent cssClass="action-button" onClick={load}><RotateCcw size={14} aria-hidden="true" /> Retry</ButtonComponent></div>
  </div>;

  if (!drivers || !schedule) return <div className="skeleton" style={{ height: 560 }} />;

  if (drivers.length === 0) return <div className="empty-state"><h2>No drivers in this scenario</h2></div>;

  return <>
    <div className="notice info"><Info size={14} aria-hidden="true" style={{ verticalAlign: '-2px', marginRight: 6 }} />
      View each driver's shifts and assigned routes for the demo day.
    </div>
    <section className="panel control-panel" aria-label="Driver schedules">
      <ScheduleComponent
        height="600px"
        selectedDate={selectedDate}
        currentView="TimelineWeek"
        readonly
        workHours={{ highlight: true, start: '11:00', end: '20:00' }}
        eventSettings={{ dataSource: events, fields: {
          id: 'Id', subject: { name: 'Subject' }, startTime: { name: 'StartTime' }, endTime: { name: 'EndTime' },
        } }}
        group={{ resources: ['Drivers'] }}
      >
        <ViewsDirective>
          <ViewDirective option="TimelineWeek" startHour="11:00" endHour="20:00" />
          <ViewDirective option="TimelineDay" startHour="11:00" endHour="20:00" />
          <ViewDirective option="Day" startHour="11:00" endHour="20:00" />
          <ViewDirective option="Week" startHour="11:00" endHour="20:00" />
        </ViewsDirective>
        <ResourcesDirective>
          <ResourceDirective field="DriverId" title="Driver" name="Drivers" dataSource={driverResources} textField="text" idField="id" colorField="color" />
        </ResourcesDirective>
        <Inject services={[TimelineViews, Day, Week]} />
      </ScheduleComponent>
    </section>
    <p className="page-footnote">One resource row per driver ({drivers.length} drivers). Timezone: America/New_York.</p>
  </>;
}
