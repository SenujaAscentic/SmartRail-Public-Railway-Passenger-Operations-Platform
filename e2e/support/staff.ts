import { expect, type Page } from "@playwright/test";
import { stack } from "./helpers.ts";

// A staff member signs in through the ops web, like a person (signInAsController in helpers.ts does the same for CTRL-01).
export async function signInAs(page: Page, staffNumber: string) {
  const s = stack();
  await page.goto(s.opsWeb + "/");
  await page.getByLabel("Staff number").fill(staffNumber);
  await page.getByLabel("Password").fill(s.demoPassword);
  await page.getByRole("button", { name: /sign in/i }).click();
  await expect(page.getByRole("heading", { name: /Dashboard/ })).toBeVisible();
}

// A staff member's access token, for calling the API directly (contract section 3.2: POST /auth/staff/sign-in).
export async function staffToken(staffNumber: string): Promise<string> {
  const s = stack();
  const response = await fetch(`${s.api}/api/v1/auth/staff/sign-in`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ staffNumber, password: s.demoPassword }),
  });
  if (!response.ok) throw new Error(`Staff sign-in of ${staffNumber} answered ${response.status}`);
  return ((await response.json()) as { accessToken: string }).accessToken;
}

export const bearer = (token: string) => ({ authorization: `Bearer ${token}`, "content-type": "application/json" });
