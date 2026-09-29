// Domain-specific status-tone mapping, per design/DESIGN.md: danger =
// SLA-breached/escalated exception/delayed route, warning =
// SLA-at-risk/investigating exception, success = on-time/resolved/delivered,
// info = planned/in-progress/scheduled.
export type Tone = 'success' | 'info' | 'warning' | 'danger' | 'neutral';

export function routeTone(status: string): Tone {
  switch (status) {
    case 'Delayed': return 'danger';
    case 'InProgress': return 'info';
    case 'Planned': return 'info';
    case 'Completed': return 'success';
    default: return 'neutral';
  }
}

export function exceptionTone(status: string): Tone {
  switch (status) {
    case 'Escalated': return 'danger';
    case 'Investigating': return 'warning';
    case 'Resolved': return 'success';
    case 'Open': return 'warning';
    default: return 'neutral';
  }
}

export function severityTone(severity: string): Tone {
  switch (severity) {
    case 'High': return 'danger';
    case 'Medium': return 'warning';
    case 'Low': return 'info';
    default: return 'neutral';
  }
}

export function healthTone(status: string): Tone {
  switch (status) {
    case 'Healthy': return 'success';
    case 'Degraded': return 'warning';
    case 'Unhealthy': return 'danger';
    default: return 'neutral';
  }
}

export function scheduleTone(status: string): Tone {
  switch (status) {
    case 'Conflict': return 'danger';
    case 'Confirmed': return 'success';
    case 'Scheduled': return 'info';
    default: return 'neutral';
  }
}

const EXCEPTION_ORDER: Record<string, number> = { Open: 0, Investigating: 1, Resolved: 2, Escalated: 3 };

/** Open -> Resolved is not allowed (must pass through Investigating); every other forward/side move is. */
export function isValidExceptionTransition(from: string, to: string): boolean {
  if (from === to) return false;
  if (from === 'Open' && to === 'Resolved') return false;
  if (from === 'Resolved') return false; // resolved is terminal in this simplified triage flow
  return EXCEPTION_ORDER[to] !== undefined;
}
