// Pure, unit-tested logic for deriving a synthetic vehicle marker position
// along an in-progress route's stop sequence. Vehicle markers are positioned
// along each in-progress route's stop sequence (synthetic position derived
// from route progress, not live GPS).

export interface RoutePoint { lat: number; lng: number; }
export interface RouteProgressInput {
  plannedStart: string; // ISO timestamp
  plannedEnd: string; // ISO timestamp
  stops: RoutePoint[]; // ordered by sequence
  now: string; // ISO timestamp, the scenario clock
}

/**
 * Returns a synthetic marker position for an in-progress route by linearly
 * interpolating along the ordered chain of stops (depot origin implicit —
 * the first stop is treated as the start of the visible path) based on how
 * far the route's planned time window has elapsed. Clamped to [0, 1] so a
 * route that hasn't started or has already finished still returns a valid
 * point (first/last stop respectively).
 */
export function deriveVehiclePosition(input: RouteProgressInput): RoutePoint | null {
  const { stops, plannedStart, plannedEnd, now } = input;
  if (stops.length === 0) return null;
  if (stops.length === 1) return stops[0];

  const start = Date.parse(plannedStart);
  const end = Date.parse(plannedEnd);
  const current = Date.parse(now);
  if (!Number.isFinite(start) || !Number.isFinite(end) || !Number.isFinite(current) || end <= start) {
    return stops[0];
  }

  const fraction = Math.min(1, Math.max(0, (current - start) / (end - start)));

  // Distribute progress evenly across the N-1 legs between stops.
  const legCount = stops.length - 1;
  const legProgress = fraction * legCount;
  const legIndex = Math.min(legCount - 1, Math.floor(legProgress));
  const legFraction = legProgress - legIndex;

  const from = stops[legIndex];
  const to = stops[legIndex + 1];
  return {
    lat: from.lat + (to.lat - from.lat) * legFraction,
    lng: from.lng + (to.lng - from.lng) * legFraction,
  };
}
