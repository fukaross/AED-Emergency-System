using System.Globalization;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Models.WeChat;

namespace AedBackend.Api.Endpoints;

/// <summary>
/// 仪表盘图表的纯聚合逻辑（无 IO，便于单测）。
/// 时间维度统一以 UTC 日期为准，与现有 dashboard 计数口径一致。
/// </summary>
internal static class AdminDashboardChartAggregator
{
    private const int GrowthWindowDays = 30;
    private const int ProcessingWindowDays = 14;
    private const string DateFormat = "yyyy-MM-dd";

    public static AdminDashboardCharts Build(
        IReadOnlyList<ExperienceUserRecord> users,
        IReadOnlyList<ResponderProfileRecord> responders,
        IReadOnlyList<HelpRequestRecord> helps,
        DateTimeOffset nowUtc)
    {
        var today = nowUtc.UtcDateTime.Date;
        return new AdminDashboardCharts(
            BuildUserGrowth(users, today),
            BuildResponderGrowth(responders, today),
            BuildHelpProcessing(helps, today),
            BuildStatusDistribution(helps));
    }

    private static IReadOnlyList<AdminDashboardTrendPoint> BuildUserGrowth(
        IReadOnlyList<ExperienceUserRecord> users,
        DateTime today)
    {
        var dates = users.Select(user => user.CreatedAt.UtcDateTime.Date).ToArray();
        return BuildCumulativeSeries(dates, today);
    }

    /// <summary>急救员增长口径：按证书发证日期(IssueDate)的持证急救员累计；空/无法解析的发证日期显式跳过。</summary>
    private static IReadOnlyList<AdminDashboardTrendPoint> BuildResponderGrowth(
        IReadOnlyList<ResponderProfileRecord> responders,
        DateTime today)
    {
        var dates = responders
            .Select(responder => TryParseDate(responder.IssueDate))
            .Where(date => date.HasValue)
            .Select(date => date!.Value)
            .ToArray();
        return BuildCumulativeSeries(dates, today);
    }

    private static IReadOnlyList<AdminDashboardTrendPoint> BuildCumulativeSeries(
        IReadOnlyList<DateTime> dates,
        DateTime today)
    {
        var points = new List<AdminDashboardTrendPoint>(GrowthWindowDays);
        for (var offset = GrowthWindowDays - 1; offset >= 0; offset--)
        {
            var day = today.AddDays(-offset);
            var total = dates.Count(date => date <= day);
            points.Add(new AdminDashboardTrendPoint(FormatDate(day), total));
        }

        return points;
    }

    private static IReadOnlyList<AdminDashboardProcessingPoint> BuildHelpProcessing(
        IReadOnlyList<HelpRequestRecord> helps,
        DateTime today)
    {
        var points = new List<AdminDashboardProcessingPoint>(ProcessingWindowDays);
        for (var offset = ProcessingWindowDays - 1; offset >= 0; offset--)
        {
            var day = today.AddDays(-offset);
            var dayHelps = helps.Where(help => help.CreatedAt.UtcDateTime.Date == day).ToArray();
            var processed = dayHelps.Count(help => HelpStatuses.IsClosed(help.Status));
            points.Add(new AdminDashboardProcessingPoint(
                FormatDate(day), processed, dayHelps.Length - processed));
        }

        return points;
    }

    private static IReadOnlyList<AdminDashboardStatusSlice> BuildStatusDistribution(
        IReadOnlyList<HelpRequestRecord> helps)
    {
        return helps
            .GroupBy(help => help.Status)
            .Select(group => new AdminDashboardStatusSlice(group.Key, group.Count()))
            .OrderBy(slice => slice.Status, StringComparer.Ordinal)
            .ToArray();
    }

    private static DateTime? TryParseDate(string? value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed.Date
            : null;
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToString(DateFormat, CultureInfo.InvariantCulture);
    }
}
