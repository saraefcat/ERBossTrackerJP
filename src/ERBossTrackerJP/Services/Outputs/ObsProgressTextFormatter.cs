using System.Globalization;
using System.Text;

namespace ERBossTrackerJP.Services.Outputs;

public static class ObsProgressTextFormatter
{
    public const int MaximumFormatLength = 256;
    public const string DefaultFormat =
        "{defeated} / {total} ({percentage}%)";

    public static bool TryValidate(string? format, out string errorMessage) =>
        TryFormat(
            format,
            defeated: 1,
            remaining: 1,
            total: 2,
            percentage: 50.0,
            out _,
            out errorMessage);

    public static string Format(
        string format,
        int defeated,
        int remaining,
        int total,
        double percentage)
    {
        if (!TryFormat(
                format,
                defeated,
                remaining,
                total,
                percentage,
                out string result,
                out string errorMessage))
        {
            throw new ArgumentException(errorMessage, nameof(format));
        }

        return result;
    }

    public static bool TryFormat(
        string? format,
        int defeated,
        int remaining,
        int total,
        double percentage,
        out string result,
        out string errorMessage)
    {
        result = string.Empty;

        if (string.IsNullOrWhiteSpace(format))
        {
            errorMessage = "書式を入力してください。";
            return false;
        }

        if (format.Length > MaximumFormatLength)
        {
            errorMessage = $"書式は{MaximumFormatLength}文字以内で入力してください。";
            return false;
        }

        if (format.Any(char.IsControl))
        {
            errorMessage = "書式に改行や制御文字は使用できません。";
            return false;
        }

        var builder = new StringBuilder(format.Length + 16);
        bool containsVariable = false;

        for (int index = 0; index < format.Length; index++)
        {
            char current = format[index];

            if (current == '{')
            {
                if (index + 1 < format.Length && format[index + 1] == '{')
                {
                    builder.Append('{');
                    index++;
                    continue;
                }

                int closingBrace = format.IndexOf('}', index + 1);

                if (closingBrace < 0)
                {
                    errorMessage = "閉じていない「{」があります。";
                    return false;
                }

                string variable = format[(index + 1)..closingBrace];
                string? value = variable switch
                {
                    "defeated" => defeated.ToString(CultureInfo.InvariantCulture),
                    "remaining" => remaining.ToString(CultureInfo.InvariantCulture),
                    "total" => total.ToString(CultureInfo.InvariantCulture),
                    "percentage" => percentage.ToString(
                        "F1",
                        CultureInfo.InvariantCulture),
                    _ => null,
                };

                if (value is null)
                {
                    errorMessage = $"使用できない変数です: {{{variable}}}";
                    return false;
                }

                builder.Append(value);
                containsVariable = true;
                index = closingBrace;
                continue;
            }

            if (current == '}')
            {
                if (index + 1 < format.Length && format[index + 1] == '}')
                {
                    builder.Append('}');
                    index++;
                    continue;
                }

                errorMessage = "対応する「{」がない「}」があります。";
                return false;
            }

            builder.Append(current);
        }

        if (!containsVariable)
        {
            errorMessage = "少なくとも1つの変数を指定してください。";
            return false;
        }

        result = builder.ToString();
        errorMessage = string.Empty;
        return true;
    }
}
