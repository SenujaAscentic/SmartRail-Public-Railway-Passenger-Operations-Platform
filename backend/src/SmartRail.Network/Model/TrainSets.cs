namespace SmartRail.Network.Model;

internal enum Traction { DieselPowerSet, DieselLocoHauled }

internal enum CoachClass { First, Second, Third }

internal enum SeatingType { Reserved, Unreserved }

internal class TrainSet
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";              // "DPS-01"
    public Traction Traction { get; set; }
    public List<Coach> Coaches { get; set; } = [];
}

internal class Coach
{
    public Guid Id { get; set; }
    public Guid TrainSetId { get; set; }
    public int Position { get; set; }                   // place in the train, counted from 1
    public CoachClass Class { get; set; }
    public SeatingType Seating { get; set; }
}
