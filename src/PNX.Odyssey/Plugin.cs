using System.Numerics;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Pnx.Odyssey.Core;
using Pnx.Odyssey.Services;
using Pnx.Odyssey.UI;

namespace Pnx.Odyssey;

public sealed class Plugin : IDalamudPlugin
{
    private readonly ICommandManager _commands;
    private readonly IChatGui _chat;
    private readonly IFramework _framework;
    private readonly IPartyList _party;
    private readonly IObjectTable _objects;
    private readonly ITargetManager _targets;
    private readonly IGameGui _gameGui;
    private readonly IPlayerState _playerState;
    private readonly INotificationManager _notifications;
    private readonly WindowSystem _windows = new("PNXOdyssey");
    private readonly TrialWatcher _watcher = new();
    private readonly MainWindow _main;
    private readonly ConfigWindow _configWindow;
    private readonly ChatWindow _chatWindow;
    private readonly HashSet<string> _marks = new(StringComparer.OrdinalIgnoreCase);
    private CharacterIdentity? _self;
    private bool _resumeArmed = true;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commands,
        IChatGui chat,
        IFramework framework,
        IPartyList party,
        IObjectTable objects,
        ITargetManager targets,
        IGameGui gameGui,
        IPlayerState playerState,
        IPluginLog log,
        INotificationManager notifications)
    {
        _commands = commands;
        _chat = chat;
        _framework = framework;
        _party = party;
        _objects = objects;
        _targets = targets;
        _gameGui = gameGui;
        _playerState = playerState;
        _notifications = notifications;

        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(pluginInterface);
        Client = new OdysseyClient(log, Config);
        Client.Configure(Configuration.ServerAddress, Configuration.PluginKeyValue);
        Ui.LoadNameFont(pluginInterface.UiBuilder.FontAtlas);
        Ui.LoadItalicFont(pluginInterface.UiBuilder.FontAtlas);

        _main = new MainWindow(this);
        _configWindow = new ConfigWindow(this);
        _chatWindow = new ChatWindow(this);
        _windows.AddWindow(_main);
        _windows.AddWindow(_configWindow);
        _windows.AddWindow(_chatWindow);
        _windows.AddWindow(new MarkerOverlay(this));

        pluginInterface.UiBuilder.Draw += _windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenMain;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        commands.AddHandler("/odyssey", new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the PNX Odyssey trial table.",
        });
        chat.ChatMessage += OnChat;
        framework.Update += OnUpdate;
    }

    public Configuration Config { get; }

    internal OdysseyClient Client { get; }

    public void OpenMain() => _main.IsOpen = true;

    public void OpenConfig()
    {
        _configWindow.Load();
        _configWindow.IsOpen = true;
    }

    public void ToggleChat() => _chatWindow.IsOpen = !_chatWindow.IsOpen;

    public bool ChatOpen => _chatWindow.IsOpen;

    public IReadOnlyList<PartyPresence> ReadParty() => TrialWatcher.ReadParty(_party);

    public bool TryTarget(out PartyPresence presence) => TrialWatcher.TryTarget(_targets, out presence);

    public bool HasMarks => _marks.Count > 0;

    public bool IsMarked(string participantId) => _marks.Contains(participantId);

    public void ToggleMark(string participantId)
    {
        if (!_marks.Add(participantId))
            _marks.Remove(participantId);
    }

    public bool TargetParticipant(Participant participant)
    {
        if (TryFind(participant, out IPlayerCharacter? character))
        {
            _targets.Target = character;
            return true;
        }

        _notifications.AddNotification(new Notification
        {
            Title = "PNX Odyssey",
            Content = $"{participant.FullName} is not nearby.",
            Type = NotificationType.Warning,
            InitialDuration = TimeSpan.FromSeconds(3),
        });
        return false;
    }

    public Vector3? LocalChest()
    {
        if (_objects.Length == 0 || _objects[0] is not IPlayerCharacter local)
            return null;
        return local.Position + new Vector3(0f, 1.2f, 0f);
    }

    public IEnumerable<Vector3> MarkedPositions()
    {
        SessionSnapshot? snapshot = Client.Snapshot;
        if (snapshot == null)
            yield break;

        foreach (string id in _marks)
        {
            Participant? participant = snapshot.Participant(id);
            if (participant != null && TryFind(participant, out IPlayerCharacter? character) && character != null)
                yield return character.Position + new Vector3(0f, 1.1f, 0f);
        }
    }

    public IEnumerable<(Participant Player, Vector3 Head)> NearbyRegistered()
    {
        SessionSnapshot? snapshot = Client.Snapshot;
        if (snapshot == null)
            yield break;

        foreach (Participant participant in snapshot.Participants)
        {
            if (TryFind(participant, out IPlayerCharacter? character) && character != null)
                yield return (participant, character.Position + new Vector3(0f, BarHeight(character), 0f));
        }
    }

    private float BarHeight(IGameObject actor)
    {
        float offset = Config.OverlayOffset;
        float baseline = Config.OverlayClamp ? Config.OverlayHeight : ActorHeight(actor);
        return baseline + offset;
    }

    private static unsafe float ActorHeight(IGameObject actor)
    {
        if (actor.Address == nint.Zero)
            return 1.8f;

        float height = ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)actor.Address)->GetHeight();
        return float.IsNaN(height) || height < 0.3f || height > 5f ? 1.8f : height;
    }

    public bool Project(Vector3 world, out Vector2 screen) => _gameGui.WorldToScreen(world, out screen);

    private bool TryFind(Participant participant, out IPlayerCharacter? character)
    {
        foreach (IGameObject gameObject in _objects)
        {
            if (gameObject is not IPlayerCharacter player)
                continue;

            string name;
            string world;
            try
            {
                name = player.Name.TextValue;
                world = player.HomeWorld.Value.Name.ToString() ?? "";
            }
            catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
            {
                continue;
            }

            if (PartyMatcher.SamePerson(name, world, participant))
            {
                character = player;
                return true;
            }
        }

        character = null;
        return false;
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
        _chat.ChatMessage -= OnChat;
        _commands.RemoveHandler("/odyssey");
        _windows.RemoveAllWindows();
        Ui.ReleaseNameFont();
        Client.Dispose();
    }

    private void OnCommand(string command, string args) => OpenMain();

    private void OnChat(IChatMessage message) => _watcher.OnChat(message, Client, _self);

    private void OnUpdate(IFramework framework)
    {
        if (TrialWatcher.TrySelf(_playerState, out CharacterIdentity identity))
        {
            _self = identity;
            Client.EnsureIdentity(identity);
        }

        Client.Tick(DateTime.UtcNow);
        if (!_main.IsOpen)
            Client.WatchedParticipantId = null;

        string? notice = Client.TakeNotice();
        if (notice != null)
        {
            _notifications.AddNotification(new Notification
            {
                Title = "PNX Odyssey",
                Content = notice,
                Type = NotificationType.Warning,
                InitialDuration = TimeSpan.FromSeconds(4),
            });
        }

        RememberSession();
    }

    private void RememberSession()
    {
        if (Client.ConsumeLeft())
        {
            Config.LastSessionId = "";
            Config.Save();
            _resumeArmed = false;
            return;
        }

        if (Client.Snapshot != null)
        {
            if (Config.LastSessionId != Client.Snapshot.Id)
            {
                Config.LastSessionId = Client.Snapshot.Id;
                Config.Save();
            }

            _resumeArmed = false;
            return;
        }

        if (Client.State == LinkState.Offline)
        {
            _resumeArmed = true;
            return;
        }

        if (_resumeArmed
            && Client.UsingServer
            && Client.State == LinkState.Connected
            && Config.LastSessionId.Length > 0)
        {
            _resumeArmed = false;
            Client.Join(Config.LastSessionId);
        }
    }
}
