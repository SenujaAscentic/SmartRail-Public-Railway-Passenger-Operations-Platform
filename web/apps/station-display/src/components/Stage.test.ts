import { describe, expect, it } from "vitest";
import { fitScale } from "./Stage.tsx";

describe("fitScale", () => {
  it("is 1 at the target size 1920 x 1080", () => {
    expect(fitScale(1920, 1080)).toBe(1);
  });

  it("shrinks the whole picture to fit a smaller window (the narrower side decides)", () => {
    expect(fitScale(1366, 768)).toBeCloseTo(0.711, 3);
    expect(fitScale(900, 1080)).toBeCloseTo(0.469, 3);
    expect(fitScale(1920, 540)).toBe(0.5);
  });

  it("fills a bigger screen too", () => {
    expect(fitScale(3840, 2160)).toBe(2);
  });
});
