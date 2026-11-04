using System;
using System.Collections.Generic;
using System.Linq;

namespace Starflow;

internal sealed record Confirm(string FlowId, string FlowName, Card Card);

internal sealed class Engine
{
    // A When that blinks false for less than this keeps its ask. Zone lines do that.
    private static readonly TimeSpan AskGrace = TimeSpan.FromSeconds(4);

    private readonly Configuration config;
    private readonly Dictionary<string, TimeSpan> trueSince = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TimeSpan> askLost = new(StringComparer.Ordinal);
    private readonly HashSet<string> active = new(StringComparer.Ordinal);
    private readonly HashSet<string> held = new(StringComparer.Ordinal);
    private readonly HashSet<string> accepted = new(StringComparer.Ordinal);
    private readonly List<string> pending = [];
    private readonly List<Confirm> confirms = [];
    private readonly Dictionary<string, Slot> slots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> errors = new(StringComparer.Ordinal);
    private readonly List<Run> runs = [];
    private TimeSpan nextPoll;
    private TimeSpan lastPump;

    public Engine(Configuration config) => this.config = config;

    // Framework time. Only moves forward, so a wall clock change cannot fire a stack twice.
    public TimeSpan Clock { get; private set; }
    public World Last { get; private set; } = new();
    public IReadOnlyList<string> Pending => pending;
    public IReadOnlyList<Confirm> Confirms => confirms;
    public int AskSerial { get; private set; }
    public int Waiting => pending.Count + confirms.Count;

    public bool IsActive(string id) => active.Contains(id);
    public bool IsHeld(string id) => held.Contains(id);
    public bool IsRunning(string id) => runs.Exists(r => r.FlowId == id);
    public string? Error(string cardId) => errors.TryGetValue(cardId, out var e) ? e : null;

    public void Advance(TimeSpan delta)
    {
        if (delta > TimeSpan.Zero) Clock += delta;
    }

    // Logout or character select. The queue is dropped on purpose: a half-fired macro does not resume.
    public void Reset()
    {
        if (active.Count == 0 && runs.Count == 0 && slots.Count == 0 && pending.Count == 0 && confirms.Count == 0)
        {
            Last = new World();
            return;
        }

        var dropped = runs.Sum(r => r.Items.Count - r.Cursor);
        foreach (var (key, slot) in slots)
        {
            var owner = config.Flows.Find(f => f.Id == slot.OwnerId);
            if (owner is null || owner.OnStop == StopMode.Keep || slot.Keep || slot.Overridden) continue;
            // Game settings and Penumbra are local and can be put back here. Status and glamour need a character.
            if (!key.StartsWith("setting:", StringComparison.Ordinal) && !key.StartsWith("penumbra:", StringComparison.Ordinal)) continue;
            if (!Actions.Restore(key, slot.Original, out var error))
                Plugin.Log.Warning("Restore of {Key} on logout failed: {Error}", key, error);
        }

        if (dropped > 0 || active.Count > 0)
            Plugin.Log.Information("Logged out. {Count} queued cards dropped; they will not resume.", dropped);

        active.Clear();
        held.Clear();
        accepted.Clear();
        pending.Clear();
        askLost.Clear();
        confirms.Clear();
        runs.Clear();
        slots.Clear();
        trueSince.Clear();
        Last = new World();
    }

