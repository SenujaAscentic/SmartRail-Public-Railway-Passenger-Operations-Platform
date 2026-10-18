import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

// In development the app and the API share ONE origin: the dev server answers /api itself and forwards it to the backend.
// Why: the refresh cookie is HttpOnly + SameSite=Strict, so the browser only sends it to the page's own origin, and with a
// single origin no CORS setup is needed (contract section 2, C-3). The same shape works in production behind one host.
// The backend address is http://localhost:5045 (launchSettings.json); set BACKEND_URL to use another.
const backend = process.env.BACKEND_URL ?? "http://localhost:5045";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: { "/api": { target: backend } },
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
  },
});
