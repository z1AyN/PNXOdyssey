using System.Text;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.Services;

internal sealed class TrialWatcher
{
    private readonly Dictionary<string, int> _levels = new(StringComparer.OrdinalIgnoreCase);

    public void OnChat(IChatMessage message, OdysseyClient client, CharacterIdentity? self)
    {
        if (self == null || !IsPartyChannel(message.LogKind) || !TryReadRoll(message.Message, out int roll, out string named))
            return;

        SessionSnapshot? snapshot = client.Snapshot;
        string? watched = client.WatchedParticipantId;
        if (snapshot == null || watched == null)
            return;

        SessionMember? member = snapshot.Member(self.Id);
        if (member == null || !StaffText.IsGod(member.Role))
            return;
        if (client.LockedByOther(EditScope.Trial(watched), out _))
            return;

        Participant? participant = snapshot.Participant(watched);
        if (participant == null)
            return;

        string sender = FirstName(named, message.Sender.TextValue);
        if (NamesMatch(sender, participant.FullName) || message.Sender.TextValue.Contains(participant.FullName, StringComparison.OrdinalIgnoreCase))
        {
            if (roll is >= 1 and <= TrialRules.MortalSides)
                client.ReportDie(participant.Id, "player", roll);
            return;
        }

        int godSides = TrialRules.DieSides(member.Role);
        bool own = NamesMatch(sender, self.Name)
            || message.Sender.TextValue.Contains(self.Name, StringComparison.OrdinalIgnoreCase)
            || (sender.Length == 0 && client.ExpectedOwnDice > 0);
        if (!own || roll < 1 || roll > godSides)
            return;

        if (sender.Length == 0)
            client.ExpectOwnDice(Math.Max(0, client.ExpectedOwnDice - 1));
        client.ReportDie(participant.Id, "god", roll);
    }

    public void RefreshLevels(OdysseyClient client, IReadOnlyList<PartyPresence> party, PartyPresence? target)
    {
        SessionSnapshot? snapshot = client.Snapshot;
        if (snapshot == null)
            return;

        Consider(client, snapshot, target);
        foreach (PartyPresence presence in party)
            Consider(client, snapshot, presence);
    }

    public static List<PartyPresence> ReadParty(IPartyList party)
    {
        var people = new List<PartyPresence>(party.Length);
        foreach (IPartyMember member in party)
        {
            string name = member.Name.ToString();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            string world = "";
            int level = 0;
            try
            {
                world = member.World.Value.Name.ToString() ?? "";
            }
            catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
            {
            }

            if (member.GameObject is IPlayerCharacter character)
                level = character.Level;

            people.Add(new PartyPresence(name, world, level));
        }

        return people;
    }

    public static bool TrySelf(IPlayerState state, out CharacterIdentity identity)
    {
        identity = new CharacterIdentity("", "");
        if (!state.IsLoaded)
            return false;

        try
        {
            string name = state.CharacterName.ToString();
            string world = state.HomeWorld.Value.Name.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(world))
                return false;

            identity = new CharacterIdentity(name, world);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
        {
            return false;
        }
    }

    public static bool TryTarget(ITargetManager targets, out PartyPresence presence)
    {
        presence = default;
        if (targets.Target is not IPlayerCharacter character)
            return false;

        try
        {
            string name = character.Name.TextValue;
            string world = character.HomeWorld.Value.Name.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(name))
                return false;

            presence = new PartyPresence(name, world, character.Level);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
        {
            return false;
        }
    }

    private void Consider(OdysseyClient client, SessionSnapshot snapshot, PartyPresence? presence)
    {
        if (presence == null || presence.Value.Level <= 0)
            return;

        Participant? participant = PartyMatcher.Find(snapshot, presence.Value.Name, presence.Value.World);
        if (participant == null || participant.Level == presence.Value.Level)
            return;
        if (_levels.TryGetValue(participant.Id, out int sent) && sent == presence.Value.Level)
            return;

        _levels[participant.Id] = presence.Value.Level;
        client.UpdateLevel(participant.Id, presence.Value.Level);
    }

    private static bool TryReadRoll(SeString message, out int roll, out string name)
    {
        roll = 0;
        name = "";
        bool sawIcon = false;
        var afterIcon = new StringBuilder();
        foreach (Payload payload in message.Payloads)
        {
            if (payload is PlayerPayload player && name.Length == 0)
                name = player.PlayerName;
            if (!sawIcon)
            {
                if (IsDiceIcon(payload))
                    sawIcon = true;
                continue;
            }

            if (payload is TextPayload text)
                afterIcon.Append(' ').Append(text.Text);
        }

        string source = afterIcon.Length > 0 ? afterIcon.ToString() : message.TextValue;
        int? value = DiceText.ReadRoll(source);
        if (value is not > 0)
            return false;

        roll = value.Value;
        return true;
    }

    private static bool IsDiceIcon(Payload payload)
    {
        if (payload is IconPayload icon && icon.Icon.ToString().Contains("Dice", StringComparison.OrdinalIgnoreCase))
            return true;
        string label = payload.ToString() ?? "";
        return label.Contains("Dice", StringComparison.OrdinalIgnoreCase)
            && (payload is IconPayload || label.Contains("Icon", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPartyChannel(XivChatType type)
    {
        string name = type.ToString();
        return name.Contains("Party", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstName(string embedded, string sender)
    {
        string cleanSender = sender.Trim().Trim('"');
        return embedded.Length > 0 ? embedded.Trim() : cleanSender;
    }

    private static bool NamesMatch(string sender, string fullName)
    {
        string clean = sender.Trim().Trim('"');
        if (clean.Length == 0 || fullName.Length == 0)
            return false;
        if (clean.Equals(fullName, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!clean.StartsWith(fullName, StringComparison.OrdinalIgnoreCase))
            return false;

        return clean.Length == fullName.Length || !char.IsLetter(clean[fullName.Length]);
    }
}
