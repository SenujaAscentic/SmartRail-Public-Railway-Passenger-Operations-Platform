import { Navigate, Outlet } from "react-router";
import { useSessionState } from "../services-context.tsx";

// Wraps the pages that need a signed-in person. While the page is still asking the server whether the refresh cookie works,
// it shows a short message; a person who is not signed in (or whose session just ended) is sent to the sign-in page,
// which tells them why when the session ended by itself.
export function RequireSession() {
  const state = useSessionState();

  if (state.status === "checking") return <p role="status">Checking your session…</p>;
  if (state.status === "signedOut") return <Navigate to="/sign-in" replace />;
  return <Outlet />;
}
