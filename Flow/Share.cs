using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow;

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

    public static bool TryRead(string text, out List<FlowItem> flows, out string error)
    {
        flows = [];
        error = string.Empty;
        var body = (text ?? string.Empty).Trim();
        if (body.Length == 0)
        {
            error = "Empty.";
            return false;
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
            error = ex.Message;
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
