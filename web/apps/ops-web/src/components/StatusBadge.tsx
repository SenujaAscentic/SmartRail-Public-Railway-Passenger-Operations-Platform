import { Icon } from "./Icon.tsx";
import type { View } from "../status/status-text.ts";

// A state in icon + words + border + colour (design rule 1: never colour alone). The wording comes from status-text.ts.
export function StatusBadge({ view }: { view: View }) {
  return (
    <span className={`status status-${view.tone}`}>
      <Icon name={view.icon} />
      <span>{view.text}</span>
    </span>
  );
}
