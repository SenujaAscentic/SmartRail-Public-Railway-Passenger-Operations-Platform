import { Link } from "react-router";

// Where a signed-in person lands.
export function HomePage() {
  return (
    <>
      <h1>SmartRail Operations</h1>
      <p>You are signed in.</p>
      <ul>
        <li>
          <Link to="/live">Live events</Link>
        </li>
      </ul>
    </>
  );
}