    public void Tick(bool force)
    {
        if (!force && Clock < nextPoll)
        {
            Pump(Quiet(Last));
            return;
        }
        nextPoll = Clock + TimeSpan.FromSeconds(Math.Max(0.15f, config.PollSec));
        Last = World.Capture();
        if (!Last.LoggedIn)
        {
            Reset();
            return;
        }

        if (!config.Enabled)
        {
            foreach (var flow in Ordered().Where(f => active.Contains(f.Id)).ToList()) Stop(flow, true, false);
            Pump(false);
            return;
        }

        var quiet = Quiet(Last);
        Pump(quiet);

        // Mid zone line the snapshot is not trustworthy. Nothing starts or stops on it.
        if (Last.Has("BetweenAreas") || Last.TerritoryId == 0) return;

        foreach (var flow in Ordered().ToList())
        {
            if (quiet && !flow.IgnoreQuiet) continue;

            if (!flow.Enabled || flow.Mode == RunMode.Off || Catalog.Missing(flow).Count > 0)
            {
                if (active.Contains(flow.Id)) Stop(flow, true, false);
                pending.Remove(flow.Id);
                continue;
            }

            var when = flow.In(Section.When).Count > 0 && Eval.SectionMatches(flow, Section.When, Last);
            if (!when)
            {
                trueSince.Remove(flow.Id);
                held.Remove(flow.Id);
                accepted.Remove(flow.Id);
                if (pending.Contains(flow.Id))
                {
                    if (!askLost.TryGetValue(flow.Id, out var lost)) askLost[flow.Id] = Clock;
                    else if (Clock - lost >= AskGrace)
                    {
                        pending.Remove(flow.Id);
                        askLost.Remove(flow.Id);
                    }
                }
                if (active.Contains(flow.Id)) Stop(flow, true, false);
                continue;
            }

            askLost.Remove(flow.Id);
            if (!trueSince.ContainsKey(flow.Id)) trueSince[flow.Id] = Clock;
            var ready = flow.HoldSec <= 0 || (Clock - trueSince[flow.Id]).TotalSeconds >= flow.HoldSec;
            if (!ready) continue;

            if (active.Contains(flow.Id))
            {
                if (flow.In(Section.Until).Count > 0 && Eval.SectionMatches(flow, Section.Until, Last))
                {
                    // Until is a rising end. The flow stays off until When itself goes false.
                    held.Add(flow.Id);
                    Stop(flow, true, false);
                }
                continue;
            }

            if (held.Contains(flow.Id)) continue;

            if (flow.Mode == RunMode.Ask)
            {
                if (accepted.Contains(flow.Id)) Start(flow, true);
                else if (!pending.Contains(flow.Id))
                {
                    pending.Add(flow.Id);
                    AskSerial++;
                }
                continue;
            }

            Start(flow, true);
        }
    }

    public void Accept(string id)
    {
        pending.Remove(id);
        askLost.Remove(id);
        accepted.Add(id);
        held.Remove(id);
        Tick(true);
    }

    public void Decline(string id)
    {
        pending.Remove(id);
        askLost.Remove(id);
        held.Add(id);
    }

    public void Answer(Confirm confirm, bool yes)
    {
        confirms.Remove(confirm);
        if (!yes)
        {
            errors[confirm.Card.Id] = "Declined.";
            return;
        }
        var result = Actions.Run(confirm.Card, Last, true);
        if (result.Ok) errors.Remove(confirm.Card.Id);
        else errors[confirm.Card.Id] = result.Detail;
    }

    // One shot: Do runs once and nothing latches. Slotted cards are skipped unless the flow is already on,
    // so a one-shot never leaves an owner behind that is not active.
    public void RunOnce(FlowItem flow)
    {
        if (!Last.LoggedIn) Last = World.Capture();
        var on = active.Contains(flow.Id);
        Enqueue(flow, Section.Do, false, on, !on);
    }

    public void Stop(FlowItem flow, bool quiet) => Stop(flow, quiet, true);

    private void Stop(FlowItem flow, bool quiet, bool manual)
    {
        var was = active.Remove(flow.Id);
        if (manual && Last.LoggedIn) held.Add(flow.Id);
        runs.RemoveAll(r => r.FlowId == flow.Id && r.Phase == Section.Do);
        confirms.RemoveAll(c => c.FlowId == flow.Id && c.Card.Section == Section.Do);
        if (flow.In(Section.After).Count == 0) Release(flow);
        else Enqueue(flow, Section.After, true, true, false);
        if (!quiet && config.NotifyInChat) Plugin.Notify(was ? $"Stopped {flow.Name}" : $"Ran After for {flow.Name}");
    }

    // Why this flow is on, or why it is not. The first line is the answer; the card lines are the reasons.
    public IEnumerable<string> Why(FlowItem flow)
    {
        var world = Last.LoggedIn ? Last : World.Capture();
        yield return "Why: " + Verdict(flow, world);
        foreach (var line in Eval.Explain(flow, world)) yield return line;
    }

