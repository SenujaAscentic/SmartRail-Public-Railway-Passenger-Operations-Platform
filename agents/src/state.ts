import { Annotation } from "@langchain/langgraph";

export interface Turn {
  speaker: string; // "moderator" or a persona slug
  text: string;
}

export const SessionState = Annotation.Root({
  topic: Annotation<string>(),
  roles: Annotation<string[]>(), // persona slugs taking part
  maxTurns: Annotation<number>(), // number of persona replies before wrapping up
  style: Annotation<"discovery" | "day">({ reducer: (_a, b) => b, default: () => "discovery" }),
  turn: Annotation<number>({ reducer: (_a, b) => b, default: () => 0 }),
  transcript: Annotation<Turn[]>({
    reducer: (a, b) => a.concat(b),
    default: () => [],
  }),
  summary: Annotation<string>({ reducer: (_a, b) => b, default: () => "" }),
});

export type SessionStateType = typeof SessionState.State;
