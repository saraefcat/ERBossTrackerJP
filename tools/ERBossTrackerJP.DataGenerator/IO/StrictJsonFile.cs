using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERBossTrackerJP.DataGenerator.Generation;

namespace ERBossTrackerJP.DataGenerator.IO;

internal static class StrictJsonFile
{
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
    };

    public static T Read<T>(string path, int maximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var file = new FileInfo(path);

        if (!file.Exists)
        {
            throw new FileNotFoundException("Required JSON input was not found.", file.FullName);
        }

        if (file.Length > maximumBytes)
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.FileTooLarge,
                $"JSON input '{file.FullName}' contains {file.Length} bytes; the maximum is {maximumBytes}.");
        }

        byte[] bytes = File.ReadAllBytes(file.FullName);
        ReadOnlySpan<byte> json = bytes;

        if (json.StartsWith(Encoding.UTF8.Preamble))
        {
            json = json[Encoding.UTF8.Preamble.Length..];
        }

        try
        {
            _ = StrictUtf8.GetCharCount(json);
        }
        catch (DecoderFallbackException exception)
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.InvalidEncoding,
                $"JSON input '{file.FullName}' is not valid UTF-8.",
                innerException: exception);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ??
                throw new LocalizationGenerationException(
                    LocalizationGenerationErrorCode.InvalidJson,
                    $"JSON input '{file.FullName}' has a null root.");
        }
        catch (JsonException exception)
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.InvalidJson,
                $"JSON input '{file.FullName}' is invalid or contains unknown properties.",
                innerException: exception);
        }
    }
}
