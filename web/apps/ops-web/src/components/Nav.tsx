import { NavLink } from "react-router";
import { useProfile } from "../session/useProfile.ts";
import { Icon, type IconName } from "./Icon.tsx";

// The places of the ops web, as in the design's sidebar. Messages and Announcements are designed but deferred (re-plan 2), so they
// are not here. The audit log is the controller's; the raw events view is a technical page for developers.
const link = (to: string, icon: IconName, label: string, end = false) => (
  <li key={to}>
    <NavLink to={to} end={end}>
      <Icon name={icon} />
      <span>{label}</span>
    </NavLink>
  </li>
);

export function Nav() {
  const profile = useProfile();

  return (
    <nav aria-label="Main">
      <ul className="nav">
        {link("/", "dashboard", "Dashboard", true)}
        {profile?.role === "Controller" && link("/audit", "history", "Audit log")}
        {link("/live", "pulse", "Live events")}
      </ul>
    </nav>
  );
}
