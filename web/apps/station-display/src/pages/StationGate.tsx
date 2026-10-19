import type { ReactNode } from "react";
import { CenterMessage } from "../components/parts.tsx";
import { useStation } from "../hooks.ts";
import type { LineInfo, StationRef } from "../types.ts";
import { TEXT } from "../wording.ts";

// Finds the station named in the address, then shows the page for it. While it is not known yet, or if it is unknown, it says so.
export function StationGate({ code, children }: { code: string; children: (station: StationRef, lines: LineInfo[]) => ReactNode }) {
  const state = useStation(code);
  switch (state.kind) {
    case "loading":
      return <CenterMessage icon="clock" text={TEXT.loading} />;
    case "unknown":
      return <CenterMessage icon="x-circle" text={TEXT.unknownStation} />;
    case "failed":
      return <CenterMessage icon="offline" text={TEXT.loadFailed} />;
    case "ready":
      return <>{children(state.station, state.lines)}</>;
  }
}
