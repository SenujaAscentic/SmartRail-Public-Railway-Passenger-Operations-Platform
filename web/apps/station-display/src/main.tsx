import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router";
import "@smartrail/tokens/tokens-display.css";
import "./styles.css";
import { App } from "./App.tsx";
import { createServices } from "./services.tsx";
import { ServicesProvider } from "./services.tsx";

// Same origin as the page: in development the Vite dev server forwards /api to the backend.
const services = createServices();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <ServicesProvider services={services}>
        <App />
      </ServicesProvider>
    </BrowserRouter>
  </StrictMode>,
);
