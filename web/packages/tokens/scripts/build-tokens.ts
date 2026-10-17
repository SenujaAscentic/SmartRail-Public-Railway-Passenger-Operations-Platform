// Reads shared/tokens.json and writes dist/tokens.css. Run by `npm run build` (and before the app's dev server, build and tests).
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { tokensToCss } from "../src/tokens-to-css.ts";
import type { TokensFile } from "../src/tokens-file.ts";

const packageDir = resolve(import.meta.dirname, "..");
const tokensPath = resolve(packageDir, "../../../shared/tokens.json");
const outputPath = resolve(packageDir, "dist/tokens.css");

const tokens = JSON.parse(readFileSync(tokensPath, "utf8")) as TokensFile;
mkdirSync(dirname(outputPath), { recursive: true });
writeFileSync(outputPath, tokensToCss(tokens, "ops"));
console.log(`Wrote ${outputPath}`);
