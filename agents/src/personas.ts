import { readFile, readdir } from "node:fs/promises";
import { resolve } from "node:path";
import { BRIEF_FILES, PERSONAS_DIR } from "./config.js";

export interface Persona {
  slug: string; // filename without .md, e.g. "daily-commuter"
  prompt: string; // full system prompt
}

const GROUND_RULES = `
You are taking part in a requirements-discovery discussion for SmartRail.
Stay strictly in character. Speak from your own needs, routine and frustrations.
Do NOT design software or propose technical solutions. Say what you need, what
goes wrong today, and what the brief leaves out. Disagree with other stakeholders
when your interests conflict. Keep replies under 200 words.
Stay inside the Sri Lankan setting described in the shared local context.
Before saying the brief leaves something out, check the brief text: only claim
a gap if the brief really does not mention it.
`;

// "day" style: describe how things work TODAY, without the brief or any new system.
const DAY_RULES = `
You are being interviewed about your normal working life or travel routine as it is
TODAY, before any new system exists. Stay strictly in character and describe concrete
facts: times of day, how long things take, how often they happen, which paper, radios,
phones, boards or apps you use, and who you talk to. Do NOT talk about new software or
what you would like it to do. Answer exactly the question asked, in order, and keep
replies under 200 words. Stay inside the Sri Lankan setting described above.
`;

async function loadSharedContext(): Promise<string> {
  return readFile(resolve(PERSONAS_DIR, "_shared-context.md"), "utf8");
}

async function loadBrief(): Promise<string> {
  const parts = await Promise.all(
    BRIEF_FILES.map(async (f) => `## ${f}\n${await readFile(f, "utf8")}`),
  );
  return parts.join("\n\n");
}

export async function listPersonaSlugs(): Promise<string[]> {
  const files = await readdir(PERSONAS_DIR);
  return files.filter((f) => f.endsWith(".md") && !f.startsWith("_")).map((f) => f.replace(/\.md$/, ""));
}

export async function loadPersonas(
  slugs: string[],
  style: "discovery" | "day" = "discovery",
): Promise<Persona[]> {
  const shared = await loadSharedContext();
  if (style === "day") {
    return Promise.all(
      slugs.map(async (slug) => {
        const body = await readFile(resolve(PERSONAS_DIR, `${slug}.md`), "utf8");
        return { slug, prompt: `${body}\n\n${shared}\n\n${DAY_RULES}` };
      }),
    );
  }
  const brief = await loadBrief();
  return Promise.all(
    slugs.map(async (slug) => {
      const body = await readFile(resolve(PERSONAS_DIR, `${slug}.md`), "utf8");
      return {
        slug,
        prompt: `${body}\n\n${shared}\n\n${GROUND_RULES}\n# Project context\n${brief}`,
      };
    }),
  );
}
