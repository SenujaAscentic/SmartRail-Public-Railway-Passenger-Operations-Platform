import { useEffect, useState } from "react";
import type { components } from "@smartrail/api-client";
import { useServices, useSessionState } from "../services-context.tsx";

type Profile = components["schemas"]["StaffMeResponse"];

// In the header, for a signed-in person: who they are (from GET /staff/me) and the sign-out button.
// The name is shown on screen only; it is never logged or put in a URL.
export function SessionBar() {
  const { api, session } = useServices();
  const state = useSessionState();
  const [profile, setProfile] = useState<Profile | null>(null);

  useEffect(() => {
    if (state.status !== "signedIn") {
      setProfile(null);
      return;
    }
    let cancelled = false;
    api
      .GET("/api/v1/staff/me")
      .then(({ data }) => {
        if (!cancelled && data) setProfile(data);
      })
      .catch(() => {
        // The header still works without the name.
      });
    return () => {
      cancelled = true;
    };
  }, [state.status, api]);

  if (state.status !== "signedIn") return null;

  return (
    <div className="session-bar">
      <span>{profile ? `Signed in as ${profile.name} (${profile.role})` : "Signed in"}</span>
      <button type="button" className="secondary" onClick={() => void session.signOut()}>
        Sign out
      </button>
    </div>
  );
}
