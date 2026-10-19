import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

// The display pages and the API share ONE origin in development: the dev server answers /api itself and forwards it to the
// backend (no CORS, same shape as in production behind one host). The backend is http://localhost:5045; set BACKEND_URL to
// use another. Port 5174, because the ops web uses 5173.
const backend = process.env.BACKEND_URL ?? "http://localhost:5045";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174,
    strictPort: true,
    proxy: { "/api": { target: backend } },
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
  },
});
