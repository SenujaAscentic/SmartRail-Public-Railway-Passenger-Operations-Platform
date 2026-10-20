import { describe, expect, it } from "vitest";
import { fitScale, stageSize } from "./Stage.tsx";

describe("fitScale", () => {
  it("is 1 at the target size 1920 x 1080", () => {
    expect(fitScale(1920, 1080)).toBe(1);
  });

  it("shrinks the picture to a smaller window (the narrower side decides)", () => {
    expect(fitScale(1366, 768)).toBeCloseTo(0.711, 3);
    expect(fitScale(900, 1080)).toBeCloseTo(0.469, 3);
    expect(fitScale(1920, 540)).toBe(0.5);
  });

  it("fills a bigger screen too", () => {
    expect(fitScale(2560, 1440)).toBeCloseTo(1.333, 3);
    expect(fitScale(3840, 2160)).toBe(2);
  });
});

describe("stageSize: the picture always fills the whole window", () => {
  const window = (w: number, h: number) => {
    const s = stageSize(w, h);
    return { ...s, shownWidth: s.width * s.scale, shownHeight: s.height * s.scale };
  };

  it("16:9 windows show exactly the designed 1920 x 1080", () => {
    for (const [w, h] of [[1920, 1080], [2560, 1440], [3840, 2160]] as const) {
      const s = stageSize(w, h);
      expect(s.width).toBeCloseTo(1920, 6);
      expect(s.height).toBeCloseTo(1080, 6);
    }
  });

  it("a 1920-wide window that is not 1080 high (a browser with its toolbar) has no gap at the sides", () => {
    const s = window(1920, 950);
    expect(s.shownWidth).toBeCloseTo(1920, 6);
    expect(s.shownHeight).toBeCloseTo(950, 6);
    expect(s.width).toBeGreaterThan(1920); // more width than designed: the halves grow
  });

  it("a window narrower than 16:9 keeps at least the designed width and gets more height", () => {
    const s = stageSize(900, 700);
    expect(s.width).toBeCloseTo(1920, 6);
    expect(s.height).toBeGreaterThan(1080);
  });

  it("the picture is never smaller than the designed 1920 x 1080", () => {
    for (const [w, h] of [[900, 700], [1366, 768], [1920, 950], [2560, 1080], [3440, 1440]] as const) {
      const s = stageSize(w, h);
      expect(s.width).toBeGreaterThanOrEqual(1920 - 1e-6);
      expect(s.height).toBeGreaterThanOrEqual(1080 - 1e-6);
    }
  });
});
