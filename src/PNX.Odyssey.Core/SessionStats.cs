using System.Globalization;
using System.Text;

namespace Pnx.Odyssey.Core;

public sealed class SessionStats
{
    public required string SessionName { get; init; }
    public required string SessionId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required TimeSpan Age { get; init; }

    public int Registered { get; init; }
    public int Shades { get; init; }
    public int ActivePlayers { get; init; }
    public int WithDiscord { get; init; }
    public int TotalThreads { get; init; }
    public double AverageThreads { get; init; }
    public double MedianThreads { get; init; }
    public int ThreadGilValue { get; init; }
    public int RunVeterans { get; init; }
    public int MaxRun { get; init; }

    public int StrengthCleared { get; init; }
    public int HarmonyCleared { get; init; }
    public int FearCleared { get; init; }
    public int PowerCleared { get; init; }
    public int PowerUnlocked { get; init; }
    public int AllCleared { get; init; }
    public int OneTrialFromPower { get; init; }

    public int Claims { get; init; }
    public int PlayersWithClaims { get; init; }
    public int OfferingsStillOpen { get; init; }
    public int FiniteClaimsLeft { get; init; }

    public int StaffSeated { get; init; }
    public int StaffConnected { get; init; }
    public int SharedMacros { get; init; }
    public int MacroAuthors { get; init; }

    public int PassEvents { get; init; }
    public int FailEvents { get; init; }
    public int RegisterEvents { get; init; }
    public int ClaimEvents { get; init; }
    public int RunEvents { get; init; }

    public int PlayerDiceFaces { get; init; }
    public int GodDiceFaces { get; init; }
    public int CompleteRollSets { get; init; }
    public double AveragePlayerSetTotal { get; init; }

    public int VenueSeen { get; init; }
    public int VenueHere { get; init; }
    public int VenueUnregistered { get; init; }
    public int VenueRegisteredSeen { get; init; }
    public int VenueTotalVisits { get; init; }

    public IReadOnlyList<NamedCount> Worlds { get; init; } = [];
    public IReadOnlyList<NamedCount> VictorGods { get; init; } = [];
    public IReadOnlyList<NamedCount> PopularOfferings { get; init; } = [];
    public IReadOnlyList<NamedCount> PopularArtists { get; init; } = [];
    public IReadOnlyList<NamedCount> ThreadBuckets { get; init; } = [];
    public IReadOnlyList<NamedCount> RunBuckets { get; init; } = [];
    public IReadOnlyList<NamedCount> StaffRoles { get; init; } = [];
    public IReadOnlyList<NamedCount> TopVisitors { get; init; } = [];
    public IReadOnlyList<string> Highlights { get; init; } = [];

    public readonly record struct NamedCount(string Name, int Count);

    public static SessionStats Build(
        SessionSnapshot snapshot,
        IReadOnlyList<LobbyLine> lines,
        IReadOnlyList<VenuePerson> seen,
        IReadOnlyCollection<string> hereKeys,
        IReadOnlyList<VenueTouch> dice)
    {
        List<Participant> players = snapshot.Participants;
        int registered = players.Count;
        int shades = players.Count(player => player.IsShade);
        int active = registered - shades;
        int withDiscord = players.Count(player => !string.IsNullOrWhiteSpace(player.Discord));
        int[] threads = players.Select(player => player.Threads).OrderBy(value => value).ToArray();
        int totalThreads = threads.Sum();
        double average = registered == 0 ? 0 : totalThreads / (double)registered;
        double median = Median(threads);

        int strength = players.Count(player => player.Strength != null);
        int harmony = players.Count(player => player.Harmony != null);
        int fear = players.Count(player => player.Fear != null);
        int power = players.Count(player => player.Power != null);
        int unlocked = players.Count(TrialRules.PowerUnlocked);
        int cleared = players.Count(TrialRules.AllTrialsCleared);
        int oneAway = players.Count(player =>
            TrialRules.PowerUnlocked(player)
            && player.Power == null
            && player.Threads > 0);

        var victors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Participant player in players)
        {
            Tally(victors, player.Strength);
            Tally(victors, player.Harmony);
            Tally(victors, player.Fear);
            Tally(victors, player.Power);
        }

