// Tiny cross-page store for the public/session profile's client-side-only
// exception status overrides. Per contracts/data-contract.md: "Public
// visitors ... Any interaction the UI exposes ... mutates only an in-memory
// client-side session copy of the loaded data — no request is sent to a
// write endpoint." This lets the Exceptions page's simulated drag/move
// affect the Overview page's Active Exceptions KPI within the same tab,
// without persisting anything or calling the API. Reload clears it (module
// state, not localStorage/sessionStorage) — matching "reload discards the
// session overlay and re-fetches the real baseline."
import { useEffect, useState } from 'react';

type Listener = () => void;
const overrides = new Map<string, string>(); // exceptionId -> simulated new status
const listeners = new Set<Listener>();

function notify() { listeners.forEach(l => l()); }

export function setSessionOverride(id: string, status: string): void {
  overrides.set(id, status);
  notify();
}

export function getSessionOverrides(): ReadonlyMap<string, string> {
  return overrides;
}

export function clearSessionOverrides(): void {
  overrides.clear();
  notify();
}

/** Net change to "active" (Open/Investigating/Escalated) exception count from session-only overrides. */
export function useActiveExceptionDelta(): number {
  const [, setTick] = useState(0);
  useEffect(() => {
    const listener = () => setTick(t => t + 1);
    listeners.add(listener);
    return () => { listeners.delete(listener); };
  }, []);
  let delta = 0;
  for (const status of overrides.values()) {
    if (status === 'Resolved') delta -= 1;
  }
  return delta;
}

export function useSessionOverrides(): ReadonlyMap<string, string> {
  // Returns a fresh Map copy on every notified change — the module-level
  // `overrides` Map is mutated in place, so returning it directly would
  // keep the same object reference across re-renders and silently defeat
  // any useMemo/useEffect that depends on it for change detection.
  const [snapshot, setSnapshot] = useState<ReadonlyMap<string, string>>(() => new Map(overrides));
  useEffect(() => {
    const listener = () => setSnapshot(new Map(overrides));
    listeners.add(listener);
    return () => { listeners.delete(listener); };
  }, []);
  return snapshot;
}
