using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow;

internal sealed class Engine
{
    private readonly Configuration config;
    private readonly Dictionary<string, DateTime> trueSince = new(StringComparer.Ordinal);
    private readonly HashSet<string> active = new(StringComparer.Ordinal);
    private readonly HashSet<string> held = new(StringComparer.Ordinal);
    private readonly HashSet<string> accepted = new(StringComparer.Ordinal);
    private readonly List<string> pending = [];
    private readonly Dictionary<string, Slot> slots = new(StringComparer.Ordinal);
    private readonly List<Run> runs = [];
    private DateTime nextPoll;

    public Engine(Configuration config) => this.config = config;

    public World Last { get; private set; } = new();
    public IReadOnlyList<string> Pending => pending;

    public bool IsActive(string id) => active.Contains(id);

    public void Reset()
    {
        foreach (var id in active.ToArray())
        {
            var flow = config.Flows.Find(f => f.Id == id);
            if (flow is not null) Stop(flow, true);
        }
        active.Clear();
        held.Clear();
        accepted.Clear();
        pending.Clear();
        runs.Clear();
        trueSince.Clear();
        Last = new World();
    }

    public void Tick(bool force)
    {
        var now = DateTime.Now;
        if (!force && now < nextPoll)
        {
            if (!Quiet(Last)) Pump(now);
            return;
        }
        nextPoll = now.AddSeconds(Math.Max(0.15f, config.PollSec));
        Last = World.Capture();
        if (!config.Enabled || !Last.LoggedIn)
        {
            if (active.Count > 0) Reset();
            return;
        }
        if (Quiet(Last)) return;
        Pump(now);

        foreach (var flow in Ordered())
        {
            if (!flow.Enabled || flow.Mode == RunMode.Off)
            {
                if (active.Contains(flow.Id)) Stop(flow, true);
                continue;
            }

            var when = flow.In(Section.When).Count > 0 && Eval.SectionMatches(flow, Section.When, Last);
            if (!when)
            {
                trueSince.Remove(flow.Id);
                held.Remove(flow.Id);
                accepted.Remove(flow.Id);
                pending.Remove(flow.Id);
                if (active.Contains(flow.Id)) Stop(flow, true);
                continue;
            }

            if (!trueSince.ContainsKey(flow.Id)) trueSince[flow.Id] = now;
            var ready = flow.HoldSec <= 0 || (now - trueSince[flow.Id]).TotalSeconds >= flow.HoldSec;
            if (!ready) continue;

            if (active.Contains(flow.Id))
            {
                if (flow.In(Section.Until).Count > 0 && Eval.SectionMatches(flow, Section.Until, Last))
                {
                    held.Add(flow.Id);
                    Stop(flow, true);
                }
                continue;
            }

            if (held.Contains(flow.Id)) continue;

            if (flow.Mode == RunMode.Ask)
            {
                if (accepted.Contains(flow.Id)) Start(flow, true);
                else if (!pending.Contains(flow.Id)) pending.Add(flow.Id);
                continue;
            }

            Start(flow, true);
        }
    }

    public void Accept(string id)
    {
        pending.Remove(id);
        accepted.Add(id);
        held.Remove(id);
        Tick(true);
    }

    public void Decline(string id)
    {
        pending.Remove(id);
        held.Add(id);
    }

    public void RunOnce(FlowItem flow)
    {
        if (!Last.LoggedIn) Last = World.Capture();
        Enqueue(flow, Section.Do, false, false);
    }

    public void Stop(FlowItem flow, bool quiet)
    {
        active.Remove(flow.Id);
        runs.RemoveAll(r => r.FlowId == flow.Id && r.Phase == "do");
        var after = flow.In(Section.After);
        if (after.Count == 0) Release(flow);
        else Enqueue(flow, Section.After, true, true);
        if (!quiet) Plugin.Notify($"Stopped {flow.Name}");
    }

    public IEnumerable<string> Why(FlowItem flow) => Eval.Explain(flow, Last.LoggedIn ? Last : World.Capture());

    public IEnumerable<string> Dry(FlowItem flow)
    {
        var i = 0;
        foreach (var card in flow.In(Section.Do))
        {
            var times = card.Type == CardType.Wait ? 1 : Math.Clamp(card.Repeat, 1, 30);
            yield return $"{Catalog.Key(Section.Do, i)}  {card.Label}" + (times > 1 ? $"  ×{times}" : string.Empty);
            i++;
        }
        if (i == 0) yield return "Do is empty.";
    }

    public IEnumerable<string> ActiveNames()
    {
        foreach (var flow in config.Flows)
        {
            if (active.Contains(flow.Id)) yield return flow.Name;
        }
    }

    private void Start(FlowItem flow, bool announce)
    {
        if (active.Contains(flow.Id)) return;
        active.Add(flow.Id);
        Enqueue(flow, Section.Do, false, true);
        if (announce && config.NotifyInChat) Plugin.Notify($"Started {flow.Name}");
    }

