import { appendFile, mkdir, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { SESSIONS_DIR, TRANSCRIPTS_DIR } from "./config.js";
import type { SessionStateType } from "./state.js";

const stamp = () => new Date().toISOString().replace(/[:.]/g, "-");

export async function writeSession(s: SessionStateType): Promise<string> {
  await mkdir(TRANSCRIPTS_DIR, { recursive: true });
  const id = `${s.roles.join("+")}-${stamp()}`;
  const file = resolve(TRANSCRIPTS_DIR, `${id}.md`);

  const raw = s.transcript.map((t) => `**${t.speaker}**\n\n${t.text}`).join("\n\n---\n\n");
  await writeFile(file, `# Session ${id}\n\nTopic: ${s.topic}\n\n${raw}\n`);

  // Append a summary to each participating role's session notes.
  const note = `\n## ${new Date().toISOString()} - ${s.topic}\n\nRoles: ${s.roles.join(", ")}\nTranscript: transcripts/${id}.md\n\n${s.summary}\n`;
  for (const role of s.roles) {
    await appendFile(resolve(SESSIONS_DIR, `${role}.md`), note);
  }
  return file;
}
