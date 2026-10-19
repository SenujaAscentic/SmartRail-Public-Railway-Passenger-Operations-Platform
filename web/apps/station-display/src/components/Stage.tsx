import { useEffect, useState, type ReactNode } from "react";

// The displays are designed for one screen, 1920 x 1080 (design: docs/design/display). On any other window size the whole
// picture is scaled to fit, centred, instead of being laid out again: a smaller window shows the SAME layout, smaller, so
// nothing (a platform number above all) can be pushed off the screen or wrap differently.
export const STAGE_WIDTH = 1920;
export const STAGE_HEIGHT = 1080;

// The scale that fits the stage into a window of this size (above 1 on a bigger screen, so a 4K display is filled too).
export function fitScale(windowWidth: number, windowHeight: number): number {
  return Math.min(windowWidth / STAGE_WIDTH, windowHeight / STAGE_HEIGHT);
}

function currentScale(): number {
  return fitScale(window.innerWidth, window.innerHeight);
}

export function Stage({ children }: { children: ReactNode }) {
  const [scale, setScale] = useState(currentScale);
  useEffect(() => {
    const onResize = () => setScale(currentScale());
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, []);

  // Centred: the free space is split on both sides of the scaled picture.
  const left = (window.innerWidth - STAGE_WIDTH * scale) / 2;
  const top = (window.innerHeight - STAGE_HEIGHT * scale) / 2;
  return (
    <div className="stage-frame">
      <div className="stage" style={{ transform: `scale(${scale})`, left, top }}>
        {children}
      </div>
    </div>
  );
}
