using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.Services;

internal sealed class VenueWatch
{
    private DateTime _next;
    private string? _diceSession;
    private readonly HashSet<string> _here = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _dice = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Here => _here;

    public void Tick(Plugin plugin, DateTime utcNow)
    {
        if (utcNow >= _next)
        {
            _next = utcNow.AddSeconds(1);
            Scan(plugin);
        }

        NoteDice(plugin, utcNow);
    }

    private void Scan(Plugin plugin)
    {
        string session = plugin.Client.Snapshot?.Id ?? "local";
        List<VenuePerson> seen = Book(plugin.Config, session);
        var now = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool changed = false;
        foreach (IGameObject actor in plugin.Objects)
        {
            if (actor is not IPlayerCharacter player)
                continue;

            string name;
            string world;
            try
            {
                name = player.Name.TextValue.Trim();
                world = player.HomeWorld.Value.Name.ToString()?.Trim() ?? "";
            }
            catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
            {
                continue;
            }

            if (name.Length == 0)
                continue;
            if (plugin.Self is { } self && string.Equals(name, self.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            string key = Key(name, world);
            now.Add(key);
            if (_here.Contains(key))
                continue;

            VenuePerson? person = seen.FirstOrDefault(item => string.Equals(Key(item.Name, item.World), key, StringComparison.OrdinalIgnoreCase));
            if (person == null)
            {
                seen.Add(new VenuePerson { Name = name, World = world, Visits = 1 });
                changed = true;
                continue;
            }

            person.Visits++;
            person.Name = name;
            person.World = world;
            changed = true;
        }

        _here.Clear();
        foreach (string key in now)
            _here.Add(key);
        if (changed)
            plugin.Config.Save();
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

        bool changed = false;
        List<VenueTouch> touches = DiceBook(plugin.Config, snapshot.Id);
        foreach (TrialBoard board in snapshot.Boards)
        {
            int count = board.PlayerRolls.Count + board.GodRolls.Count;
            if (!_dice.TryGetValue(board.ParticipantId, out int previous))
            {
                _dice[board.ParticipantId] = count;
                continue;
            }

            if (count <= previous)
            {
                _dice[board.ParticipantId] = count;
                continue;
            }

            _dice[board.ParticipantId] = count;
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

            changed = true;
        }

        if (changed)
            plugin.Config.Save();
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
