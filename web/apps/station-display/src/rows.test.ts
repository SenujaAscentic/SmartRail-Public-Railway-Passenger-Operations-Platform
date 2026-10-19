import { describe, expect, it } from "vitest";
import { boardRows, callingPointsAfter, groupByDirection, platformDirection, platformView } from "./rows.ts";
import { FORT, GAMPAHA, journey, KANDY, LINES, MARADANA, POLGAHAWELA, row, toFort, toKandy } from "./test/data.ts";

const NOW = Date.parse("2026-10-20T01:40:00Z"); // 07:10 in Colombo

describe("boardRows", () => {
  it("keeps the trains that stop here and leaves out the ones that only pass", () => {
    const rows = [toKandy(1), toKandy(2, { callType: "Pass" }), toKandy(3)];
    expect(boardRows(rows, NOW, false).map((r) => r.serviceNumber)).toEqual(["1001", "1003"]);
  });

  it("shows at most 10, in the order the backend sent", () => {
    const rows = Array.from({ length: 14 }, (_, i) => toKandy(i + 1));
    const shown = boardRows(rows, NOW, false);
    expect(shown).toHaveLength(10);
    expect(shown[0]!.serviceNumber).toBe("1001");
    expect(shown[9]!.serviceNumber).toBe("1010");
  });

  it("online, a cancelled train whose time has just passed stays: the backend decides what is on the board", () => {
    const past = toKandy(1, { time: "2026-10-20T01:30:00Z", status: { kind: "Cancelled", delayMinutes: null } });
    expect(boardRows([past], NOW, false)).toHaveLength(1);
  });

  it("offline, trains are removed by the clock as their scheduled time passes", () => {
    const rows = [toKandy(1, { time: "2026-10-20T01:30:00Z" }), toKandy(2, { time: "2026-10-20T01:40:00Z" }), toKandy(3, { time: "2026-10-20T01:50:00Z" })];
    expect(boardRows(rows, NOW, true).map((r) => r.serviceNumber)).toEqual(["1002", "1003"]);
  });

  it("is empty when the backend sends nothing (no more departures today)", () => {
    expect(boardRows([], NOW, false)).toEqual([]);
  });
});

describe("groupByDirection", () => {
  it("at Gampaha: one column towards Kandy and one towards Colombo Fort, Kandy first", () => {
    const rows = [toFort(1), toKandy(1), toKandy(2), toFort(2)];
    const columns = groupByDirection(rows, LINES, GAMPAHA);

    expect(columns.map((c) => c.terminus)).toEqual(["Kandy", "Colombo Fort"]);
    expect(columns.map((c) => c.arrow)).toEqual(["right", "left"]);
    expect(columns[0]!.rows.map((r) => r.serviceNumber)).toEqual(["1001", "1002"]);
    expect(columns[1]!.rows.map((r) => r.serviceNumber)).toEqual(["2001", "2002"]);
  });

  it("keeps the order of the lines inside a column", () => {
    const columns = groupByDirection([toKandy(2), toKandy(1)], LINES, GAMPAHA);
    expect(columns[0]!.rows.map((r) => r.serviceNumber)).toEqual(["1002", "1001"]);
  });

  it("a train that ends early still belongs to its direction (its destination is the termination station)", () => {
    const early = toKandy(1, { destination: { stationId: POLGAHAWELA, name: "Polgahawela" } });
    const columns = groupByDirection([early], LINES, GAMPAHA);
    expect(columns[0]!.rows).toHaveLength(1);
    expect(columns[1]!.rows).toHaveLength(0);
  });

  it("at Colombo Fort there is only the column towards Kandy", () => {
    const columns = groupByDirection([toKandy(1)], LINES, FORT);
    expect(columns.map((c) => c.terminus)).toEqual(["Kandy"]);
  });

  it("at Kandy there is only the column towards Colombo Fort", () => {
    const columns = groupByDirection([toFort(1)], LINES, KANDY);
    expect(columns.map((c) => c.terminus)).toEqual(["Colombo Fort"]);
  });

  it("a line whose destination is not after this station on any route is left out", () => {
    const columns = groupByDirection([toKandy(1, { destination: { stationId: GAMPAHA, name: "Gampaha" } })], LINES, GAMPAHA);
    expect(columns.every((c) => c.rows.length === 0)).toBe(true);
  });

  it("a station that is on no route gives no columns", () => {
    expect(groupByDirection([toKandy(1)], LINES, "00000000-0000-4000-8000-0000000000ff")).toEqual([]);
  });
});

