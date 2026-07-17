namespace AedBackend.Infrastructure.Rescue;

public sealed class RescueOptions
{
    public const string SectionName = "Rescue";

    public int SignalLostAfterSeconds { get; set; } = 45;
    public int LocationGoneAfterSeconds { get; set; } = 90;
    public int LocationReportIntervalSeconds { get; set; } = 5;
}
