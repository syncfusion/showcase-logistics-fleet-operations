export type Theme = 'light' | 'dark';
export function initialTheme(): Theme {
  try { return localStorage.getItem('fleet-ops-theme') === 'dark' ? 'dark' : 'light'; } catch { return 'light'; }
}
function loadStylesheet(id: string, href: string): Promise<HTMLLinkElement> {
  return new Promise((resolve, reject) => {
    const link = document.createElement('link');
    link.id = id; link.rel = 'stylesheet'; link.href = href;
    link.onload = () => resolve(link);
    link.onerror = () => reject(new Error(`Failed to load stylesheet: ${href}`));
    document.head.append(link);
  });
}

export async function applyTheme(theme: Theme): Promise<void> {
  // The all-in-one tailwind3(-dark).css bundle has zero `.e-pager`,
  // `.e-breadcrumb`, `.e-listview`, and `.e-sidebar` rules. Grid's pager,
  // Breadcrumb, ListView, and Sidebar each need their own stylesheet, same
  // gap as Dashboard Layout's dashboard-layout/index.css import in Overview.tsx.
  // In light mode an unstyled control can look plausible; it breaks once the
  // shell is dark. Sidebar also loses its open/close positioning without it.
  const [themeAsset, pagerAsset, breadcrumbAsset, listViewAsset, sidebarAsset] = await Promise.all([
    theme === 'dark'
      ? import('@syncfusion/ej2-tailwind3-dark-theme/styles/tailwind3-dark.css?url')
      : import('@syncfusion/ej2-tailwind3-theme/styles/tailwind3.css?url'),
    theme === 'dark'
      ? import('@syncfusion/ej2-tailwind3-dark-theme/styles/pager/index.css?url')
      : import('@syncfusion/ej2-tailwind3-theme/styles/pager/index.css?url'),
    theme === 'dark'
      ? import('@syncfusion/ej2-tailwind3-dark-theme/styles/breadcrumb/index.css?url')
      : import('@syncfusion/ej2-tailwind3-theme/styles/breadcrumb/index.css?url'),
    theme === 'dark'
      ? import('@syncfusion/ej2-tailwind3-dark-theme/styles/list-view/index.css?url')
      : import('@syncfusion/ej2-tailwind3-theme/styles/list-view/index.css?url'),
    theme === 'dark'
      ? import('@syncfusion/ej2-tailwind3-dark-theme/styles/sidebar/index.css?url')
      : import('@syncfusion/ej2-tailwind3-theme/styles/sidebar/index.css?url'),
  ]);

  // Fully load BOTH new stylesheets off-screen (appended, not yet replacing
  // anything) before touching the DOM the mounted Kanban/Grid/Dashboard
  // Layout components are already reading from. Swapping the live stylesheet
  // out from under an already-mounted Syncfusion component (even for a
  // single frame, between removing the old link and the new one finishing
  // its network fetch) was found to leave Kanban's layout permanently
  // broken after a live theme toggle -- it only measures its column
  // geometry once and never re-measures, so a mid-swap CSS gap corrupts it
  // for the rest of that mount, even though the CSS itself resolves fine a
  // moment later. Loading fully first, then swapping both old links out in
  // one synchronous pass, removes that gap entirely.
  const [newLink, newPagerLink, newBreadcrumbLink, newListViewLink, newSidebarLink] = await Promise.all([
    loadStylesheet('syncfusion-theme-next', themeAsset.default),
    loadStylesheet('syncfusion-pager-theme-next', pagerAsset.default),
    loadStylesheet('syncfusion-breadcrumb-theme-next', breadcrumbAsset.default),
    loadStylesheet('syncfusion-listview-theme-next', listViewAsset.default),
    loadStylesheet('syncfusion-sidebar-theme-next', sidebarAsset.default),
  ]);

  const current = document.getElementById('syncfusion-theme');
  const currentPager = document.getElementById('syncfusion-pager-theme');
  const currentBreadcrumb = document.getElementById('syncfusion-breadcrumb-theme');
  const currentListView = document.getElementById('syncfusion-listview-theme');
  const currentSidebar = document.getElementById('syncfusion-sidebar-theme');
  newLink.id = 'syncfusion-theme';
  newPagerLink.id = 'syncfusion-pager-theme';
  newBreadcrumbLink.id = 'syncfusion-breadcrumb-theme';
  newListViewLink.id = 'syncfusion-listview-theme';
  newSidebarLink.id = 'syncfusion-sidebar-theme';
  current?.remove();
  currentPager?.remove();
  currentBreadcrumb?.remove();
  currentListView?.remove();
  currentSidebar?.remove();

  document.documentElement.dataset.theme = theme;
  try { localStorage.setItem('fleet-ops-theme', theme); } catch { /* Theme persistence is optional. */ }

  // Belt-and-suspenders: nudge any mounted component that measures its own
  // layout via a resize listener (Kanban/Dashboard Layout/Gantt all do) to
  // recompute now that the final CSS is in place, in case anything latched
  // onto a transient state during the swap above.
  window.dispatchEvent(new Event('resize'));
}
