namespace AedBackend.Infrastructure.Help;

internal static class SeedIdNumberBuilder
{
    private const string Prefix = "46010020000101";
    private static readonly int[] Weights = [7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2];
    private static readonly char[] CheckMap = ['1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2'];

    public static string Build(int index)
    {
        var body = $"{Prefix}{index + 1:D3}";
        var sum = 0;
        for (var i = 0; i < Weights.Length; i++)
            sum += (body[i] - '0') * Weights[i];

        return $"{body}{CheckMap[sum % 11]}";
    }
}
