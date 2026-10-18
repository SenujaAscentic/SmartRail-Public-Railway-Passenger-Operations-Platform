import { existsSync, readdirSync, readFileSync } from "node:fs";
import { basename, resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { describeFrame } from "./live-events.ts";

// CLAUDE.md section 5.5: every SSE event type has ONE example JSON in shared/events/, and the backend, the web and Flutter tests
// parse these files. This is the web's side. It checks every file that exists, so it passes with however many there are, and a
// new example is covered the moment it lands. (SHARED_EVENTS_DIR only exists so the test can be tried against another folder.)
const folder = process.env.SHARED_EVENTS_DIR ?? resolve(import.meta.dirname, "../../../../../shared/events");
const files = existsSync(folder) ? readdirSync(folder).filter((name) => name.endsWith(".json")).sort() : [];

// "snapshot" and "resync" are stream messages of their own; every other file is named after an event type (domain model 8.1 envelope).
const STREAM_MESSAGES = new Set(["snapshot", "resync"]);

const isObject = (value: unknown): value is Record<string, unknown> => typeof value === "object" && value !== null && !Array.isArray(value);
const isInteger = (value: unknown) => typeof value === "number" && Number.isInteger(value);
const isUtc = (value: unknown) => typeof value === "string" && /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?Z$/.test(value);

describe("shared/events examples", () => {
  it("are in a folder that exists", () => {
    expect(existsSync(folder), `${folder} should exist`).toBe(true);
  });

  for (const file of files) {
    const name = basename(file, ".json");
    const text = readFileSync(resolve(folder, file), "utf8");

    describe(file, () => {
      it("is valid JSON describing an object", () => {
        expect(isObject(JSON.parse(text))).toBe(true);
      });

      if (name === "snapshot") {
        it("has the fields of a journey snapshot", () => {
          const data = JSON.parse(text) as Record<string, unknown>;
          expect(typeof data.journeyId).toBe("string");
          expect(isInteger(data.sequence)).toBe(true);
          expect(Array.isArray(data.stops)).toBe(true);
        });
      } else if (name === "resync") {
        it("explains why in a reason", () => {
          expect(typeof (JSON.parse(text) as Record<string, unknown>).reason).toBe("string");
        });
      } else {
        it("has the whole envelope, and its type is the file name", () => {
          const event = JSON.parse(text) as Record<string, unknown>;
          expect(typeof event.eventId).toBe("string");
          expect(isInteger(event.sequence)).toBe(true);
          expect(event.type).toBe(name);
          expect(isInteger(event.version)).toBe(true);
          expect(isUtc(event.occurredAt), "occurredAt is UTC").toBe(true);
          expect(Array.isArray(event.stationIds)).toBe(true);
          expect(typeof event.lineId).toBe("string");
          expect(isObject(event.payload)).toBe(true);
        });
      }

      // The page reads sequence, time and journey from every event; it must be able to read each of these examples.
      it("can be read by the live page", () => {
        const event = describeFrame({ name, id: "", data: text }, 1);
        expect(event.name).toBe(name);
        if (name !== "resync") {
          expect(event.sequence).not.toBeNull();
          expect(event.journeyId).not.toBeNull();
        }
        if (!STREAM_MESSAGES.has(name)) expect(event.occurredAt).not.toBeNull();
      });
    });
  }
});