    private string Verdict(FlowItem flow, World world)
    {
        if (!world.LoggedIn) return "not logged in, so nothing runs.";
        if (!config.Enabled) return "Starflow is turned off in the top bar.";
        if (Plugin.Instance is { IsPaused: true }) return "Starflow is paused. /fl resume";
        var missing = Catalog.Missing(flow);
        if (missing.Count > 0) return $"it is not ready to run: {missing[0]}.";
        if (!flow.Enabled) return "this flow is switched off. Tick On to let it run.";
        if (flow.Mode == RunMode.Off) return "mode is Off, so it only runs from Run once.";
        if (active.Contains(flow.Id)) return "it is on. When matched, and Until has not tripped.";
        if (Quiet(world) && !flow.IgnoreQuiet) return "a quiet option in Settings is holding it right now.";
        if (flow.In(Section.When).Count == 0) return "When is empty, so it does not start on its own.";
        if (!Eval.SectionMatches(flow, Section.When, world))
            return flow.WhenJoin == Join.All ? "When is false. Every When card must be true; the 'no' lines below are the ones stopping it." : "When is false. At least one When card must be true, and none are.";
        if (held.Contains(flow.Id)) return "it was stopped (by Until, Stop, or a skipped prompt) and waits for When to go false once before it can start again.";
        if (pending.Contains(flow.Id)) return "it is waiting for a yes in Prompts.";
        if (flow.HoldSec > 0 && trueSince.TryGetValue(flow.Id, out var since) && (Clock - since).TotalSeconds < flow.HoldSec)
            return $"When is true, but Hold wants {flow.HoldSec:0.#} s of it first.";
        return "When is true. It starts on the next check.";
    }

    // Both stages, labelled: Do when the flow starts, After and Revert when it stops. Nothing is sent.
    public IEnumerable<string> Dry(FlowItem flow)
    {
        yield return "Stage 1 · On start (Do)";
        foreach (var line in DryStage(flow, Section.Do)) yield return "  " + line;
        yield return "Stage 2 · On stop (After)";
        foreach (var line in DryStage(flow, Section.After)) yield return "  " + line;
        var slotted = flow.In(Section.Do).Where(c => CardType.IsSlotted(c.Type) && !c.Keep).ToList();
        yield return "Stage 3 · Revert";
        if (flow.OnStop == StopMode.Keep) yield return "  Stop is Keep. Nothing is put back.";
        else if (slotted.Count == 0) yield return "  No slotted cards in Do. Nothing to put back.";
        else foreach (var card in slotted) yield return "  put back what was there before: " + card.Label;
        yield return "Dry run. Nothing was sent.";
    }

    private IEnumerable<string> DryStage(FlowItem flow, Section section)
    {
        var at = 0;
        var i = 0;
        var world = Last;
        foreach (var card in flow.In(section))
        {
            var key = Catalog.Key(section, i++);
            if (card.Type == CardType.Wait)
            {
                at += Math.Max(0, card.WaitMs);
                yield return $"{Stamp(at)}  {key}  wait {Math.Max(0, card.WaitMs)} ms";
                continue;
            }

            var times = Math.Clamp(card.Repeat <= 0 ? 1 : card.Repeat, 1, 30);
            var gap = Math.Max(0, card.WaitMs);
            var text = card.Type switch
            {
                CardType.Command or CardType.Notice => Tokens.Fill(card.Value, world),
                CardType.SearchComment => ChatSender.Comment(Tokens.Fill(card.Value, world)),
                _ => card.Label,
            };
            var repeat = times > 1 ? $"  ×{times}, {gap} ms apart" : string.Empty;
            var slot = section != Section.Do || !CardType.IsSlotted(card.Type) ? string.Empty
                : card.Keep || flow.OnStop == StopMode.Keep ? "  [slot, keep]" : "  [slot, reverts]";
            yield return $"{Stamp(at)}  {key}  {text}{repeat}{slot}";
            if (card.Type == CardType.Command && times > 1) at += gap * (times - 1);
            if (gap > 0 && card.Type == CardType.Command) at += gap;
        }
        if (i == 0) yield return $"{section} is empty.";
    }

