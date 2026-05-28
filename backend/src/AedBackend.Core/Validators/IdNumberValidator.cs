using System.Text.RegularExpressions;

namespace AedBackend.Core.Validators;

public static partial class IdNumberValidator
{
    [GeneratedRegex(@"^\d{17}[\dXx]$")]
    private static partial Regex IdNumberFormat();

    private static readonly int[] Weights = [7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2];
    private static readonly char[] CheckMap = ['1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2'];

    public static string? Validate(string? idNumber)
    {
        if (string.IsNullOrWhiteSpace(idNumber))
            return "身份证号不能为空";

        var trimmed = idNumber.Trim();
        if (!IdNumberFormat().IsMatch(trimmed))
            return "身份证号格式不正确";

        var sum = 0;
        for (var i = 0; i < 17; i++)
            sum += (trimmed[i] - '0') * Weights[i];

        if (char.ToUpperInvariant(trimmed[17]) != CheckMap[sum % 11])
            return "身份证号校验位不正确";

        return null;
    }
}
