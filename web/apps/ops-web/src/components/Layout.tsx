import { Link, Outlet } from "react-router";
import { useSessionState } from "../services-context.tsx";
import { formatClock } from "../format.ts";
import { useNow } from "../useNow.ts";
import { Icon } from "./Icon.tsx";
import { Nav } from "./Nav.tsx";
import { SessionBar } from "./SessionBar.tsx";

// The frame of every page (design: docs/design/ops). Signed in: a left sidebar (brand, places, who is signed in) and a top bar with
// the time; signed out: the brand and the page, centred. Either way: a skip link, ONE banner, ONE main landmark that each page
// fills, and exactly one <h1> per page, so a screen reader announces where you are.
export function Layout() {
  const state = useSessionState();

  if (state.status !== "signedIn") {
    return (
      <>
        <SkipLink />
        <header className="auth-header">
          <Brand />
        </header>
        <main id="main" tabIndex={-1} className="auth-main">
          <Outlet />
        </main>
      </>
    );
  }

  return (
    <>
      <SkipLink />
      <div className="shell">
        <aside className="sidebar">
          <Brand />
          <Nav />
          <SessionBar />
        </aside>
        <div className="content">
          <header className="topbar">
            <span className="topbar-line">Main Line</span>
            <Clock />
          </header>
          <main id="main" tabIndex={-1}>
            <Outlet />
          </main>
        </div>
      </div>
    </>
  );
}

const SkipLink = () => (
  <a className="skip-link" href="#main">
    Skip to main content
  </a>
);

function Brand() {
  return (
    <Link className="brand" to="/">
      <Icon name="train" />
      <span>SmartRail Ops</span>
    </Link>
  );
}

// The time in Sri Lanka, 24 hours (design). Display only: nothing is decided from this clock.
function Clock() {
  const now = useNow(10_000);
  return <time className="topbar-clock" dateTime={new Date(now).toISOString()}>{formatClock(new Date(now).toISOString())}</time>;
}
