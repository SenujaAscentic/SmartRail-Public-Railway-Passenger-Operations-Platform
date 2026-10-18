namespace SmartRail.LiveOperations.Domain;

// Domain model 4.2. TerminatedEarly comes with terminate (Phase 3b).
public enum JourneyStatus { Planned, Running, Completed, Cancelled }

// Why a journey ended (domain model 8.2): the driver pressed End; the train reached the terminus; the controller ended it.
public enum JourneyEndReason { Driver, Terminus, Controller }

public enum StartResult { Started, TooEarly, WrongState }

// What became of a controller decision (domain model R-18). StaleVersion is checked first: if the journey changed since the
// controller looked, they are told to look again, whatever else is wrong with the request.
// UnknownStop: the station named in the decision is not a stop of this journey.
public enum DecisionResult { Done, StaleVersion, InvalidState, UnknownStop }

// Why the controller did something (delay, cancel): one of the agreed codes and an optional plain-text note. Public (US-P4).
public sealed record JourneyReason(string Code, string? Note);

// A hold the controller put on the train at a station (domain model D-4, R-22). Minutes is the planned length.
public sealed record JourneyHold(Guid StationId, int Minutes, JourneyReason? Reason, DateTime SetAt);

public enum EndResult { Ended, WrongState }

public enum CallType { Stop, Pass }

// Domain model 4.3. Skipped: the controller cancelled the journey before the train got here (skip / unskip of a single stop is Phase 3b).
public enum StopStatus { Scheduled, Arrived, Departed, Passed, Skipped }

// What a crew member taps for a stop.
public enum StopEventKind { Arrival, Departure }

// One stop of a service as the timetable gives it: times are minutes after local midnight of the service date.
public sealed record PlannedStop(
    int Sequence, Guid StationId, CallType CallType,
    int? ArrivalOffset, int? DepartureOffset, string PlannedPlatform);

public sealed class JourneyStop
{
    public int Sequence { get; private set; }
    public Guid StationId { get; private set; }
    public CallType CallType { get; private set; }
    public DateTime? ScheduledArrival { get; private set; }     // UTC
    public DateTime? ScheduledDeparture { get; private set; }   // UTC
    public string PlannedPlatform { get; private set; } = "";

    // What has happened at this stop. Values and their sources are plain columns (not value-object types) so that
    // EF Core maps them without any configuration in the Domain project.
    public StopStatus Status { get; private set; }
    public DateTime? ActualArrival { get; private set; }          // UTC, corrected (R-09)
    public Source? ActualArrivalSource { get; private set; }
    public DateTime? ActualDeparture { get; private set; }
    public Source? ActualDepartureSource { get; private set; }
    public ZonePhase Zone { get; private set; }                   // station-zone detection state (R-04)
    public DateTime? SlowSince { get; private set; }

    public TimeWithSource? ActualArrivalTime =>
        ActualArrival is { } at && ActualArrivalSource is { } source ? new TimeWithSource(at, source) : null;

    public TimeWithSource? ActualDepartureTime =>
        ActualDeparture is { } at && ActualDepartureSource is { } source ? new TimeWithSource(at, source) : null;

    public ZoneState ZoneState => new(Zone, SlowSince);

    // R-02 / R-03: what actually happened wins; otherwise the schedule plus the journey's current delay.
    public DateTime? ExpectedArrival(Delay delay) =>
        ActualArrival ?? (ScheduledArrival is { } scheduled ? ExpectedTimes.Arrival(scheduled, delay) : null);

    public DateTime? ExpectedDeparture(Delay delay) =>
        ActualDeparture ?? (ScheduledDeparture is { } scheduled ? ExpectedTimes.Departure(scheduled, delay) : null);

    internal void SetActualArrival(TimeWithSource time)
    {
        ActualArrival = time.Value;
        ActualArrivalSource = time.Source;
    }

    internal void SetActualDeparture(TimeWithSource time)
    {
        ActualDeparture = time.Value;
        ActualDepartureSource = time.Source;
    }

    // A reinstated journey: the stops that were skipped by the cancellation are ahead of the train again.
    internal void Unskip()
    {
        if (Status == StopStatus.Skipped)
        {
            Status = StopStatus.Scheduled;
        }
    }