        List<AspectClaim> claims = snapshot.Claims;
        int playersWithClaims = claims.Select(claim => claim.ParticipantId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        int offeringsOpen = AspectCatalog.All.Count(offering => AspectCatalog.HasRoom(offering, claims));
        int finiteLeft = 0;
        var seenPools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (AspectCatalog.Offering offering in AspectCatalog.All)
        {
            if (offering.Slots is not int slots)
                continue;
            string key = offering.Shared ? offering.PoolId : offering.Id;
            if (!seenPools.Add(key))
                continue;
            finiteLeft += Math.Max(0, slots - AspectCatalog.Used(claims, offering));
        }

        var offerings = claims
            .GroupBy(claim => AspectCatalog.Find(claim.OfferingId)?.Text ?? claim.OfferingId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new NamedCount(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        var artists = claims
            .GroupBy(claim => AspectCatalog.Find(claim.OfferingId)?.Name ?? "Unknown", StringComparer.OrdinalIgnoreCase)
            .Select(group => new NamedCount(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        int playerFaces = snapshot.Boards.Sum(board => board.PlayerRolls.Count);
        int godFaces = snapshot.Boards.Sum(board => board.GodRolls.Count);
        var setTotals = new List<int>();
        int completeSets = 0;
        foreach (TrialBoard board in snapshot.Boards)
        {
            foreach (RollSet set in DiceGrouping.Group(board.PlayerRolls))
            {
                if (!set.IsComplete)
                    continue;
                completeSets++;
                setTotals.Add(set.Total);
            }
        }

        int venueUnregistered = seen.Count(person =>
            !players.Any(player => PartyMatcher.SamePerson(person.Name, person.World, player)));
        int venueRegistered = seen.Count(person =>
            players.Any(player => PartyMatcher.SamePerson(person.Name, person.World, player)));
        int venueHere = seen.Count(person => hereKeys.Contains(VenueKey(person.Name, person.World)));

        var highlights = new List<string>();
        if (registered > 0)
        {
            highlights.Add($"{Pct(cleared, registered)} finished every trial");
            highlights.Add($"{Pct(withDiscord, registered)} registered with Discord");
            if (shades > 0)
                highlights.Add($"{shades} shade{(shades == 1 ? "" : "s")} with no threads left");
            if (oneAway > 0)
                highlights.Add($"{oneAway} ready for Zeus");
            if (claims.Count > 0 && artists.Count > 0)
                highlights.Add($"Most claimed artist: {artists[0].Name} ({artists[0].Count})");
            if (victors.Count > 0)
            {
                NamedCount topGod = victors
                    .Select(pair => new NamedCount(pair.Key, pair.Value))
                    .OrderByDescending(item => item.Count)
                    .First();
                highlights.Add($"Hottest god seat: {topGod.Name} ({topGod.Count} wins)");
            }

            Participant? richest = players.OrderByDescending(player => player.Threads).FirstOrDefault();
            if (richest != null && richest.Threads > 0)
                highlights.Add($"Thread leader: {richest.FullName} ({richest.Threads})");
        }

        if (seen.Count > 0)
        {
            VenuePerson? busy = seen.OrderByDescending(person => person.Visits).FirstOrDefault();
            if (busy != null && busy.Visits > 1)
                highlights.Add($"Most seen visitor: {busy.Name} ({busy.Visits} visits)");
            highlights.Add($"{venueUnregistered} venue guest{(venueUnregistered == 1 ? "" : "s")} still unregistered");
        }

        int pass = lines.Count(line => line.Kind == LobbyKind.Pass);
        int fail = lines.Count(line => line.Kind == LobbyKind.Fail);
        if (pass + fail > 0)
            highlights.Add($"Lobby verdicts {pass} pass / {fail} fail");

        return new SessionStats
        {
            SessionName = snapshot.Name,
            SessionId = snapshot.Id,
            CreatedAt = snapshot.CreatedAt,
            Age = SessionTime.Elapsed(snapshot.CreatedAt),
            Registered = registered,
            Shades = shades,
            ActivePlayers = active,
            WithDiscord = withDiscord,
            TotalThreads = totalThreads,
            AverageThreads = average,
            MedianThreads = median,
            ThreadGilValue = totalThreads * TrialRules.GilPerThread,
            RunVeterans = players.Count(player => player.Level > 1),
            MaxRun = players.Count == 0 ? 0 : players.Max(player => player.Level),
            StrengthCleared = strength,
            HarmonyCleared = harmony,
            FearCleared = fear,
            PowerCleared = power,
            PowerUnlocked = unlocked,
            AllCleared = cleared,
            OneTrialFromPower = oneAway,
            Claims = claims.Count,
            PlayersWithClaims = playersWithClaims,
            OfferingsStillOpen = offeringsOpen,
            FiniteClaimsLeft = finiteLeft,
            StaffSeated = snapshot.Members.Count(member => member.Role != StaffRole.None),
            StaffConnected = snapshot.Members.Count(member => member.Connected),
            SharedMacros = snapshot.Macros.Count,
            MacroAuthors = snapshot.Macros.Select(macro => macro.AuthorId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            PassEvents = pass,
            FailEvents = fail,
            RegisterEvents = lines.Count(line => line.Kind == LobbyKind.Register),
            ClaimEvents = lines.Count(line => line.Kind == LobbyKind.Claim),
            RunEvents = lines.Count(line => line.Kind == LobbyKind.Run),
            PlayerDiceFaces = playerFaces,
            GodDiceFaces = godFaces,
            CompleteRollSets = completeSets,
            AveragePlayerSetTotal = setTotals.Count == 0 ? 0 : setTotals.Average(),
            VenueSeen = seen.Count,
            VenueHere = venueHere,
            VenueUnregistered = venueUnregistered,
            VenueRegisteredSeen = venueRegistered,
            VenueTotalVisits = seen.Sum(person => person.Visits),
            Worlds = players
                .GroupBy(player => player.World, StringComparer.OrdinalIgnoreCase)
                .Select(group => new NamedCount(group.Key, group.Count()))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            VictorGods = victors
                .Select(pair => new NamedCount(pair.Key, pair.Value))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            PopularOfferings = offerings,
            PopularArtists = artists,
            ThreadBuckets = BucketThreads(players),
            RunBuckets = players
                .GroupBy(player => $"Run {Math.Max(player.Level, 1)}")
                .Select(group => new NamedCount(group.Key, group.Count()))
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            StaffRoles = snapshot.Members
                .Where(member => member.Role != StaffRole.None)
                .GroupBy(member => StaffText.Label(member.Role))
                .Select(group => new NamedCount(group.Key, group.Count()))
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            TopVisitors = seen
                .OrderByDescending(person => person.Visits)
                .ThenBy(person => person.Name, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(person => new NamedCount($"{person.Name} · {person.World}", person.Visits))
                .ToList(),
            Highlights = highlights,
        };
    }

    public static IReadOnlyDictionary<string, string> BuildCsvPack(
        SessionSnapshot snapshot,
        IReadOnlyList<LobbyLine> lines,
        IReadOnlyList<VenuePerson> seen,
        IReadOnlyCollection<string> hereKeys,
        IReadOnlyList<VenueTouch> dice)
    {
        SessionStats stats = Build(snapshot, lines, seen, hereKeys, dice);
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["summary.csv"] = SummaryCsv(stats),
            ["players.csv"] = PlayersCsv(snapshot, claims: snapshot.Claims, seen, hereKeys, dice),
            ["claims.csv"] = ClaimsCsv(snapshot),
            ["staff.csv"] = StaffCsv(snapshot),
            ["boards.csv"] = BoardsCsv(snapshot),
            ["lobby.csv"] = LobbyCsv(lines),
            ["venue_seen.csv"] = VenueSeenCsv(snapshot, seen, hereKeys),
            ["venue_dice.csv"] = VenueDiceCsv(snapshot, dice),
            ["offerings.csv"] = OfferingsCsv(snapshot.Claims),
            ["macros.csv"] = MacrosCsv(snapshot),
        };
        return files;
    }

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        return value;
    }

    private static string SummaryCsv(SessionStats stats)
    {
        var rows = new List<(string Key, string Value)>
        {
            ("session_name", stats.SessionName),
            ("session_id", stats.SessionId),
            ("created_at_utc", stats.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
            ("age", SessionTime.Format(stats.Age)),
            ("registered", stats.Registered.ToString(CultureInfo.InvariantCulture)),
            ("active_players", stats.ActivePlayers.ToString(CultureInfo.InvariantCulture)),
            ("shades", stats.Shades.ToString(CultureInfo.InvariantCulture)),
            ("with_discord", stats.WithDiscord.ToString(CultureInfo.InvariantCulture)),
            ("total_threads", stats.TotalThreads.ToString(CultureInfo.InvariantCulture)),
            ("average_threads", stats.AverageThreads.ToString("0.##", CultureInfo.InvariantCulture)),
            ("median_threads", stats.MedianThreads.ToString("0.##", CultureInfo.InvariantCulture)),
            ("thread_gil_value", stats.ThreadGilValue.ToString(CultureInfo.InvariantCulture)),
            ("run_veterans", stats.RunVeterans.ToString(CultureInfo.InvariantCulture)),
            ("max_run", stats.MaxRun.ToString(CultureInfo.InvariantCulture)),
            ("strength_cleared", stats.StrengthCleared.ToString(CultureInfo.InvariantCulture)),
            ("harmony_cleared", stats.HarmonyCleared.ToString(CultureInfo.InvariantCulture)),
            ("fear_cleared", stats.FearCleared.ToString(CultureInfo.InvariantCulture)),
            ("power_cleared", stats.PowerCleared.ToString(CultureInfo.InvariantCulture)),
            ("power_unlocked", stats.PowerUnlocked.ToString(CultureInfo.InvariantCulture)),
            ("all_cleared", stats.AllCleared.ToString(CultureInfo.InvariantCulture)),
            ("one_trial_from_power", stats.OneTrialFromPower.ToString(CultureInfo.InvariantCulture)),
            ("claims", stats.Claims.ToString(CultureInfo.InvariantCulture)),
            ("players_with_claims", stats.PlayersWithClaims.ToString(CultureInfo.InvariantCulture)),
            ("offerings_still_open", stats.OfferingsStillOpen.ToString(CultureInfo.InvariantCulture)),
            ("finite_claims_left", stats.FiniteClaimsLeft.ToString(CultureInfo.InvariantCulture)),
            ("staff_seated", stats.StaffSeated.ToString(CultureInfo.InvariantCulture)),
            ("staff_connected", stats.StaffConnected.ToString(CultureInfo.InvariantCulture)),
            ("shared_macros", stats.SharedMacros.ToString(CultureInfo.InvariantCulture)),
            ("pass_events", stats.PassEvents.ToString(CultureInfo.InvariantCulture)),
            ("fail_events", stats.FailEvents.ToString(CultureInfo.InvariantCulture)),
            ("player_dice_faces", stats.PlayerDiceFaces.ToString(CultureInfo.InvariantCulture)),
            ("god_dice_faces", stats.GodDiceFaces.ToString(CultureInfo.InvariantCulture)),
            ("venue_seen", stats.VenueSeen.ToString(CultureInfo.InvariantCulture)),
            ("venue_here", stats.VenueHere.ToString(CultureInfo.InvariantCulture)),
            ("venue_unregistered", stats.VenueUnregistered.ToString(CultureInfo.InvariantCulture)),
        };

        var csv = new StringBuilder();
        csv.AppendLine("key,value");
        foreach ((string key, string value) in rows)
            csv.Append(Escape(key)).Append(',').Append(Escape(value)).AppendLine();
        return csv.ToString();
    }

    private static string PlayersCsv(
        SessionSnapshot snapshot,
        IReadOnlyList<AspectClaim> claims,
        IReadOnlyList<VenuePerson> seen,
        IReadOnlyCollection<string> hereKeys,
        IReadOnlyList<VenueTouch> dice)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',',
        [
            "id", "first_name", "last_name", "full_name", "world", "discord",
            "threads", "is_shade", "run", "strength", "harmony", "fear", "power",
            "power_unlocked", "all_cleared", "claim_count", "claim_offerings", "claim_artists",
            "player_dice_faces", "god_dice_faces", "venue_visits", "venue_here", "dice_touches",
            "last_editor",
        ]));

        foreach (Participant player in snapshot.Participants.OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
        {
            List<AspectClaim> mine = claims
                .Where(claim => string.Equals(claim.ParticipantId, player.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            TrialBoard? board = snapshot.Board(player.Id);
            VenuePerson? visit = seen.FirstOrDefault(person => PartyMatcher.SamePerson(person.Name, person.World, player));
            VenueTouch? touch = dice.FirstOrDefault(item => string.Equals(item.ParticipantId, player.Id, StringComparison.OrdinalIgnoreCase));
            bool here = visit != null && hereKeys.Contains(VenueKey(visit.Name, visit.World));

            csv.Append(Escape(player.Id)).Append(',')
                .Append(Escape(player.FirstName)).Append(',')
                .Append(Escape(player.LastName)).Append(',')
                .Append(Escape(player.FullName)).Append(',')
                .Append(Escape(player.World)).Append(',')
                .Append(Escape(player.Discord)).Append(',')
                .Append(player.Threads).Append(',')
                .Append(player.IsShade ? "yes" : "no").Append(',')
                .Append(player.Level).Append(',')
                .Append(Escape(StaffText.GodOrDash(player.Strength))).Append(',')
                .Append(Escape(StaffText.GodOrDash(player.Harmony))).Append(',')
                .Append(Escape(StaffText.GodOrDash(player.Fear))).Append(',')
                .Append(Escape(StaffText.GodOrDash(player.Power))).Append(',')
                .Append(TrialRules.PowerUnlocked(player) ? "yes" : "no").Append(',')
                .Append(TrialRules.AllTrialsCleared(player) ? "yes" : "no").Append(',')
                .Append(mine.Count).Append(',')
                .Append(Escape(string.Join(" | ", mine.Select(ClaimOfferingText)))).Append(',')
                .Append(Escape(string.Join(" | ", mine.Select(ClaimArtist)))).Append(',')
                .Append(board?.PlayerRolls.Count ?? 0).Append(',')
                .Append(board?.GodRolls.Count ?? 0).Append(',')
                .Append(visit?.Visits ?? 0).Append(',')
                .Append(here ? "yes" : "no").Append(',')
                .Append(touch?.Count ?? 0).Append(',')
                .Append(Escape(player.LastEditor))
                .AppendLine();
        }

        return csv.ToString();
    }

    private static string ClaimsCsv(SessionSnapshot snapshot)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',',
        [
            "claim_id", "at_utc", "participant_id", "player_name", "player_discord", "player_world",
            "run", "offering_id", "artist", "offering", "artist_discord",
        ]));

        foreach (AspectClaim claim in snapshot.Claims.OrderBy(item => item.At))
        {
            Participant? player = snapshot.Participant(claim.ParticipantId);
            AspectCatalog.Offering? offering = AspectCatalog.Find(claim.OfferingId);
            string artist = offering?.Name ?? "";
            csv.Append(Escape(claim.Id)).Append(',')
                .Append(Escape(claim.At.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(Escape(claim.ParticipantId)).Append(',')
                .Append(Escape(claim.PlayerName)).Append(',')
                .Append(Escape(player?.Discord)).Append(',')
                .Append(Escape(player?.World)).Append(',')
                .Append(claim.Run).Append(',')
                .Append(Escape(claim.OfferingId)).Append(',')
                .Append(Escape(artist)).Append(',')
                .Append(Escape(offering?.Text ?? "")).Append(',')
                .Append(Escape(AspectCatalog.Discord(artist)))
                .AppendLine();
        }

        return csv.ToString();
    }

    private static string StaffCsv(SessionSnapshot snapshot)
    {
        var csv = new StringBuilder();
        csv.AppendLine("id,name,world,role,connected,last_seen_utc");
        foreach (SessionMember member in snapshot.Members.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            csv.Append(Escape(member.Id)).Append(',')
                .Append(Escape(member.Name)).Append(',')
                .Append(Escape(member.World)).Append(',')
                .Append(Escape(StaffText.Label(member.Role))).Append(',')
                .Append(member.Connected ? "yes" : "no").Append(',')
                .Append(Escape(member.LastSeen.ToString("O", CultureInfo.InvariantCulture)))
                .AppendLine();
        }

        return csv.ToString();
    }

    private static string BoardsCsv(SessionSnapshot snapshot)
    {
        var csv = new StringBuilder();
        csv.AppendLine("participant_id,player_name,aspect,player_rolls,god_rolls,player_faces,god_faces,suggested,last_editor,revision");
        foreach (TrialBoard board in snapshot.Boards.OrderBy(item => item.ParticipantId, StringComparer.OrdinalIgnoreCase))
        {
            Participant? player = snapshot.Participant(board.ParticipantId);
            SuggestedCall call = TrialRules.Suggest(board.Aspect, board.PlayerRolls, board.GodRolls);
            csv.Append(Escape(board.ParticipantId)).Append(',')
                .Append(Escape(player?.FullName ?? "")).Append(',')
                .Append(Escape(StaffText.AspectName(board.Aspect))).Append(',')
                .Append(Escape(string.Join(' ', board.PlayerRolls))).Append(',')
                .Append(Escape(string.Join(' ', board.GodRolls))).Append(',')
                .Append(board.PlayerRolls.Count).Append(',')
                .Append(board.GodRolls.Count).Append(',')
                .Append(Escape(call.ToString())).Append(',')
                .Append(Escape(board.LastEditor)).Append(',')
                .Append(board.Revision)
                .AppendLine();
        }

        return csv.ToString();
    }

    private static string LobbyCsv(IReadOnlyList<LobbyLine> lines)
    {
        var csv = new StringBuilder();
        csv.AppendLine("at_utc,name,role,kind,text");
        foreach (LobbyLine line in lines.OrderBy(item => item.At))
        {
            csv.Append(Escape(line.At.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(Escape(line.Name)).Append(',')
                .Append(Escape(StaffText.Label(line.Role))).Append(',')
                .Append(Escape(line.Kind.ToString())).Append(',')
                .Append(Escape(line.Text))
                .AppendLine();
        }

        return csv.ToString();
    }

    private static string VenueSeenCsv(SessionSnapshot snapshot, IReadOnlyList<VenuePerson> seen, IReadOnlyCollection<string> hereKeys)
    {
        var csv = new StringBuilder();
        csv.AppendLine("name,world,visits,here_now,registered,participant_id,discord");
        foreach (VenuePerson person in seen.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            Participant? player = snapshot.Participants.FirstOrDefault(item => PartyMatcher.SamePerson(person.Name, person.World, item));
            csv.Append(Escape(person.Name)).Append(',')
                .Append(Escape(person.World)).Append(',')
                .Append(person.Visits).Append(',')
                .Append(hereKeys.Contains(VenueKey(person.Name, person.World)) ? "yes" : "no").Append(',')
                .Append(player == null ? "no" : "yes").Append(',')
                .Append(Escape(player?.Id)).Append(',')
                .Append(Escape(player?.Discord))
                .AppendLine();
        }

        return csv.ToString();
    }

    private static string VenueDiceCsv(SessionSnapshot snapshot, IReadOnlyList<VenueTouch> dice)
    {
        var csv = new StringBuilder();
        csv.AppendLine("participant_id,player_name,discord,count,last_at_utc");
        foreach (VenueTouch touch in dice.OrderByDescending(item => item.At))
        {
            Participant? player = snapshot.Participant(touch.ParticipantId);
            csv.Append(Escape(touch.ParticipantId)).Append(',')
                .Append(Escape(player?.FullName ?? "")).Append(',')
                .Append(Escape(player?.Discord)).Append(',')
                .Append(touch.Count).Append(',')
                .Append(Escape(touch.At.ToString("O", CultureInfo.InvariantCulture)))
                .AppendLine();
        }

        return csv.ToString();
    }

    private static string OfferingsCsv(IReadOnlyList<AspectClaim> claims)
    {
        var csv = new StringBuilder();
        csv.AppendLine("offering_id,artist,offering,pool_id,shared,slots,used,remaining,artist_discord");
        foreach (AspectCatalog.Offering offering in AspectCatalog.All)
        {
            int used = AspectCatalog.Used(claims, offering);
            string remaining = offering.Slots is int slots
                ? Math.Max(0, slots - used).ToString(CultureInfo.InvariantCulture)
                : "unlimited";
            csv.Append(Escape(offering.Id)).Append(',')
                .Append(Escape(offering.Name)).Append(',')
                .Append(Escape(offering.Text)).Append(',')
                .Append(Escape(offering.PoolId)).Append(',')
                .Append(offering.Shared ? "yes" : "no").Append(',')
                .Append(offering.Slots?.ToString(CultureInfo.InvariantCulture) ?? "unlimited").Append(',')
                .Append(used).Append(',')
                .Append(Escape(remaining)).Append(',')
                .Append(Escape(AspectCatalog.Discord(offering.Name)))
                .AppendLine();
        }

        return csv.ToString();
    }

    private static string MacrosCsv(SessionSnapshot snapshot)
    {
        var csv = new StringBuilder();
        csv.AppendLine("id,name,author_id,author_name,line_count,text");
        foreach (GodMacro macro in snapshot.Macros.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            int lines = macro.Text
                .Replace("\r", "", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Length;
            csv.Append(Escape(macro.Id)).Append(',')
                .Append(Escape(macro.Name)).Append(',')
                .Append(Escape(macro.AuthorId)).Append(',')
                .Append(Escape(macro.AuthorName)).Append(',')
                .Append(lines).Append(',')
                .Append(Escape(macro.Text))
                .AppendLine();
        }

        return csv.ToString();
    }

    private static void Tally(Dictionary<string, int> map, StaffRole? role)
    {
        if (role is not { } known || known == StaffRole.None)
            return;
        string label = StaffText.Label(known);
        map[label] = map.TryGetValue(label, out int count) ? count + 1 : 1;
    }

    private static List<NamedCount> BucketThreads(IReadOnlyList<Participant> players)
    {
        int zero = players.Count(player => player.Threads <= 0);
        int low = players.Count(player => player.Threads is >= 1 and <= 2);
        int mid = players.Count(player => player.Threads is >= 3 and <= 5);
        int high = players.Count(player => player.Threads >= 6);
        return
        [
            new("0 threads (shade)", zero),
            new("1–2 threads", low),
            new("3–5 threads", mid),
            new("6+ threads", high),
        ];
    }

    private static double Median(IReadOnlyList<int> ordered)
    {
        if (ordered.Count == 0)
            return 0;
        int mid = ordered.Count / 2;
        return ordered.Count % 2 == 1
            ? ordered[mid]
            : (ordered[mid - 1] + ordered[mid]) / 2.0;
    }

    private static string Pct(int part, int whole) =>
        whole <= 0 ? "0%" : $"{Math.Round(100.0 * part / whole):0}%";

    private static string ClaimOfferingText(AspectClaim claim) =>
        AspectCatalog.Find(claim.OfferingId)?.Text ?? claim.OfferingId;

    private static string ClaimArtist(AspectClaim claim) =>
        AspectCatalog.Find(claim.OfferingId)?.Name ?? "";

    private static string VenueKey(string name, string world) => $"{name.Trim()}|{world.Trim()}";
}
