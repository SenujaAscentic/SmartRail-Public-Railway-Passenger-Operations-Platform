import { expect, type Page } from "@playwright/test";
import { spawn, type ChildProcess } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { repo, stateFile, type Stack } from "./stack.ts";

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
  child.stdout?.on("data", (d) => (text += d));
  child.stderr?.on("data", (d) => (text += d));
  return { process: child, output: () => text };
}