describe("one line for each journey", () => {
  it("the board shows a journey once, even if the list holds it twice", () => {
    const rows = [toKandy(1), toKandy(2), toKandy(1)];
    expect(boardRows(rows, NOW, false).map((r) => r.serviceNumber)).toEqual(["1001", "1002"]);
  });

  it("the platform's 'Then' never repeats the next train", () => {
    const view = platformView([toKandy(1), toKandy(1), toKandy(2), toKandy(1), toKandy(3)], "1", NOW, false);
    expect(view.next!.serviceNumber).toBe("1001");
    expect(view.then.map((r) => r.serviceNumber)).toEqual(["1002", "1003"]);
  });
});

describe("platformView", () => {
  it("next is the first train on the platform, then the following two", () => {
    const rows = [toKandy(1), toFort(1), toKandy(2), toKandy(3), toKandy(4)];
    const view = platformView(rows, "1", NOW, false);

    expect(view.next!.serviceNumber).toBe("1001");
    expect(view.then.map((r) => r.serviceNumber)).toEqual(["1002", "1003"]);
  });

  it("trains of other platforms are not on it", () => {
    const view = platformView([toFort(1), toFort(2)], "1", NOW, false);
    expect(view.next).toBeNull();
    expect(view.then).toEqual([]);
  });

  it("a train that only passes can be the next train (S-18)", () => {
    const view = platformView([toKandy(1, { callType: "Pass" }), toKandy(2)], "1", NOW, false);
    expect(view.next!.callType).toBe("Pass");
    expect(view.then.map((r) => r.serviceNumber)).toEqual(["1002"]);
  });

  it("a train whose platform changed away shows as moved, and is no longer on this platform", () => {
    const moved = toKandy(1, { platform: "5", previousPlatform: "1", serviceNumber: "1520" });
    const view = platformView([moved, toKandy(2)], "1", NOW, false);

    expect(view.movedAway.map((r) => r.serviceNumber)).toEqual(["1520"]);
    expect(view.next!.serviceNumber).toBe("1002");
  });

  it("a train that changed TO this platform is on it, not moved away", () => {
    const arriving = toKandy(1, { platform: "1", previousPlatform: "3" });
    const view = platformView([arriving], "1", NOW, false);
    expect(view.next!.serviceNumber).toBe("1001");
    expect(view.movedAway).toEqual([]);
  });

  it("offline, trains whose scheduled time has passed are removed", () => {
    const rows = [toKandy(1, { time: "2026-10-20T01:30:00Z" }), toKandy(2, { time: "2026-10-20T01:50:00Z" })];
    expect(platformView(rows, "1", NOW, true).next!.serviceNumber).toBe("1002");
    expect(platformView(rows, "1", NOW, false).next!.serviceNumber).toBe("1001");
  });
});

describe("callingPointsAfter", () => {
  it("lists the stops after this station, without the ones it only passes", () => {
    expect(callingPointsAfter(journey(), GAMPAHA)).toEqual(["Polgahawela", "Kandy"]);
  });

  it("leaves out a stop that is skipped", () => {
    const detail = journey();
    detail.stops.find((s) => s.stationId === POLGAHAWELA)!.status = "Skipped";
    expect(callingPointsAfter(detail, GAMPAHA)).toEqual(["Kandy"]);
  });

  it("is empty at the last stop and for a station that is not on the journey", () => {
    expect(callingPointsAfter(journey(), KANDY)).toEqual([]);
    expect(callingPointsAfter(journey(), MARADANA + "x")).toEqual([]);
  });
});

describe("platformDirection", () => {
  it("is the end of the route the train runs along", () => {
    expect(platformDirection(toKandy(1), LINES, GAMPAHA)).toBe("Kandy");
    expect(platformDirection(toFort(1), LINES, GAMPAHA)).toBe("Colombo Fort");
  });

  it("is null without a train, or when the destination is not ahead", () => {
    expect(platformDirection(null, LINES, GAMPAHA)).toBeNull();
    expect(platformDirection(row({ destination: { stationId: GAMPAHA, name: "Gampaha" } }), LINES, GAMPAHA)).toBeNull();
  });
});
