import { useCallback, useState } from 'react';
import { api, ApiError } from '../api/client.ts';

// Public/session mode: session — unauthenticated visitors hit GET-only
// endpoints; any write interaction is simulated client-side only (never
// reaches the write endpoint). Customer/authenticated mode: signing in with
// the demo account stores a short-lived JWT in sessionStorage (per-tab, not
// shared across browsers/sessions) and subsequent writes call the real,
// authenticated endpoints. See contracts/data-contract.md's "Public and
// customer behavior" section.
const STORAGE_KEY = 'fleet-ops-auth-token';

export interface AuthState {
  token: string | null;
  email: string | null;
  signingIn: boolean;
  error: string | null;
  signIn: (email: string, password: string) => Promise<boolean>;
  signOut: () => void;
}

function readStoredToken(): { token: string; email: string } | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch { return null; }
}

export function useAuth(): AuthState {
  const stored = readStoredToken();
  const [token, setToken] = useState<string | null>(stored?.token ?? null);
  const [email, setEmail] = useState<string | null>(stored?.email ?? null);
  const [signingIn, setSigningIn] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const signIn = useCallback(async (emailInput: string, password: string) => {
    setSigningIn(true);
    setError(null);
    try {
      const result = await api.login(emailInput, password);
      setToken(result.token);
      setEmail(emailInput);
      try { sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ token: result.token, email: emailInput })); } catch { /* storage optional */ }
      return true;
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to sign in. Please try again.');
      return false;
    } finally {
      setSigningIn(false);
    }
  }, []);

  const signOut = useCallback(() => {
    setToken(null);
    setEmail(null);
    try { sessionStorage.removeItem(STORAGE_KEY); } catch { /* storage optional */ }
  }, []);

  return { token, email, signingIn, error, signIn, signOut };
}
