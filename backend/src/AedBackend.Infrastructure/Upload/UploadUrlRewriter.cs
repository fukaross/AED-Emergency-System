using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AedBackend.Infrastructure.Upload;

public sealed partial class UploadUrlRewriter
{
    private readonly string oldBase;
    private readonly string newBase;

    public UploadUrlRewriter(string oldBase, string newBase)
    {
        this.oldBase = oldBase.TrimEnd('/');
        this.newBase = newBase.TrimEnd('/');
    }

    public UrlRewriteResult RewriteText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return UrlRewriteResult.Empty(value ?? string.Empty);
        var changed = false;
        var references = new List<UploadUrlReference>();
        var rewritten = UploadUrlPattern().Replace(value, match =>
        {
            var item = ToReference(match.Value);
            references.Add(item);
            if (!item.ShouldMigrate) return match.Value;
            changed = true;
            return $"{newBase}/{item.RelativePath}{item.Suffix}";
        });
        return new UrlRewriteResult(rewritten, changed, references);
    }

    public UrlRewriteResult RewriteJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return UrlRewriteResult.Empty(json ?? string.Empty);
        var root = JsonNode.Parse(json);
        if (root is null) return UrlRewriteResult.Empty(json);
        var references = new List<UploadUrlReference>();
        var changed = RewriteJsonNode(root, references);
        var rewritten = changed ? root.ToJsonString() : json;
        return new UrlRewriteResult(rewritten, changed, references);
    }

    private bool RewriteJsonNode(JsonNode node, List<UploadUrlReference> references)
    {
        return node switch
        {
            JsonObject obj => RewriteJsonObject(obj, references),
            JsonArray arr => RewriteJsonArray(arr, references),
            JsonValue value => RewriteJsonValue(value, references),
            _ => false
        };
    }

    private bool RewriteJsonObject(JsonObject obj, List<UploadUrlReference> references)
    {
        var changed = false;
        foreach (var key in obj.Select(item => item.Key).ToArray())
        {
            var child = obj[key];
            if (child is null) continue;
            changed |= RewriteJsonNode(child, references);
        }
        return changed;
    }

    private bool RewriteJsonArray(JsonArray arr, List<UploadUrlReference> references)
    {
        var changed = false;
        foreach (var child in arr)
        {
            if (child is not null) changed |= RewriteJsonNode(child, references);
        }
        return changed;
    }

    private bool RewriteJsonValue(JsonValue value, List<UploadUrlReference> references)
    {
        if (!value.TryGetValue<string>(out var text)) return false;
        var result = RewriteText(text);
        references.AddRange(result.References);
        if (!result.Changed) return false;
        value.ReplaceWith(JsonValue.Create(result.Value));
        return true;
    }

    private UploadUrlReference ToReference(string value)
    {
        if (value.StartsWith(newBase, StringComparison.OrdinalIgnoreCase))
            return UploadUrlReference.Existing(value, RelativeFromUrl(value, newBase));
        if (value.StartsWith(oldBase, StringComparison.OrdinalIgnoreCase))
            return UploadUrlReference.Migratable(value, RelativeFromUrl(value, oldBase));
        if (value.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return UploadUrlReference.Migratable(value, value.TrimStart('/'));
        return UploadUrlReference.Skipped(value);
    }

    private static string RelativeFromUrl(string value, string baseUrl)
    {
        return value[(baseUrl.TrimEnd('/').Length + 1)..].TrimStart('/');
    }

    internal static (string Path, string Suffix) SplitSuffix(string relative)
    {
        var query = relative.IndexOf('?');
        var fragment = relative.IndexOf('#');
        var split = (query, fragment) switch
        {
            ( >= 0, >= 0) => Math.Min(query, fragment),
            ( >= 0, _) => query,
            (_, >= 0) => fragment,
            _ => -1
        };
        return split < 0 ? (relative, string.Empty) : (relative[..split], relative[split..]);
    }

    [GeneratedRegex(@"(?:(?:https?://[^/""'\s<>\\)]+)?/uploads/[^""'\s<>\\)]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UploadUrlPattern();
}

public sealed record UrlRewriteResult(
    string Value,
    bool Changed,
    IReadOnlyList<UploadUrlReference> References)
{
    public static UrlRewriteResult Empty(string value) => new(value, false, []);
}

public sealed record UploadUrlReference(
    string Value,
    string RelativePath,
    string Suffix,
    bool ShouldMigrate,
    bool AlreadyMigrated)
{
    public static UploadUrlReference Migratable(string value, string path)
    {
        var item = UploadUrlRewriter.SplitSuffix(path);
        return new UploadUrlReference(value, item.Path, item.Suffix, true, false);
    }

    public static UploadUrlReference Existing(string value, string path)
    {
        var item = UploadUrlRewriter.SplitSuffix(path);
        return new UploadUrlReference(value, item.Path, item.Suffix, false, true);
    }

    public static UploadUrlReference Skipped(string value)
    {
        return new UploadUrlReference(value, string.Empty, string.Empty, false, false);
    }
}
