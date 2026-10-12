import "dotenv/config";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));

export const PROJECT_ROOT = resolve(here, "../..");
export const DISCOVER_DIR = resolve(PROJECT_ROOT, "docs/discover");
export const PERSONAS_DIR = resolve(DISCOVER_DIR, "personas");
export const SESSIONS_DIR = resolve(DISCOVER_DIR, "stakeholder-sessions");
export const TRANSCRIPTS_DIR = resolve(SESSIONS_DIR, "transcripts");

// Context every persona must read before speaking. Only the original brief:
// the analysis and assumptions hold the author's decisions and would bias discovery.
export const BRIEF_FILES = [resolve(PROJECT_ROOT, "final-assessment-scoped 2.md")];

// Models are tried in order; the next one is used when a model is out of quota or overloaded.
export const MODELS = (
  process.env.GEMINI_MODELS ?? "gemini-3.1-flash-lite,gemini-3.7-flash,gemini-3.6-flash,gemini-3.5-flash,gemini-3.8-flash,gemini-3.5-flash-lite,gemini-flash-lite-latest"
)
  .split(",")
  .map((m) => m.trim())
  .filter(Boolean);
