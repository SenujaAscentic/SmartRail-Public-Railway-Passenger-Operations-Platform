import { Navigate, Route, Routes } from "react-router";
import { Layout } from "./components/Layout.tsx";
import { RequireSession } from "./components/RequireSession.tsx";
import { AuditPage } from "./pages/AuditPage.tsx";
import { DashboardPage } from "./pages/DashboardPage.tsx";
import { JourneyPage } from "./pages/JourneyPage.tsx";
import { LivePage } from "./pages/LivePage.tsx";
import { SignInPage } from "./pages/SignInPage.tsx";

// The routes of the ops web. Everything except the sign-in page needs a signed-in person.
export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="sign-in" element={<SignInPage />} />
        <Route element={<RequireSession />}>
          <Route index element={<DashboardPage />} />
          <Route path="journeys/:journeyId" element={<JourneyPage />} />
          <Route path="audit" element={<AuditPage />} />
          <Route path="live" element={<LivePage />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