    internal void SetZoneState(ZoneState state)
    {
        Zone = state.Phase;
        SlowSince = state.SlowSince;
    }

    // Domain model 4.3: a status only moves forward. A GPS "Arrived" after a crew tap already said Departed is ignored.
    internal bool TryMoveTo(StopStatus next)
    {
        var allowed = next switch
        {
            StopStatus.Arrived => Status == StopStatus.Scheduled,
            StopStatus.Departed => Status is StopStatus.Scheduled or StopStatus.Arrived,
            StopStatus.Passed => Status == StopStatus.Scheduled,
            StopStatus.Skipped => Status == StopStatus.Scheduled,
            _ => false,
        };
        if (allowed)
        {
            Status = next;
        }
        return allowed;
    }

    private JourneyStop() { }   // for EF Core, which fills the properties itself

    internal JourneyStop(int sequence, Guid stationId, CallType callType, DateTime? arrival, DateTime? departure, string plannedPlatform)
    {
        Sequence = sequence;
        StationId = stationId;
        CallType = callType;
        ScheduledArrival = arrival;
        ScheduledDeparture = departure;
        PlannedPlatform = plannedPlatform;
    }
}

// One run of a service on one date (domain model section 4.1).
public sealed class OperationalJourney
{
    private readonly List<JourneyStop> _stops = [];
    private readonly List<IDomainEvent> _events = [];

