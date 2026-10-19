import { Route, Routes } from "react-router";
import { Stage } from "./components/Stage.tsx";
import { CenterMessage } from "./components/parts.tsx";
import { BoardPage } from "./pages/BoardPage.tsx";
import { PlatformPage } from "./pages/PlatformPage.tsx";
import { TEXT } from "./wording.ts";

// The address names the screen and the station: /board/FOT is the departures board of Colombo Fort, /platform/FOT/5 the display of
// its Platform 5. The pages need no sign-in (public, read-only endpoints).
export function App() {
  return (
    <Stage>
    <Routes>
      <Route path="/board/:station" element={<BoardPage />} />
      <Route path="/platform/:station/:platform" element={<PlatformPage />} />
      <Route path="*" element={<CenterMessage icon="x-circle" text={TEXT.unknownPage} />} />
    </Routes>
    </Stage>
  );
}
