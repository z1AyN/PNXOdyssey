using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.Services;

internal sealed class VenueWatch
{
    private DateTime _nextScan;
    private string? _diceSession;
    private bool _dirty;
    private readonly HashSet<string> _here = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _dice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, string> _worlds = new();

    public IReadOnlyCollection<string> Here => _here;

    public void Tick(Plugin plugin, DateTime utcNow)
    {
        if (utcNow >= _nextScan)
        {
            _nextScan = utcNow.AddSeconds(3);
            Scan(plugin);
            NoteDice(plugin, utcNow);
            Flush(plugin);
        }
    }

    public void Flush(Plugin plugin)
    {
        if (!_dirty)
            return;
        _dirty = false;
        plugin.Config.SaveDeferred();
    }

    private void Scan(Plugin plugin)
    {
        string session = plugin.Client.Snapshot?.Id ?? "local";
        List<VenuePerson> seen = Book(plugin.Config, session);
        var index = new Dictionary<string, VenuePerson>(StringComparer.OrdinalIgnoreCase);
        foreach (VenuePerson person in seen)
            index[Key(person.Name, person.World)] = person;

        var now = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? selfName = plugin.Self?.Name;
        foreach (IGameObject actor in plugin.Objects)
        {
            if (actor is not IPlayerCharacter player)
                continue;

            string name;
            string world;
            try
            {
                name = player.Name.TextValue.Trim();
                if (name.Length == 0)
                    continue;
                if (selfName != null && string.Equals(name, selfName, StringComparison.OrdinalIgnoreCase))
                    continue;
                world = WorldName(player);
            }
            catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
            {
                continue;
            }

            string key = Key(name, world);
            now.Add(key);
            if (_here.Contains(key))
                continue;

            if (index.TryGetValue(key, out VenuePerson? person))
            {
                person.Visits++;
            }
            else
            {
                person = new VenuePerson { Name = name, World = world, Visits = 1 };
                seen.Add(person);
                index[key] = person;
            }

            MarkDirty();
        }

        _here.Clear();
        foreach (string key in now)
            _here.Add(key);
    }

    private void NoteDice(Plugin plugin, DateTime utcNow)
    {
        SessionSnapshot? snapshot = plugin.Client.Snapshot;
        if (snapshot == null)
            return;
        if (!string.Equals(_diceSession, snapshot.Id, StringComparison.Ordinal))
        {
            _diceSession = snapshot.Id;
            _dice.Clear();
            foreach (TrialBoard board in snapshot.Boards)
                _dice[board.ParticipantId] = board.PlayerRolls.Count + board.GodRolls.Count;
            return;
        }

        List<VenueTouch>? touches = null;
        foreach (TrialBoard board in snapshot.Boards)
        {
            int count = board.PlayerRolls.Count + board.GodRolls.Count;
            if (!_dice.TryGetValue(board.ParticipantId, out int previous))
            {
                _dice[board.ParticipantId] = count;
                continue;
            }

            _dice[board.ParticipantId] = count;
            if (count <= previous)
                continue;

            touches ??= DiceBook(plugin.Config, snapshot.Id);
            VenueTouch? touch = touches.FirstOrDefault(item => string.Equals(item.ParticipantId, board.ParticipantId, StringComparison.OrdinalIgnoreCase));
            if (touch == null)
            {
                touches.Add(new VenueTouch
                {
                    ParticipantId = board.ParticipantId,
                    At = utcNow,
                    Count = count - previous,
                });
            }
            else
            {
                touch.At = utcNow;
                touch.Count += count - previous;
            }

            MarkDirty();
        }
    }

    private void MarkDirty() => _dirty = true;

    private string WorldName(IPlayerCharacter player)
    {
        uint id = player.HomeWorld.RowId;
        if (_worlds.TryGetValue(id, out string? cached))
            return cached;

        string name = "";
        try
        {
            name = player.HomeWorld.Value.Name.ToString()?.Trim() ?? "";
        }
        catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
        {
        }

        _worlds[id] = name;
        return name;
    }

    public static string Key(string name, string world) => $"{name.Trim()}|{world.Trim()}";

    public static List<VenuePerson> Book(Configuration config, string session)
    {
        if (!config.VenueSeen.TryGetValue(session, out List<VenuePerson>? people) || people == null)
        {
            people = [];
            config.VenueSeen[session] = people;
        }

        return people;
    }

    public static List<VenueTouch> DiceBook(Configuration config, string session)
    {
        if (!config.VenueDice.TryGetValue(session, out List<VenueTouch>? touches) || touches == null)
        {
            touches = [];
            config.VenueDice[session] = touches;
        }

        return touches;
    }
}
