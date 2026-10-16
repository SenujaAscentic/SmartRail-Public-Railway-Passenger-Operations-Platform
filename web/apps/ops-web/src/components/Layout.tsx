import { Outlet } from "react-router";

// The frame of every page: a skip link for keyboard users, a banner, and ONE main landmark that each page fills.
// Each page has exactly one <h1>, so a screen reader announces where you are.
export function Layout() {
  return (
    <>
      <a className="skip-link" href="#main">
        Skip to main content
      </a>
      <header className="app-header">
        <span className="app-name">SmartRail Operations</span>
      </header>
      <main id="main" tabIndex={-1}>
        <Outlet />
      </main>
    </>
  );
}
