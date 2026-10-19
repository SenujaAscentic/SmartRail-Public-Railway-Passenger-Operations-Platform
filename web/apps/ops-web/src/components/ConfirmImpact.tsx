import { Icon } from "./Icon.tsx";
import { StatusBadge } from "./StatusBadge.tsx";
import type { View } from "../status/status-text.ts";

// The "This will affect" block of the ConfirmImpactDialog (US-O4 AC3). It lists what the backend will do to the people and screens
// that follow this train. Designed but not shown yet: the crew instruction (crew messages are deferred) and the catering line
// (the journey detail does not say which stations have catering; there is no impact-preview endpoint).
export function ConfirmImpact({ stations, badge, displayText }: { stations: string[]; badge: View; displayText: string }) {
  return (
    <div className="impact" aria-label="This will affect">
      <p><strong>This will affect</strong></p>
      <dl>
        <div>
          <dt><Icon name="pin" /> Stations</dt>
          <dd>{stations.length === 0 ? "none" : `${stations.join(", ")} (remaining stops)`}</dd>
        </div>
        <div>
          <dt><Icon name="phone" /> Passengers&rsquo; apps</dt>
          <dd><StatusBadge view={badge} /></dd>
        </div>
        <div>
          <dt><Icon name="monitor" /> Station displays</dt>
          <dd>{stations.length === 0 ? "no station" : `${stations.length === 1 ? "1 station shows" : `${stations.length} stations show`} "${displayText}"`}</dd>
        </div>
      </dl>
    </div>
  );
}
