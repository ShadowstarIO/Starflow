using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Starflow;

public static class Share
{
    private static readonly JsonSerializerOptions Opt = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string One(FlowItem flow) => JsonSerializer.Serialize(flow, Opt);

    public static string All(IEnumerable<FlowItem> flows) => JsonSerializer.Serialize(flows, Opt);

    public const string CodePrefix = "SF1.";

    private const string Damaged = "That share code is cut short or damaged. Copy the whole line.";

    private static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    // One line for a post or a message: SF1. then the flows, compressed. Webhook URLs are left blank,
    // since they work like passwords; blanked counts how many were taken out.
    public static string Code(IEnumerable<FlowItem> flows, out int blanked)
    {
        blanked = 0;
        var list = new List<FlowItem>();
        foreach (var flow in flows)
        {
            if (!TryRead(One(flow), out var copy, out _)) continue;
            foreach (var card in copy[0].Cards)
            {
                if (card.Type != CardType.Webhook || string.IsNullOrWhiteSpace(card.Value)) continue;
                card.Value = string.Empty;
                blanked++;
            }
            list.Add(copy[0]);
        }
        var json = list.Count == 1 ? JsonSerializer.Serialize(list[0], Compact) : JsonSerializer.Serialize(list, Compact);
        var raw = Encoding.UTF8.GetBytes(json);
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.SmallestSize, true)) gz.Write(raw, 0, raw.Length);
        return CodePrefix + Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string? Unpack(string code)
    {
        try
        {
            var text = code.Trim()[CodePrefix.Length..].Replace('-', '+').Replace('_', '/');
            while (text.Length % 4 != 0) text += "=";
            using var input = new MemoryStream(Convert.FromBase64String(text));
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gz.CopyTo(output);
            return Encoding.UTF8.GetString(output.ToArray());
        }
        catch
        {
            return null;
        }
    }

    public static bool TryRead(string text, out List<FlowItem> flows, out string error)
    {
        flows = [];
        error = string.Empty;
        var body = (text ?? string.Empty).Trim();
        var fromCode = false;
        if (body.Length == 0)
        {
            error = "Empty.";
            return false;
        }
        if (body.StartsWith(CodePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var unpacked = Unpack(body);
            if (unpacked is null)
            {
                error = Damaged;
                return false;
            }
            body = unpacked.Trim();
            fromCode = true;
        }

        try
        {
            if (body.StartsWith('['))
                flows = JsonSerializer.Deserialize<List<FlowItem>>(body, Opt) ?? [];
            else
            {
                var one = JsonSerializer.Deserialize<FlowItem>(body, Opt);
                if (one is not null) flows.Add(one);
            }
        }
        catch (Exception ex)
        {
            error = fromCode ? Damaged : ex.Message;
            return false;
        }

        if (flows.Count == 0)
        {
            error = "No flow in that text.";
            return false;
        }

        foreach (var flow in flows)
        {
            if (string.IsNullOrWhiteSpace(flow.Id)) flow.Id = Guid.NewGuid().ToString("N");
            flow.Cards ??= [];
            foreach (var card in flow.Cards)
            {
                if (string.IsNullOrWhiteSpace(card.Id)) card.Id = Guid.NewGuid().ToString("N");
                if (card.Repeat <= 0) card.Repeat = 1;
            }
        }
        return true;
    }
}
