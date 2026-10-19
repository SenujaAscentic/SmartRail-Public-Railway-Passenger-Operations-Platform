import { NavLink } from "react-router";
import { useProfile } from "../session/useProfile.ts";
import { useSessionState } from "../services-context.tsx";

// The main places of the ops web. The audit log is the controller's; the raw live events are a technical view for developers.
export function Nav() {
  const state = useSessionState();
  const profile = useProfile();
  if (state.status !== "signedIn") return null;

  return (
    <nav aria-label="Main">
      <ul className="nav">
        <li><NavLink to="/" end>Dashboard</NavLink></li>
        {profile?.role === "Controller" && <li><NavLink to="/audit">Audit log</NavLink></li>}
        <li><NavLink to="/live">Live events</NavLink></li>
      </ul>
    </nav>
  );
}
