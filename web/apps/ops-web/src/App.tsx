import { Navigate, Route, Routes } from "react-router";
import { Layout } from "./components/Layout.tsx";
import { RequireSession } from "./components/RequireSession.tsx";
import { HomePage } from "./pages/HomePage.tsx";
import { LivePage } from "./pages/LivePage.tsx";
import { SignInPage } from "./pages/SignInPage.tsx";

// The routes of the ops web. Everything except the sign-in page needs a signed-in person.
export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="sign-in" element={<SignInPage />} />
        <Route element={<RequireSession />}>
          <Route index element={<HomePage />} />
          <Route path="live" element={<LivePage />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
