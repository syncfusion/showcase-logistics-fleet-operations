namespace LogisticsFleetOperations.Infrastructure.Persistence;

/// <summary>
/// Infrastructure-only settings row (not part of the Domain model) recording the dataset/schema
/// version the seed command stamped on its batch, surfaced via GET /api/meta.
/// </summary>
public sealed class DatasetMetaRow
{
    public int Id { get; set; }
    public string DatasetVersion { get; set; } = "";
    public DateTimeOffset ScenarioClockReference { get; set; }
    public DateTimeOffset SeededAt { get; set; }
}