    public IEnumerable<string> ActiveNames()
    {
        foreach (var flow in config.Flows)
        {
            if (active.Contains(flow.Id)) yield return flow.Name;
        }
    }

    // The flow is being deleted. Its After will not run, so whatever it owns is released now.
    public void Forget(FlowItem flow)
    {
        active.Remove(flow.Id);
        held.Remove(flow.Id);
        accepted.Remove(flow.Id);
        pending.Remove(flow.Id);
        trueSince.Remove(flow.Id);
        askLost.Remove(flow.Id);
        runs.RemoveAll(r => r.FlowId == flow.Id);
        confirms.RemoveAll(c => c.FlowId == flow.Id);
        foreach (var card in flow.Cards) errors.Remove(card.Id);
        Release(flow);
    }

    private static string Stamp(int ms) => ms < 1000 ? $"+{ms} ms" : $"+{ms / 1000.0:0.0} s";

    private void Start(FlowItem flow, bool announce)
    {
        if (active.Contains(flow.Id)) return;
        active.Add(flow.Id);
        Enqueue(flow, Section.Do, false, true, false);
        if (announce && config.NotifyInChat) Plugin.Notify($"Started {flow.Name}");
    }

    private void Enqueue(FlowItem flow, Section section, bool releaseAfter, bool slot, bool oneShot)
    {
        runs.RemoveAll(r => r.FlowId == flow.Id && r.Phase == section);
        var cards = flow.In(section);
        foreach (var card in cards) errors.Remove(card.Id);

        var run = new Run { FlowId = flow.Id, Phase = section, ReleaseAfter = releaseAfter, Slot = slot, OneShot = oneShot };
        var delay = 0;
        foreach (var card in cards)
        {
            if (card.Type == CardType.Wait)
            {
                delay += Math.Max(0, card.WaitMs);
                continue;
            }

            var due = Clock + TimeSpan.FromMilliseconds(delay);
            if (card.Type != CardType.Command)
            {
                // Two writes to the same slot at the same moment are one write. The last card wins and claims once.
                var key = SlotKey(card);
                var last = run.Items.Count > 0 ? run.Items[^1] : null;
                if (key is not null && last is not null && last.Due == due && SlotKey(last.Card) == key)
                    run.Items.RemoveAt(run.Items.Count - 1);
                run.Items.Add(new Work { Due = due, Card = card });
                continue;
            }

            var times = Math.Clamp(card.Repeat <= 0 ? 1 : card.Repeat, 1, 30);
            var gap = Math.Max(0, card.WaitMs);
            for (var i = 0; i < times; i++)
            {
                run.Items.Add(new Work { Due = Clock + TimeSpan.FromMilliseconds(delay), Card = card });
                if (i < times - 1) delay += gap;
            }
            delay += gap;
        }
        if (run.Items.Count == 0)
        {
            if (releaseAfter) Release(flow);
            return;
        }
        runs.Add(run);
    }

    private void Pump(bool quiet)
    {
        var dt = Clock - lastPump;
        lastPump = Clock;
        foreach (var run in runs.ToArray())
        {
            var flow = config.Flows.Find(f => f.Id == run.FlowId);
            if (flow is null)
            {
                runs.Remove(run);
                continue;
            }

            if (quiet && !flow.IgnoreQuiet)
            {
                // Paused, so the gaps between cards keep their length.
                for (var i = run.Cursor; i < run.Items.Count; i++) run.Items[i].Due += dt;
                continue;
            }

            while (run.Cursor < run.Items.Count && run.Items[run.Cursor].Due <= Clock)
            {
                var work = run.Items[run.Cursor];
                run.Cursor++;
                Apply(flow, run, work.Card);
            }

            if (run.Cursor < run.Items.Count) continue;
            runs.Remove(run);
            if (run.ReleaseAfter) Release(flow);
        }
    }