    public Guid Id { get; private set; }
    public Guid ServiceId { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public Guid TrainSetId { get; private set; }
    public Guid LineId { get; private set; }   // for the event stream: lets a client filter by line (domain model 8.1)

    // US-C2 AC2 and contract 3.4: "Start journey" is allowed from 60 minutes before the scheduled departure.
    public static readonly TimeSpan StartsBefore = TimeSpan.FromMinutes(60);

    // The driver pressed Start on the cab tablet (domain model 4.2: Planned -> Running only from the cab tablet; the slice has
    // already checked the caller is the assigned driver with a cab session). Starting is not a controller decision, so it does
    // not touch the control version (R-18). Tracking stays NotStarted until the first position arrives (domain model 4.4).
    public StartResult Start(DateTime now, Guid deviceId, Guid crewSessionId)
    {
        if (Status != JourneyStatus.Planned)
        {
            return StartResult.WrongState;
        }

        var first = _stops.OrderBy(s => s.Sequence).First();
        if ((first.ScheduledDeparture ?? first.ScheduledArrival) is { } departure && now < departure - StartsBefore)
        {
            return StartResult.TooEarly;
        }

        Status = JourneyStatus.Running;
        _events.Add(new JourneyStarted(Id, now, deviceId, crewSessionId));
        return StartResult.Started;
    }

    // Announces where the train is (event TrainPositionUpdated). Called by the position slice once per accepted batch, with the latest
    // accepted fix. The position itself was recorded by RecordPosition; this keeps the announced fields next to it and raises the event.
    public void AnnouncePosition(DateTime at, GeoPoint location, double speedKmh, Guid lastStationId, Guid nextStationId, double fractionToNext)
    {
        var fraction = Math.Clamp(fractionToNext, 0, 1);
        (LastSpeedKmh, LastStationId, NextStationId, FractionToNext) = (speedKmh, lastStationId, nextStationId, fraction);
        _events.Add(new TrainPositionUpdated(Id, at, location, speedKmh, lastStationId, nextStationId, fraction));
    }

    // The driver pressed End (domain model 4.2: Running -> Completed). The data is over: tracking becomes Ended (R-06 stops ageing it).
    public EndResult End(DateTime now)
    {
        if (Status != JourneyStatus.Running)
        {
            return EndResult.WrongState;
        }

        Status = JourneyStatus.Completed;
        _events.Add(new JourneyEnded(Id, now, JourneyEndReason.Driver));
        SetTracking(TrackingStatus.Ended, now);
        return EndResult.Ended;
    }

    // The driver who runs this journey: the id of their account, which is the `sub` of their tokens. Null while none is assigned.
    // R-07 accepts positions only from this driver. The id comes from service-assignments.json (staff number -> account).
    public Guid? DriverId { get; private set; }

    // Sets the driver only where none is set; a driver already assigned is never replaced here. Returns whether it was set.
    public bool AssignDriver(Guid driverId)
    {
        if (DriverId is not null)
        {
            return false;
        }

        DriverId = driverId;
        return true;
    }

    // R-18. Only DECISIONS bump it (delay, hold, cancel, ...), never GPS, detection, crew taps or ETA recalculation.
    public int ControlVersion { get; private set; }

    // Who made the last decision and when: so a refused, stale decision can say "Changed by <name> a moment ago" (US-O4 AC6).
    public Guid? LastDecisionBy { get; private set; }
    public DateTime? LastDecisionAt { get; private set; }

    // The reason of the delay the controller set (cleared with it). Two plain columns, see JourneyStop.
    public string? ReasonCode { get; private set; }
    public string? ReasonNote { get; private set; }
    public JourneyReason? Reason => ReasonCode is null ? null : new JourneyReason(ReasonCode, ReasonNote);

    // Every decision calls this once it is accepted: count it and remember who made it. (The version was checked by the caller.)
    private void CountDecision(Guid actorId, DateTime now)
    {
        ControlVersion++;
        LastDecisionBy = actorId;
        LastDecisionAt = now;
    }

    private bool IsOpen => Status is JourneyStatus.Planned or JourneyStatus.Running;

    // The hold, if any: plain columns like everything else (see JourneyStop).
    public Guid? HoldStationId { get; private set; }
    public int? HoldMinutes { get; private set; }
    public string? HoldReasonCode { get; private set; }
    public string? HoldReasonNote { get; private set; }
    public DateTime? HoldSetAt { get; private set; }

    public JourneyHold? Hold => HoldStationId is { } station
        ? new JourneyHold(station, HoldMinutes!.Value, HoldReasonCode is null ? null : new JourneyReason(HoldReasonCode, HoldReasonNote), HoldSetAt!.Value)
        : null;

    // US-O4: hold the train at a station it has not left yet. Only one hold at a time; the terminus has no departure to hold.
    public DecisionResult SetHoldByController(int expectedVersion, Guid stationId, int minutes, JourneyReason? reason, Guid actorId, DateTime now)
    {
        if (expectedVersion != ControlVersion)
        {
            return DecisionResult.StaleVersion;
        }

        var stop = _stops.FirstOrDefault(s => s.StationId == stationId);
        if (stop is null)
        {
            return DecisionResult.UnknownStop;
        }

        if (!IsOpen || Hold is not null || stop.Status is not (StopStatus.Scheduled or StopStatus.Arrived) || stop.ScheduledDeparture is null)
        {
            return DecisionResult.InvalidState;
        }

        CountDecision(actorId, now);
        (HoldStationId, HoldMinutes, HoldSetAt) = (stationId, minutes, now);
        (HoldReasonCode, HoldReasonNote) = (reason?.Code, reason?.Note);
        _events.Add(new JourneyHeld(Id, stationId, Released: false, minutes, reason, Source.Controller, now));
        return DecisionResult.Done;
    }

    // R-14: cancel a Planned or Running journey. Every stop the train has not reached becomes Skipped; a stop it is standing at keeps
    // Arrived (that is what happened). A hold ends. The data is over, so tracking ends. The reason is public (US-P4).
    public DecisionResult CancelByController(int expectedVersion, JourneyReason reason, Guid actorId, DateTime now)
    {
        if (expectedVersion != ControlVersion)
        {
            return DecisionResult.StaleVersion;
        }

        if (!IsOpen)
        {
            return DecisionResult.InvalidState;
        }

        CountDecision(actorId, now);
        Status = JourneyStatus.Cancelled;
        (ReasonCode, ReasonNote) = (reason.Code, reason.Note);
        foreach (var stop in _stops)
        {
            stop.TryMoveTo(StopStatus.Skipped);   // only Scheduled stops move; the others are history
        }

        if (Hold is not null)
        {
            EndHold(Source.Controller, now);
        }

        _events.Add(new JourneyCancelled(Id, reason, now));
        SetTracking(TrackingStatus.Ended, now);
        return DecisionResult.Done;
    }

    // R-14: reinstate a Cancelled journey, only before its scheduled start. It becomes Planned again and the driver starts it as usual.
    public DecisionResult ReinstateByController(int expectedVersion, Guid actorId, DateTime now)
    {
        if (expectedVersion != ControlVersion)
        {
            return DecisionResult.StaleVersion;
        }

        var first = _stops.OrderBy(s => s.Sequence).First();
        var start = first.ScheduledDeparture ?? first.ScheduledArrival;
        if (Status != JourneyStatus.Cancelled || (start is { } scheduledStart && now >= scheduledStart))
        {
            return DecisionResult.InvalidState;
        }

        CountDecision(actorId, now);
        Status = JourneyStatus.Planned;
        (ReasonCode, ReasonNote) = (null, null);
        foreach (var stop in _stops)
        {
            stop.Unskip();
        }

        _events.Add(new JourneyReinstated(Id, now));
        SetTracking(TrackingStatus.NotStarted, now);
        return DecisionResult.Done;
    }

    // The controller lets the train go.
    public DecisionResult ReleaseHoldByController(int expectedVersion, Guid actorId, DateTime now)
    {
        if (expectedVersion != ControlVersion)
        {
            return DecisionResult.StaleVersion;
        }

        if (!IsOpen || Hold is null)
        {
            return DecisionResult.InvalidState;
        }

        CountDecision(actorId, now);
        EndHold(Source.Controller, now);
        return DecisionResult.Done;
    }

    private void EndHold(Source source, DateTime at)
    {
        var station = HoldStationId!.Value;
        (HoldStationId, HoldMinutes, HoldSetAt, HoldReasonCode, HoldReasonNote) = (null, null, null, null, null);
        _events.Add(new JourneyHeld(Id, station, Released: true, null, null, source, at));
    }

    // R-22: while held, the expected departure from that station is max(scheduled, now), so the delay grows with the clock until the
    // train goes. Stored with source Schedule like the overdue delay (R-23); a controller delay still wins (R-05).
    private void ApplyHoldDelay(DateTime now)
    {
        if (Hold is not { } hold)
        {
            return;
        }

        var scheduled = _stops.First(s => s.StationId == hold.StationId).ScheduledDeparture!.Value;
        var held = Delay.Between(scheduled, now);
        if (held.Minutes > CurrentDelay.Value.Minutes)
        {
            SetDelay(CurrentDelay.Apply(held, Source.Schedule), now);
        }
    }

    // US-O4: the controller sets the delay. R-05: it wins over crew and GPS until cleared. The event is sent even if the minutes are
    // unchanged, because the reason is news.
    public DecisionResult SetDelayByController(int expectedVersion, Delay delay, JourneyReason reason, Guid actorId, DateTime now)
    {
        if (expectedVersion != ControlVersion)
        {
            return DecisionResult.StaleVersion;
        }

        if (!IsOpen)
        {
            return DecisionResult.InvalidState;
        }

        CountDecision(actorId, now);
        (ReasonCode, ReasonNote) = (reason.Code, reason.Note);
        SetDelay(CurrentDelay.Apply(delay, Source.Controller), now, reason, announceAlways: true);
        return DecisionResult.Done;
    }

    // Clears the controller's delay (see ClearControllerDelay). Refused when there is none to clear.
    public DecisionResult ClearDelayByController(int expectedVersion, Guid actorId, DateTime now)
    {
        if (expectedVersion != ControlVersion)
        {
            return DecisionResult.StaleVersion;
        }

        if (!IsOpen || DelaySource != Source.Controller)
        {
            return DecisionResult.InvalidState;
        }

        CountDecision(actorId, now);
        (ReasonCode, ReasonNote) = (null, null);
        SetDelay(CurrentDelay.ClearController(DelayFromLatestStopEvent()), now, reason: null, announceAlways: true);
        return DecisionResult.Done;
    }
    public JourneyStatus Status { get; private set; }
    public IReadOnlyList<JourneyStop> Stops => _stops;

    // Current delay and who set it (R-01, R-05): two plain columns, see JourneyStop.
    public int DelayMinutes { get; private set; }
    public Source DelaySource { get; private set; } = Source.Schedule;
    public SourcedDelay CurrentDelay => new(new Delay(DelayMinutes), DelaySource);

    // Data freshness (R-06) and the last accepted position, needed to judge the next one (R-08).
    public TrackingStatus Tracking { get; private set; }
    public DateTime? LastPositionAt { get; private set; }   // UTC, corrected
    public double? LastLatitude { get; private set; }
    public double? LastLongitude { get; private set; }

    // The last position that was announced (TrainPositionUpdated): speed, the stations around the train and how far it is between
    // them. Kept so the read endpoints can show the same fields without the apps computing anything. All null until one is announced.
    public double? LastSpeedKmh { get; private set; }
    public Guid? LastStationId { get; private set; }
    public Guid? NextStationId { get; private set; }
    public double? FractionToNext { get; private set; }

    // Events raised since the last save. The saving slice writes them to the outbox, then calls ClearDomainEvents.
    public IReadOnlyList<IDomainEvent> DomainEvents => _events;
    public void ClearDomainEvents() => _events.Clear();

    // A position from the cab tablet (R-08, R-09, R-04, R-06). Returns why it was discarded, or Accepted.
    // The caller has already checked R-07 (registered device, active cab session, journey running).
    public PositionVerdict RecordPosition(PositionSample sample, DateTime now)
    {
        var eventTime = ClockCorrection.EventTime(sample.GpsTime, sample.DeviceTime, sample.ClockOffset);   // R-09

        AcceptedPosition? last = LastPositionAt is { } at && LastLatitude is { } lat && LastLongitude is { } lon
            ? new AcceptedPosition(new GeoPoint(lat, lon), at)
            : null;
        var verdict = PositionPlausibility.Check(last, sample.Location, eventTime, sample.DistanceFromRouteMetres);   // R-08
        if (verdict != PositionVerdict.Accepted)
        {
            return verdict;   // discarded: nothing changes and nothing is announced
        }

        LastPositionAt = eventTime;
        LastLatitude = sample.Location.Latitude;
        LastLongitude = sample.Location.Longitude;
        SetTracking(TrackingStatus.Live, now);   // R-06: any accepted position means Live

        // R-04 for every stop the slice gave a distance for. _stops is in sequence order, so events come out in order.
        foreach (var stop in _stops)
        {
            if (stop.Zone == ZonePhase.Finished || !sample.DistanceToStationsMetres.TryGetValue(stop.StationId, out var metres))
            {
                continue;
            }

            var step = StationZone.Observe(stop.ZoneState, new ZoneSample(eventTime, metres, sample.SpeedKmh));
            stop.SetZoneState(step.State);
            switch (step.Event)
            {
                case ZoneEvent.Arrived: RecordArrival(stop, step.EventTime!.Value, Source.Gps); break;
                case ZoneEvent.Departed: RecordDeparture(stop, step.EventTime!.Value, Source.Gps); break;
                case ZoneEvent.Passed: RecordPassed(stop, step.EventTime!.Value, Source.Gps); break;
            }
        }

        return PositionVerdict.Accepted;
    }

    // A crew tap (source = Crew). occurredAt is already corrected (R-09).
    public void RecordCrewTime(Guid stationId, StopEventKind kind, DateTime occurredAt)
    {
        var stop = _stops.FirstOrDefault(s => s.StationId == stationId)
            ?? throw new ArgumentException("The station is not a stop of this journey.", nameof(stationId));

        if (kind == StopEventKind.Arrival)
        {
            RecordArrival(stop, occurredAt, Source.Crew);
        }
        else
        {
            RecordDeparture(stop, occurredAt, Source.Crew);
        }
    }

    public void SetControllerDelay(Delay delay, DateTime now) =>
        SetDelay(CurrentDelay.Apply(delay, Source.Controller), now);

    // R-05: the controller override stays until cleared. Clearing recalculates from the latest recorded stop time
    // (R-01), not 0, so the apps never show a false "On time" (US-P4) or trigger a wrong push (R-16).
    public void ClearControllerDelay(DateTime now) =>
        SetDelay(CurrentDelay.ClearController(DelayFromLatestStopEvent()), now);

    // R-06 and R-23: call periodically (at least every 30 s, so lateness grows within a minute); positions arriving
    // move the status to Live by themselves. Does nothing for a journey that was never started or has ended.
    public void RefreshTracking(DateTime now)
    {
        if (Tracking is TrackingStatus.Live or TrackingStatus.Stale or TrackingStatus.Lost && LastPositionAt is { } lastSeen)
        {
            SetTracking(TrackingFreshness.Of(lastSeen, now), now);
            ApplyOverdueDelay(now);
            ApplyHoldDelay(now);
        }
    }

    // R-23. Stored with source Schedule, because it is worked out from the timetable and the clock. A controller delay
    // is kept by SourcedDelay.Apply (R-05); the next real stop time replaces this one (R-01).
    private void ApplyOverdueDelay(DateTime now)
    {
        var next = _stops.FirstOrDefault(s => s.Status == StopStatus.Scheduled && s.ScheduledArrival is not null);
        if (next is null)
        {
            return;
        }

        var overdue = ExpectedTimes.OverdueDelay(next.ScheduledArrival!.Value, CurrentDelay.Value, now);
        if (overdue != CurrentDelay.Value)
        {
            SetDelay(CurrentDelay.Apply(overdue, Source.Schedule), now);
        }
    }

    // A pass has one time; it is stored as both arrival and departure so the stop reads like any other. The delay is
    // measured against the scheduled arrival, or the departure for a stop that has none (R-01).
    private void RecordPassed(JourneyStop stop, DateTime at, Source source)
    {
        InferEarlierStops(stop, at, source);
        var previousLatest = LatestStopEventTime(except: stop);

        var incoming = new TimeWithSource(at, source);
        var resolved = TimeWithSource.Resolve(stop.ActualArrivalTime, incoming);   // R-05
        stop.SetActualArrival(resolved);
        stop.SetActualDeparture(TimeWithSource.Resolve(stop.ActualDepartureTime, incoming));
        ChangeStatus(stop, StopStatus.Passed, at, source);

        if (resolved == incoming && (stop.ScheduledArrival ?? stop.ScheduledDeparture) is { } scheduled)
        {
            FollowDelay(Delay.Between(scheduled, at), source, at, previousLatest);
        }
    }

    private void RecordArrival(JourneyStop stop, DateTime at, Source source)
    {
        InferEarlierStops(stop, at, source);
        var previousLatest = LatestStopEventTime(except: stop);

        var incoming = new TimeWithSource(at, source);
        var resolved = TimeWithSource.Resolve(stop.ActualArrivalTime, incoming);   // R-05: protects this stop's own time
        stop.SetActualArrival(resolved);
        ChangeStatus(stop, StopStatus.Arrived, at, source);

        // Only the time that won at this stop counts (R-01).
        if (resolved == incoming && stop.ScheduledArrival is { } scheduled)
        {
            FollowDelay(Delay.Between(scheduled, at), source, at, previousLatest);
        }
    }

    private void RecordDeparture(JourneyStop stop, DateTime at, Source source)
    {
        InferEarlierStops(stop, at, source);
        var previousLatest = LatestStopEventTime(except: stop);

        var incoming = new TimeWithSource(at, source);
        var resolved = TimeWithSource.Resolve(stop.ActualDepartureTime, incoming);
        stop.SetActualDeparture(resolved);
        ChangeStatus(stop, StopStatus.Departed, at, source);

        if (resolved == incoming && stop.ScheduledDeparture is { } scheduled)
        {
            FollowDelay(Delay.Between(scheduled, at), source, at, previousLatest);
        }
    }

    // A stop that gets an actual time means the train is past every earlier stop (R-04, inference). Those still
    // Scheduled get no time (none is known) and no delay. A Stop call type becomes Departed, the origin included:
    // "Passed" at a scheduled stop would tell passengers the train did not stop there. Only a Pass call type becomes
    // Passed. Without this, a missed GPS detection would leave R-23 measuring against a station the train has already
    // gone through, and the delay would grow forever.
    private void InferEarlierStops(JourneyStop reached, DateTime at, Source source)
    {
        foreach (var earlier in _stops.Where(s => s.Sequence < reached.Sequence && s.Status == StopStatus.Scheduled))
        {
            earlier.SetZoneState(new ZoneState(ZonePhase.Finished, null));   // GPS must not detect it again later
            var inferred = earlier.CallType == CallType.Stop ? StopStatus.Departed : StopStatus.Passed;
            ChangeStatus(earlier, inferred, at, source);
        }
    }

    // R-01 / R-05: the delay follows the most recent stop event. A late-delivered older event (a queued crew tap) records
    // its stop time but does not pull the delay back. A controller delay is kept by SourcedDelay.Apply.
    private void FollowDelay(Delay delay, Source source, DateTime at, DateTime? previousLatest)
    {
        if (previousLatest is { } latest && at < latest)
        {
            return;
        }

        SetDelay(CurrentDelay.Apply(delay, source), at);
    }

    // The latest recorded time at any other stop. The stop being updated is left out: its old time is being replaced.
    private DateTime? LatestStopEventTime(JourneyStop except) =>
        _stops.Where(s => s != except).SelectMany(s => new[] { s.ActualArrival, s.ActualDeparture }).Max();

    private void ChangeStatus(JourneyStop stop, StopStatus to, DateTime at, Source source)
    {
        var from = stop.Status;
        if (stop.TryMoveTo(to))
        {
            _events.Add(new StopStatusChanged(Id, stop.StationId, stop.Sequence, from, to, at, source));

            // The train has left the station it was held at: the hold is over. Not a decision, so the control version stays (R-18).
            if (to is StopStatus.Departed or StopStatus.Passed && HoldStationId == stop.StationId)
            {
                EndHold(source, at);
            }
        }
    }

    // Stores the delay; announces it only when the whole minutes changed (a change of source alone is not news).
    // `announceAlways` is for the controller's decisions, whose reason is news even when the minutes stay the same.
    private void SetDelay(SourcedDelay next, DateTime at, JourneyReason? reason = null, bool announceAlways = false)
    {
        var previous = CurrentDelay;
        DelayMinutes = next.Value.Minutes;
        DelaySource = next.Source;
        if (announceAlways || next.Value != previous.Value)
        {
            _events.Add(new DelayChanged(Id, next.Value.Minutes, previous.Value.Minutes, next.Source, at, reason));
        }
    }

    private void SetTracking(TrackingStatus next, DateTime now)
    {
        if (next != Tracking)
        {
            _events.Add(new DataFreshnessChanged(Id, Tracking, next, LastPositionAt, now));
            Tracking = next;
        }
    }

    // R-01: the delay at the most recent stop event, with that event's source; on schedule only if there is none.
    private SourcedDelay DelayFromLatestStopEvent()
    {
        var latest = SourcedDelay.FromSchedule;
        DateTime latestAt = DateTime.MinValue;

        void Consider(DateTime? scheduled, TimeWithSource? actual)
        {
            if (scheduled is { } s && actual is { } a && a.Value > latestAt)
            {
                latestAt = a.Value;
                latest = new SourcedDelay(Delay.Between(s, a.Value), a.Source);
            }
        }

        foreach (var stop in _stops)
        {
            Consider(stop.ScheduledArrival, stop.ActualArrivalTime);
            Consider(stop.ScheduledDeparture, stop.ActualDepartureTime);
        }
        return latest;
    }

    private OperationalJourney() { }   // for EF Core

    // Creates the journey for a service on a service date. The timetable's offsets are minutes after midnight on that
    // date in the given time zone (Asia/Colombo); they are converted to UTC here, once, and never again.
    public static OperationalJourney Plan(
        Guid id, Guid serviceId, DateOnly serviceDate, Guid trainSetId,
        IEnumerable<PlannedStop> stops, TimeZoneInfo timeZone, Guid lineId, Guid? driverId = null)
    {
        var journey = new OperationalJourney
        {
            Id = id,
            ServiceId = serviceId,
            ServiceDate = serviceDate,
            TrainSetId = trainSetId,
            LineId = lineId,
            DriverId = driverId,
            Status = JourneyStatus.Planned,
        };

        foreach (var stop in stops.OrderBy(s => s.Sequence))
        {
            journey._stops.Add(new JourneyStop(
                stop.Sequence, stop.StationId, stop.CallType,
                ToUtc(serviceDate, stop.ArrivalOffset, timeZone),
                ToUtc(serviceDate, stop.DepartureOffset, timeZone),
                stop.PlannedPlatform));
        }

        return journey;
    }

    // Local midnight of the service date plus the offset, as a wall-clock time in the zone, converted to UTC.
    // An offset above 1440 (an overnight run) simply lands on the next calendar day, still in this journey.
    private static DateTime? ToUtc(DateOnly serviceDate, int? offsetMinutes, TimeZoneInfo timeZone)
    {
        if (offsetMinutes is null)
        {
            return null;
        }

        var local = serviceDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified).AddMinutes(offsetMinutes.Value);
        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}
