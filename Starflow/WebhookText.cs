using System;
using System.Text.Json;

namespace Starflow;

// The parts of a webhook card that do not touch the network. No Dalamud types, so they can be tested.
public static class WebhookText
{
    public const string DiscordTemplate = "{\n  \"username\": \"Starflow\",\n  \"content\": \"{name} entered {zone} on {world} as {job}.\"\n}";

    public static bool ValidUrl(string url, out string error)
    {
        error = string.Empty;
        if (!Uri.TryCreate((url ?? string.Empty).Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            error = "Not an http or https address.";
            return false;
        }
        return true;
    }

    public static string Host(string url) =>
        Uri.TryCreate((url ?? string.Empty).Trim(), UriKind.Absolute, out var uri) ? uri.Host : "webhook";

    // Null when the body is fine to send as JSON, or is not meant to be JSON.
    public static string? JsonError(string body, string contentType)
    {
        if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var _ = JsonDocument.Parse(body);
            return null;
        }
        catch (JsonException ex)
        {
            return ex.Message;
        }
    }

    // Token values are escaped for JSON so a name with a quote cannot break the body.
    public static string FillBody(string body, World world, bool json) =>
        Tokens.Fill(body, key =>
        {
            var value = Tokens.Fill("{" + key + "}", world);
            if (value == "{" + key + "}") return null;
            return json ? JsonEncodedText.Encode(value).ToString() : value;
        });
}
