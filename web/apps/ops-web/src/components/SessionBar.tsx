import { useServices, useSessionState } from "../services-context.tsx";
import { useProfile } from "../session/useProfile.ts";

// At the foot of the sidebar, for a signed-in person: who they are (from GET /staff/me), their scope, and the sign-out button.
// The name is shown on screen only; it is never logged or put in a URL.
export function SessionBar() {
  const { session } = useServices();
  const state = useSessionState();
  const profile = useProfile();

  if (state.status !== "signedIn") return null;

  const role = profile?.role === "StationManager" ? "Station manager · own station" : profile?.role === "Controller" ? "Controller · whole line" : profile?.role;

  return (
    <div className="session-bar">
      <p className="session-who">
        <strong>{profile ? profile.name : "Signed in"}</strong>
        {role && <span>{role}</span>}
      </p>
      <button type="button" className="secondary" onClick={() => void session.signOut()}>
        Sign out
      </button>
    </div>
  );
}
