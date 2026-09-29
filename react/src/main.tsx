import { createRoot } from 'react-dom/client';
import { registerLicense } from '@syncfusion/ej2-base';
import { ButtonComponent } from '@syncfusion/ej2-react-buttons';
import { App } from './App.tsx';
import { applyTheme, initialTheme } from './theme.ts';
import './styles.css';

if (import.meta.env.VITE_SYNCFUSION_LICENSE_KEY) registerLicense(import.meta.env.VITE_SYNCFUSION_LICENSE_KEY);

const root = createRoot(document.getElementById('root')!);
root.render(<main className="page-content" aria-busy="true"><p role="status">Loading Logistics Fleet &amp; Delivery Operations Dashboard…</p><div className="skeleton" /></main>);

const theme = initialTheme();
applyTheme(theme)
  .then(() => root.render(<App initialTheme={theme} />))
  .catch(() => root.render(
    <main className="page-content" role="alert">
      <h1>Unable to load application styles</h1><p>Please check the local files and reload.</p>
      <ButtonComponent onClick={() => location.reload()}>Reload</ButtonComponent>
    </main>
  ));
