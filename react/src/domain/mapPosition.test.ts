import { test } from 'node:test';
import assert from 'node:assert/strict';
import { deriveVehiclePosition } from './mapPosition.ts';

test('returns null for a route with no stops', () => {
  assert.equal(deriveVehiclePosition({ plannedStart: '2026-09-22T05:00:00Z', plannedEnd: '2026-09-22T09:00:00Z', stops: [], now: '2026-09-22T06:00:00Z' }), null);
});

test('returns the single stop when only one exists', () => {
  const stop = { lat: 39.7, lng: -104.9 };
  const result = deriveVehiclePosition({ plannedStart: '2026-09-22T05:00:00Z', plannedEnd: '2026-09-22T09:00:00Z', stops: [stop], now: '2026-09-22T06:00:00Z' });
  assert.deepEqual(result, stop);
});

test('returns the first stop before the route starts', () => {
  const stops = [{ lat: 39.6, lng: -104.9 }, { lat: 39.7, lng: -104.8 }];
  const result = deriveVehiclePosition({ plannedStart: '2026-09-22T05:00:00Z', plannedEnd: '2026-09-22T09:00:00Z', stops, now: '2026-09-22T04:00:00Z' });
  assert.deepEqual(result, stops[0]);
});

test('returns the last stop once the route has finished', () => {
  const stops = [{ lat: 39.6, lng: -104.9 }, { lat: 39.7, lng: -104.8 }];
  const result = deriveVehiclePosition({ plannedStart: '2026-09-22T05:00:00Z', plannedEnd: '2026-09-22T09:00:00Z', stops, now: '2026-09-22T10:00:00Z' });
  assert.deepEqual(result, stops[1]);
});

test('interpolates the midpoint at the halfway mark between two stops', () => {
  const stops = [{ lat: 39.0, lng: -105.0 }, { lat: 40.0, lng: -104.0 }];
  const result = deriveVehiclePosition({ plannedStart: '2026-09-22T05:00:00Z', plannedEnd: '2026-09-22T09:00:00Z', stops, now: '2026-09-22T07:00:00Z' });
  assert.ok(result);
  assert.equal(result.lat, 39.5);
  assert.equal(result.lng, -104.5);
});

test('distributes progress across multiple legs', () => {
  const stops = [{ lat: 0, lng: 0 }, { lat: 10, lng: 0 }, { lat: 20, lng: 0 }];
  // 25% of the way through the window, with 2 legs, lands a quarter of the way along leg 1.
  const result = deriveVehiclePosition({ plannedStart: '2026-09-22T00:00:00Z', plannedEnd: '2026-09-22T04:00:00Z', stops, now: '2026-09-22T01:00:00Z' });
  assert.ok(result);
  assert.equal(result.lat, 5);
});

test('falls back to the first stop when the planned window is invalid', () => {
  const stops = [{ lat: 1, lng: 1 }, { lat: 2, lng: 2 }];
  const result = deriveVehiclePosition({ plannedStart: 'not-a-date', plannedEnd: '2026-09-22T09:00:00Z', stops, now: '2026-09-22T06:00:00Z' });
  assert.deepEqual(result, stops[0]);
});