    private void Apply(FlowItem flow, Run run, Card card)
    {
        if (run.OneShot && CardType.IsSlotted(card.Type))
        {
            errors[card.Id] = "Skipped in a one-shot run. Slotted cards need the flow on, so they can be put back.";
            return;
        }

        var key = SlotKey(card);
        if (run.Slot && key is not null && Blocked(flow, key, out var owner))
        {
            errors[card.Id] = $"{owner} holds this with a higher priority.";
            return;
        }

        var result = Actions.Run(card, Last);
        if (result.NeedsConfirm)
        {
            confirms.Add(new Confirm(flow.Id, flow.Name, card));
            errors[card.Id] = "Waiting for a yes in Prompts.";
            AskSerial++;
            return;
        }
        if (!result.Ok)
        {
            errors[card.Id] = result.Detail;
            Plugin.Log.Warning("Flow \"{Name}\" card failed: {Detail}", flow.Name, result.Detail);
            return;
        }
        errors.Remove(card.Id);
        if (!run.Slot || result.SlotKey is null || result.Original is null) return;

        if (run.Phase == Section.After)
        {
            // An After card that writes a slot this flow owns overrides the restore on purpose.
            if (slots.TryGetValue(result.SlotKey, out var mine) && mine.OwnerId == flow.Id) mine.Overridden = true;
            return;
        }
        Claim(flow, card, result.SlotKey, result.Original);
    }

    private bool Blocked(FlowItem flow, string key, out string ownerName)
    {
        ownerName = string.Empty;
        if (!slots.TryGetValue(key, out var slot) || slot.OwnerId == flow.Id) return false;
        var owner = config.Flows.Find(f => f.Id == slot.OwnerId);
        if (owner is null || !active.Contains(owner.Id) || owner.Priority <= flow.Priority) return false;
        ownerName = owner.Name;
        return true;
    }

    // The first owner captures the original. Later owners take the slot but not the original.
    private void Claim(FlowItem flow, Card card, string key, string original)
    {
        if (!slots.TryGetValue(key, out var slot))
        {
            slots[key] = new Slot { Original = original, OwnerId = flow.Id, Keep = card.Keep };
            return;
        }
        if (slot.OwnerId != flow.Id) slot.Overridden = false;
        slot.OwnerId = flow.Id;
        slot.Keep = card.Keep;
    }

    private void Release(FlowItem flow)
    {
        var mine = slots.Where(s => s.Value.OwnerId == flow.Id).Select(s => s.Key).ToList();
        foreach (var key in mine)
        {
            var slot = slots[key];
            if (!slot.Overridden && NextOwner(flow, key) is { } next)
            {
                // Hand the slot to the next active flow and apply its card. The original stays as it was.
                slot.OwnerId = next.Flow.Id;
                slot.Keep = next.Card.Keep;
                var result = Actions.Run(next.Card, Last);
                if (!result.Ok) errors[next.Card.Id] = result.Detail;
                continue;
            }

            slots.Remove(key);
            if (slot.Overridden || slot.Keep || flow.OnStop == StopMode.Keep) continue;
            if (!Actions.Restore(key, slot.Original, out var error))
            {
                Plugin.Log.Warning("Flow \"{Name}\" could not restore {Key}: {Error}", flow.Name, key, error);
                Plugin.Notify($"{flow.Name}: could not put back {key}. {error}");
            }
        }
    }

    private (FlowItem Flow, Card Card)? NextOwner(FlowItem leaving, string key)
    {
        foreach (var flow in Ordered())
        {
            if (flow.Id == leaving.Id || !active.Contains(flow.Id)) continue;
            Card? found = null;
            foreach (var candidate in flow.In(Section.Do))
            {
                if (SlotKey(candidate) == key) found = candidate;
            }
            if (found is not null) return (flow, found);
        }
        return null;
    }

    public static string? SlotKey(Card card) => card.Type switch
    {
        CardType.Setting => $"setting:{(string.IsNullOrWhiteSpace(card.Option) ? "System" : card.Option.Trim())}:{card.Value.Trim()}",
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
        public string Original = string.Empty;
        public string OwnerId = string.Empty;
        public bool Keep;
        public bool Overridden;
    }

    private sealed class Work
    {
        public TimeSpan Due;
        public Card Card = new();
    }

    private sealed class Run
    {
        public string FlowId = string.Empty;
        public Section Phase;
        public bool ReleaseAfter;
        public bool Slot;
        public bool OneShot;
        public int Cursor;
        public List<Work> Items = [];
    }
}
