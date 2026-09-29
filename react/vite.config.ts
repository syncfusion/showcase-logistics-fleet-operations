import { readFileSync } from 'node:fs';
import { defineConfig } from 'vite';
import tailwindcss from '@tailwindcss/vite';

const pkg = JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf8'));

// Served from the web root — Vite's default base '/' makes bundle URLs
// relative (e.g. /assets/index-<hash>.js) which is correct for an app
// mounted at the root of its host, not under a vanity subpath.
export default defineConfig({
  plugins: [tailwindcss()],
  // Exposed to the /health page so it reports real build info, not a
  // hardcoded string.
  define: {
    __APP_VERSION__: JSON.stringify(pkg.version),
    __APP_BUILD_TIME__: JSON.stringify(new Date().toISOString()),
  },
  server: {
    port: 5175,
    strictPort: true,
    // Dev-only convenience: proxy same-origin /api/* to the local ASP.NET
    // Core API so the browser never has to deal with cross-origin requests
    // (the backend does not set CORS headers — it is designed to sit behind
    // a same-origin reverse proxy in every real deployment).
    proxy: {
      '/api': { target: 'http://localhost:5269', changeOrigin: true },
    },
  },
  preview: { port: 4175, strictPort: true, headers: { 'X-Content-Type-Options': 'nosniff', 'Referrer-Policy': 'no-referrer' } },
});
