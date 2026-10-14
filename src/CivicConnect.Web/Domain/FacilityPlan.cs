namespace CivicConnect.Web.Domain;

public sealed record FacilityRoom(string Id, string Name, int X, int Y, int Width, int Height);

public static class FacilityPlan
{
    public const string BuildingName = "Riverside Community Centre";

    public const int PlanWidth = 960;
    public const int PlanHeight = 580;

    public static readonly IReadOnlyList<FacilityRoom> Rooms =
    [
        new("workshop", "Workshop", 20, 20, 240, 160),
        new("stores", "Stores", 20, 180, 120, 120),
        new("server-room", "Server room", 140, 180, 120, 120),
        new("hall", "Main hall", 260, 20, 440, 280),
        new("kitchen", "Kitchen", 700, 20, 240, 150),
        new("toilets", "Toilets", 700, 170, 240, 130),
        new("corridor", "Corridor", 20, 300, 920, 60),
        new("reception", "Reception", 20, 360, 260, 180),
        new("office", "Admin office", 280, 360, 200, 180),
        new("meeting-1", "Meeting room 1", 480, 360, 230, 180),
        new("meeting-2", "Meeting room 2", 710, 360, 230, 180)
    ];

    public static FacilityRoom? Find(string? id)
        => id is null ? null : Rooms.FirstOrDefault(room => room.Id == id);

    public static string NameOf(string? id)
        => Find(id)?.Name ?? "Room not recorded";
}
