import { useEffect, useState, type ReactNode } from "react";

// The displays are designed for one screen, 1920 x 1080 (design: docs/design/display). On any other window the picture is
// scaled, never laid out again from scratch, so nothing (a platform number above all) can be pushed off the screen or wrap
// differently. The scale is the one that keeps AT LEAST the designed 1920 x 1080 visible; the picture then fills the WHOLE window:
// a wider window than 16:9 gives the board more width (the two halves grow), a taller one gives it more height. No gap at the sides.
export const STAGE_WIDTH = 1920;
export const STAGE_HEIGHT = 1080;

export interface StageSize {
  scale: number;
  // The size of the picture before it is scaled; scale x size = the window.
  width: number;
  height: number;
}

export function stageSize(windowWidth: number, windowHeight: number): StageSize {
  const scale = Math.min(windowWidth / STAGE_WIDTH, windowHeight / STAGE_HEIGHT);
  return { scale, width: windowWidth / scale, height: windowHeight / scale };
}

// The scale that fits the designed 1920 x 1080 into a window of this size (above 1 on a bigger screen, so a 4K display is filled too).
export function fitScale(windowWidth: number, windowHeight: number): number {
  return stageSize(windowWidth, windowHeight).scale;
}

function currentSize(): StageSize {
  return stageSize(window.innerWidth, window.innerHeight);
}

export function Stage({ children }: { children: ReactNode }) {
  const [size, setSize] = useState(currentSize);
  useEffect(() => {
    const onResize = () => setSize(currentSize());
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, []);

  return (
    <div className="stage-frame">
      <div className="stage" style={{ width: size.width, height: size.height, transform: `scale(${size.scale})` }}>
        {children}
      </div>
    </div>
  );
}
