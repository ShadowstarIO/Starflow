using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Starflow;

// A plain HTTP POST. The body is whatever the card holds, with tokens filled, so it works for Discord
// and for anything else that takes a POST. It runs off the game thread; a failure lands in chat.
internal static class Webhook
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static ActResult Send(Card card, World world)
    {
        if (!WebhookText.ValidUrl(card.Value, out var error)) return new ActResult(false, error);
        var type = string.IsNullOrWhiteSpace(card.Option) ? "application/json" : card.Option.Trim();
        var body = WebhookText.FillBody(card.Extra ?? string.Empty, world, type.Contains("json", StringComparison.OrdinalIgnoreCase));
        var url = card.Value.Trim();
        var label = card.Label;
        _ = Task.Run(async () =>
        {
            try
            {
                using var content = new StringContent(body, Encoding.UTF8, type);
                using var response = await Client.PostAsync(url, content).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    Plugin.Notify($"{label} failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Webhook failed");
                Plugin.Notify($"{label} failed: {ex.Message}");
            }
        });
        return new ActResult(true, "POST " + WebhookText.Host(url));
    }
}
