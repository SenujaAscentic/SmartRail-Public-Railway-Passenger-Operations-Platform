import { ChatGoogleGenerativeAI } from "@langchain/google-genai";
import { END, START, StateGraph } from "@langchain/langgraph";
import { MODELS } from "./config.js";
import type { Persona } from "./personas.js";
import { SessionState, type SessionStateType, type Turn } from "./state.js";

// One client per model. maxRetries is low so a failing model is skipped quickly.
const clients = MODELS.map((model) => ({
  model,
  llm: new ChatGoogleGenerativeAI({ model, temperature: 0.7, maxRetries: 1 }),
}));

// Gone for the rest of this run: no daily quota left (429) or not available (404).
const unavailable = new Set<string>();
// Temporary problems worth trying another model for: 429, 404, 500, 503.
const FALLBACK_STATUS = new Set([404, 429, 500, 503]);

const render = (t: Turn[]) => t.map((x) => `${x.speaker.toUpperCase()}: ${x.text}`).join("\n\n");

async function ask(system: string, user: string): Promise<string> {
  let lastError: unknown;
  for (const { model, llm } of clients) {
    if (unavailable.has(model)) continue;
    try {
      const res = await llm.invoke([
        { role: "system", content: system },
        { role: "user", content: user },
      ]);
      return typeof res.content === "string" ? res.content : JSON.stringify(res.content);
    } catch (e) {
      const status = (e as { status?: number }).status;
      if (status === undefined || !FALLBACK_STATUS.has(status)) throw e;
      if (status === 429 || status === 404) unavailable.add(model);
      console.log(`${model} failed (${status}), trying next model`);
      lastError = e;
    }
  }
  throw lastError ?? new Error("No models available. Check GEMINI_MODELS.");
}

const MODERATOR_SYSTEM = `You are a business analyst moderating a requirements-discovery
session for SmartRail. Ask ONE short, probing question or raise ONE conflict at a time,
addressed to the named stakeholder. Dig into missing actors, processes, failure scenarios,
states, security, accessibility and integration gaps. Do not answer for the stakeholders.`;

const SUMMARY_SYSTEM = `You are a business analyst. From the transcript produce markdown with
these sections: Needs, Pain points, Failure scenarios, Requirements wanted (with priority),
Conflicts between roles, Open questions / assumptions. Only include what was actually said.`;

// "day" style: a chronological day-in-the-life interview about how things work today.
const DAY_MODERATOR_SYSTEM = `You are a business analyst doing a day-in-the-life interview about how
things work TODAY, before any new system. Ask ONE short, concrete question at a time about the
stage you are given. Ask for times, durations, how often, which paper, radios, phones, boards or
apps are used, and who they talk to. Never ask about future systems, features, hypotheticals or
solutions. Do not answer for the stakeholder.`;

const DAY_STAGES = [
  "the start of the day or shift: what happens first, at what time, with which tools and people",
  "the busiest or main part of the day: hour by hour what they do and decide, and with whom they communicate",
  "the rest of the day and how it ends: handover, final tasks, travelling home or leaving the station",
  "how a normal week differs: weekdays vs weekends, peak or special days, and tasks that happen weekly or monthly",
  "what goes wrong most often in a typical week, how often, how long it takes to sort out and what they do about it",
];

const DAY_SUMMARY_SYSTEM = `You are a business analyst. From the transcript write markdown with these
sections: Typical day (a time-ordered list), Typical week, Tools and information sources used today,
People they talk to, What goes wrong and how often. Only include what was actually said; write
"not stated" where the transcript has nothing.`;

export function buildGraph(personas: Persona[]) {
  const bySlug = new Map(personas.map((p) => [p.slug, p]));

  const moderator = async (s: SessionStateType) => {
    const next = s.roles[s.turn % s.roles.length];
    console.log(`[${s.turn + 1}/${s.maxTurns}] moderator -> ${next}`);
    const day = s.style === "day";
    const stage = DAY_STAGES[Math.min(s.turn, DAY_STAGES.length - 1)];
    const text = await ask(
      day ? DAY_MODERATOR_SYSTEM : MODERATOR_SYSTEM,
      day
        ? `Topic: ${s.topic}\nInterviewee: ${next}\nStage for this question: ${stage}\n\nTranscript so far:\n${render(s.transcript) || "(none)"}\n\nAsk your next question to ${next}.`
        : `Topic: ${s.topic}\nNext stakeholder: ${next}\n\nTranscript so far:\n${render(s.transcript) || "(none)"}\n\nAsk your next question to ${next}.`,
    );
    return { transcript: [{ speaker: "moderator", text }] };
  };

  const stakeholder = async (s: SessionStateType) => {
    const slug = s.roles[s.turn % s.roles.length];
    const persona = bySlug.get(slug)!;
    console.log(`[${s.turn + 1}/${s.maxTurns}] ${slug} replying`);
    const text = await ask(
      persona.prompt,
      `Discussion so far:\n${render(s.transcript)}\n\nReply as ${slug}.`,
    );
    return { transcript: [{ speaker: slug, text }], turn: s.turn + 1 };
  };

  const summarize = async (s: SessionStateType) => (console.log("summarizing"), {
    summary: await ask(s.style === "day" ? DAY_SUMMARY_SYSTEM : SUMMARY_SYSTEM, render(s.transcript)),
  });

  return new StateGraph(SessionState)
    .addNode("moderator", moderator)
    .addNode("stakeholder", stakeholder)
    .addNode("summarize", summarize)
    .addEdge(START, "moderator")
    .addEdge("moderator", "stakeholder")
    .addConditionalEdges("stakeholder", (s) => (s.turn >= s.maxTurns ? "summarize" : "moderator"), [
      "moderator",
      "summarize",
    ])
    .addEdge("summarize", END)
    .compile();
}
