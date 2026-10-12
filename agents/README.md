# SmartRail discovery agents

LangGraph (TypeScript) agents that simulate requirement-discovery sessions.

Flow: `moderator -> stakeholder -> (loop until maxTurns) -> summarize`.
Personas come from `docs/discover/personas/<slug>.md`; each file is a system prompt.
`personas/_shared-context.md` (Sri Lankan setting, shared by all personas; files starting with `_` are not personas) is also injected. Only the original brief (`final-assessment-scoped 2.md`) is injected, so agents are not biased by your own analysis and decisions.

## Setup

    cd agents
    npm install
    cp .env.example .env   # add GOOGLE_API_KEY (Gemini)

## Run

    # one-on-one interview
    npm run session -- --roles daily-commuter --topic "Daily journey needs" --turns 4
    # multi-role debate
    npm run session -- --roles daily-commuter,timetable-planner --topic "Platform change timing" --turns 6

Output: full transcript in `stakeholder-sessions/transcripts/`, summary appended to each role's
`stakeholder-sessions/<role>.md`. Review and validate before copying into the assumptions log;
record what you accepted or rejected in `docs/ai-usage/ai-usage-log.md`.
