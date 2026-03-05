namespace RestControlAPI.Config
{
    public class TimeSlot
    {
        public string Name { get; set; } = string.Empty;
        public TimeOnly Start { get; set; }
        public TimeOnly End { get; set; }
        public int MaxCapacity { get; set; }

        public bool Contains(TimeOnly time) => time >= Start && time <= End;
    }

    public static class RestaurantCapacityConfig
    {
        public static readonly TimeOnly OpeningTime = new(8, 0);
        public static readonly TimeOnly ClosingTime = new(23, 0);

        public static readonly List<TimeSlot> Slots = new()
        {
            new TimeSlot { Name = "Manhã",  Start = new(8, 0),  End = new(11, 59), MaxCapacity = 30 },
            new TimeSlot { Name = "Almoço", Start = new(12, 0), End = new(14, 59), MaxCapacity = 40 },
            new TimeSlot { Name = "Tarde",  Start = new(15, 0), End = new(17, 59), MaxCapacity = 20 },
            new TimeSlot { Name = "Jantar", Start = new(18, 0), End = new(22, 59), MaxCapacity = 60 },
        };
        public static TimeSlot? GetSlotFor(TimeOnly time)
            => Slots.FirstOrDefault(s => s.Contains(time));
    }
}