    private void Enqueue(FlowItem flow, Section section, bool releaseAfter, bool slot)
    {
        runs.RemoveAll(r => r.FlowId == flow.Id && r.Phase == section.ToString());
        var run = new Run { FlowId = flow.Id, Phase = section.ToString(), ReleaseAfter = releaseAfter, Slot = slot };
        var delay = 0;
        var start = DateTime.Now;
        foreach (var card in flow.In(section))
        {
            if (card.Type == CardType.Wait)
            {
                delay += Math.Max(0, card.WaitMs);
                continue;
            }

            var times = Math.Clamp(card.Repeat <= 0 ? 1 : card.Repeat, 1, 30);
            var gap = Math.Max(0, card.WaitMs);
            for (var i = 0; i < times; i++)
            {
                run.Items.Add(new Work { Due = start.AddMilliseconds(delay), Card = card });
                if (i < times - 1) delay += gap;
            }
            if (gap > 0 && times >= 1) delay += gap;
        }
        if (run.Items.Count == 0)
        {
            if (releaseAfter) Release(flow);
            return;
        }
        runs.Add(run);
    }

    private void Pump(DateTime now)
    {
        foreach (var run in runs.ToArray())
        {
            while (run.Cursor < run.Items.Count && run.Items[run.Cursor].Due <= now)
            {
                var work = run.Items[run.Cursor];
                run.Cursor++;
                var flow = config.Flows.Find(f => f.Id == run.FlowId);
                if (flow is null) continue;
                Apply(flow, work.Card, run.Slot);
            }

            if (run.Cursor < run.Items.Count) continue;
            runs.Remove(run);
            if (!run.ReleaseAfter) continue;
            var owner = config.Flows.Find(f => f.Id == run.FlowId);
            if (owner is not null) Release(owner);
        }
    }

    private void Apply(FlowItem flow, Card card, bool slot)
    {
        if (slot && Blocked(flow, card)) return;
        var result = Actions.Run(card, false, null);
        if (!result.Ok) Plugin.Log.Warning("Flow {Name} card failed: {Detail}", flow.Name, result.Detail);
        if (!slot || result.SlotKey is null || result.Original is null) return;
        Claim(flow, result.SlotKey, result.Original);
    }

    private bool Blocked(FlowItem flow, Card card)
    {
        var key = SlotKey(card);
        if (key is null) return false;
        if (!slots.TryGetValue(key, out var slot)) return false;
        if (slot.OwnerId == flow.Id) return false;
        var owner = config.Flows.Find(f => f.Id == slot.OwnerId);
        return owner is not null && active.Contains(owner.Id) && owner.Priority > flow.Priority;
    }

    private void Claim(FlowItem flow, string key, string original)
    {
        if (!slots.ContainsKey(key))
            slots[key] = new Slot { Key = key, Original = original, OwnerId = flow.Id };
        else
            slots[key].OwnerId = flow.Id;
    }

    private void Release(FlowItem flow)
    {
        var mine = slots.Where(s => s.Value.OwnerId == flow.Id).Select(s => s.Key).ToList();
        foreach (var key in mine)
        {
            var slot = slots[key];
            var next = NextOwner(flow, key);
            if (next is not null)
            {
                slot.OwnerId = next.Value.Flow.Id;
                Actions.Run(next.Value.Card, false, null);
                continue;
            }

            if (flow.OnStop == StopMode.Revert)
                Actions.Restore(key, slot.Original);
            slots.Remove(key);
        }
    }

    private (FlowItem Flow, Card Card)? NextOwner(FlowItem leaving, string key)
    {
        FlowItem? best = null;
        Card? card = null;
        foreach (var flow in Ordered())
        {
            if (flow.Id == leaving.Id || !active.Contains(flow.Id)) continue;
            foreach (var candidate in flow.Cards)
            {
                if (candidate.Section != Section.Do) continue;
                if (SlotKey(candidate) != key) continue;
                if (best is null || flow.Priority > best.Priority)
                {
                    best = flow;
                    card = candidate;
                }
            }
        }
        return best is null || card is null ? null : (best, card);
    }

    private static string? SlotKey(Card card) => card.Type switch
    {
        CardType.Setting => $"setting:{card.Option}:{card.Value}",
        CardType.Online => "status",
        CardType.Glamour or CardType.GlamourRevert => "glamour",
        CardType.PenumbraMod when !string.IsNullOrWhiteSpace(card.Option) => "penumbra:" + card.Option.Trim(),
        _ => null,
    };

    private bool Quiet(World world)
    {
        if (config.QuietInCombat && world.Has("InCombat")) return true;
        if (config.QuietInCutscene && world.Has("Cutscene")) return true;
        if (config.QuietBetweenAreas && world.Has("BetweenAreas")) return true;
        if (config.QuietWhenOccupied && world.Has("Occupied")) return true;
        if (config.QuietWhenDead && world.Has("Dead")) return true;
        if (config.QuietInDuty && world.Has("InDuty")) return true;
        return false;
    }

    private IEnumerable<FlowItem> Ordered() =>
        config.Flows.OrderByDescending(f => f.Priority).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase);

    private sealed class Slot
    {
        public string Key = string.Empty;
        public string Original = string.Empty;
        public string OwnerId = string.Empty;
    }

    private sealed class Work
    {
        public DateTime Due;
        public Card Card = new();
    }

    private sealed class Run
    {
        public string FlowId = string.Empty;
        public string Phase = string.Empty;
        public bool ReleaseAfter;
        public bool Slot;
        public int Cursor;
        public List<Work> Items = [];
    }
}
