import { useEffect, useState } from "react";
import type { components } from "@smartrail/api-client";
import { useServices, useSessionState } from "../services-context.tsx";

export type Profile = components["schemas"]["StaffMeResponse"];

// Who is signed in (GET /staff/me): the screens use the ROLE to decide what to offer (only the controller decides things) and the
// station of a station manager as the default view. The server still checks every request; hiding a button is only courtesy.
export function useProfile(): Profile | null {
  const { api } = useServices();
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
        // Without a profile the screens show the read-only view.
      });
    return () => {
      cancelled = true;
    };
  }, [state.status, api]);

  return profile;
}
