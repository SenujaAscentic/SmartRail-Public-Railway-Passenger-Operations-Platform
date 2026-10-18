namespace SmartRail.LiveOperations.Domain;

// Only what daily generation (US-B2) needs. Delay, hold, tracking and the other statuses come with their own rules.
public enum JourneyStatus { Planned }

public enum CallType { Stop, Pass }

// Domain model 4.3. Skipped comes with the skip / cancel decisions.
public enum StopStatus { Scheduled, Arrived, Departed, Passed }

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

    // R-18. Only DECISIONS bump it (delay, hold, cancel, ...), never GPS, detection, crew taps or ETA recalculation. No decision
    // exists yet, so it stays 0; it is here so the API can show it from the first day (apps' generated types must not change later).
    public int ControlVersion { get; private set; }
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
        }
    }

    // Stores the delay; announces it only when the whole minutes changed (a change of source alone is not news).
    private void SetDelay(SourcedDelay next, DateTime at)
    {
        var previous = CurrentDelay;
        DelayMinutes = next.Value.Minutes;
        DelaySource = next.Source;
        if (next.Value != previous.Value)
        {
            _events.Add(new DelayChanged(Id, next.Value.Minutes, previous.Value.Minutes, next.Source, at));
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
        IEnumerable<PlannedStop> stops, TimeZoneInfo timeZone, Guid lineId)
    {
        var journey = new OperationalJourney
        {
            Id = id,
            ServiceId = serviceId,
            ServiceDate = serviceDate,
            TrainSetId = trainSetId,
            LineId = lineId,
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
