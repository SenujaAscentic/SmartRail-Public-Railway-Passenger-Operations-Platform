import { expect, type Page } from "@playwright/test";
import { spawn, type ChildProcess } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { repo, simsFile, stateFile, type Stack } from "./stack.ts";

export type RunningStack = Stack & { catering: { secret: string; key: string } };

export const stack = (): RunningStack => JSON.parse(readFileSync(stateFile, "utf-8")) as RunningStack;

export const evidenceDir = join(repo, "docs", "qa", "evidence");

// What a flow measured, written next to the traces: the times are real wall-clock milliseconds.
export function record(flow: string, data: Record<string, unknown>) {
  mkdirSync(evidenceDir, { recursive: true });
  writeFileSync(join(evidenceDir, `${flow}.json`), JSON.stringify({ flow, recordedAt: new Date().toISOString(), ...data }, null, 2));
}

// The controller signs in through the ops web, like a person.
export async function signInAsController(page: Page) {
  const s = stack();
  await page.goto(s.opsWeb + "/");
  await page.getByLabel("Staff number").fill("CTRL-01");
  await page.getByLabel("Password").fill(s.demoPassword);
  await page.getByRole("button", { name: /sign in/i }).click();
  await expect(page.getByRole("heading", { name: /Dashboard/ })).toBeVisible();
}

// Opens a train's journey page from "Today's trains" (the controller can open a train that has not started).
export async function openJourney(page: Page, serviceNumber: string): Promise<string> {
  const panel = page.locator("section", { has: page.getByRole("heading", { name: "Today's trains" }) });
  const link = panel.getByRole("link", { name: new RegExp(`Open ${serviceNumber}`) });
  await expect(link).toBeVisible({ timeout: 60_000 });
  const id = (await link.getAttribute("href"))!.split("/").pop()!;
  await link.click();
  await expect(page.getByRole("heading", { level: 1 })).toContainText(serviceNumber);
  return id;
}

export async function getJson<T = any>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${url} answered ${response.status}`);
  return (await response.json()) as T;
}

// Polls until `check` returns a value; returns it with the wall-clock time it was first seen.
export async function until<T>(what: string, check: () => Promise<T | undefined | false>, seconds: number, everyMs = 200): Promise<{ value: T; at: number }> {
  const limit = Date.now() + seconds * 1000;
  while (Date.now() < limit) {
    const value = await check().catch(() => undefined);
    if (value) return { value, at: Date.now() };
    await new Promise((r) => setTimeout(r, everyMs));
  }
  throw new Error(`Timed out after ${seconds} s waiting for ${what}.`);
}

// The simulator acts as the cab tablet (the driver app is deferred): the real console program, driven through the public API.
export function startSimulator(options: { service: string; device: string; driver: string; extra?: string[] }): { process: ChildProcess; output: () => string } {
  const s = stack();
  const dll = join(repo, "backend", "src", "SmartRail.Simulator", "bin", "Debug", "net10.0", "SmartRail.Simulator.dll");
  let text = "";
  const child = spawn(
    "dotnet",
    [dll, "--api", s.api, "--device", options.device, "--driver", options.driver, "--service", options.service, "--from", "FOT", "--to", "KDT", "--speed", String(s.clockSpeed), ...(options.extra ?? [])],
    { env: { ...process.env, SMARTRAIL_CAB_SECRET: s.cabSecrets[options.device]!, SMARTRAIL_DEMO_PASSWORD: s.demoPassword }, windowsHide: true },
  );
  const pids: number[] = existsSync(simsFile) ? (JSON.parse(readFileSync(simsFile, "utf-8")) as number[]) : [];
  writeFileSync(simsFile, JSON.stringify([...pids, child.pid!])); // the run's teardown stops it if the flow does not
  child.stdout?.on("data", (d) => (text += d));
  child.stderr?.on("data", (d) => (text += d));
  return { process: child, output: () => text };
}

// The API's own clock, as an apps sees it (the Date header), and the Colombo date of it.
export async function serverNow(): Promise<Date> {
  const response = await fetch(`${stack().api}/api/v1/lines`);
  return new Date(response.headers.get("date")!);
}

export const colomboClock = (d: Date) => new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Colombo", hour: "2-digit", minute: "2-digit", hour12: false }).format(d);
export const colomboDate = (d: Date) => new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Colombo" }).format(d); // yyyy-MM-dd

export async function stationId(code: string): Promise<string> {
  const lines = await getJson<any[]>(`${stack().api}/api/v1/lines`);
  for (const line of lines) for (const route of line.routes) for (const st of route.stations) if (st.code === code) return st.stationId;
  throw new Error(`No station ${code}`);
}

// One stream message as it arrived, with the wall-clock time it arrived.
export interface StreamMessage { event: string; data: any; at: number }

// Listens to the public event stream like an app does (only the events of the given journeys) and keeps what arrives.
export function listen(journeyId: string): { messages: StreamMessage[]; stop: () => void } {
  const messages: StreamMessage[] = [];
  const controller = new AbortController();
  void (async () => {
    const response = await fetch(`${stack().api}/api/v1/stream?journeyIds=${journeyId}`, { signal: controller.signal });
    const reader = response.body!.getReader();
    const decoder = new TextDecoder();
    let buffer = "";
    for (;;) {
      const { done, value } = await reader.read();
      if (done) return;
      buffer += decoder.decode(value, { stream: true });
      let end: number;
      while ((end = buffer.indexOf("\n\n")) >= 0) {
        const block = buffer.slice(0, end);
        buffer = buffer.slice(end + 2);
        const event = /^event: (.+)$/m.exec(block)?.[1];
        const data = /^data: (.+)$/m.exec(block)?.[1];
        if (event && data) messages.push({ event, data: JSON.parse(data), at: Date.now() });
      }
    }
  })().catch(() => undefined);
  return { messages, stop: () => controller.abort() };
}
