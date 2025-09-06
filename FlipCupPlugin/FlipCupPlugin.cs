using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace FlipCup;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "FlipCup";

    // Dalamud services via DI
    [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static CommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static ChatGui ChatGui { get; private set; } = null!;

    private const string Command = "/flipcup";

    internal Configuration Config { get; private set; } = null!;
    internal MainWindow MainWindow { get; private set; } = null!;

    // Active sessions keyed by player name
    private readonly Dictionary<string, GameSession> sessions = new(StringComparer.OrdinalIgnoreCase);

    // Accepts “Player rolls a 7 (1-10).” and “Player rolls 7 (1-10).”
    private readonly Regex rollRx = new(@"(?i)rolls(?:\s+a)?\s+(?<v>\d+)\s+\((?<min>\d+)-(?<max>\d+)\)\.?",
        RegexOptions.Compiled);

    public Plugin()
    {
        // Load config
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(PluginInterface);

        // Windows
        MainWindow = new MainWindow(this);

        // Hooks
        PluginInterface.UiBuilder.Draw += MainWindow.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += () => MainWindow.IsOpen = true;

        // Command
        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "FlipCup controls. Use '/flipcup help'."
        });

        // Chat
        ChatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        ChatGui.ChatMessage -= OnChatMessage;
        CommandManager.RemoveHandler(Command);
        PluginInterface.UiBuilder.Draw -= MainWindow.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= () => MainWindow.IsOpen = true;
        Config.Save();
    }

    private void OnCommand(string _, string args)
    {
        var a = (args ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (a.Length == 0)
        {
            MainWindow.IsOpen = true;
            return;
        }

        switch (a[0].ToLowerInvariant())
        {
            case "help":
                Print($"Commands: " +
                      $"{Command} start <player> | cancel <player> | stats | reset");
                break;

            case "start":
                if (a.Length < 2) { Print("Usage: /flipcup start <player>"); return; }
                var name = string.Join(' ', a.Skip(1));
                StartGame(name);
                break;

            case "cancel":
                if (a.Length < 2) { Print("Usage: /flipcup cancel <player>"); return; }
                var c = string.Join(' ', a.Skip(1));
                if (sessions.Remove(c))
                    Print($"Cancelled {c}'s game.");
                break;

            case "stats":
                MainWindow.IsOpen = true;
                break;

            case "reset":
                sessions.Clear();
                Config.ResetStats();
                Print("FlipCup: Stats & jackpot reset.");
                break;

            default:
                Print("Unknown. Try '/flipcup help'.");
                break;
        }
    }

    private void OnChatMessage(
        XivChatType type,
        uint senderId,
        ref SeString message,
        ref string sender,
        ref bool isHandled)
    {
        // Only /party rolls are accepted
        if (type != XivChatType.Party)
            return;

        var text = message.TextValue;
        var m = rollRx.Match(text);
        if (!m.Success) return;

        if (!int.TryParse(m.Groups["v"].Value, out var value)) return;
        if (!int.TryParse(m.Groups["min"].Value, out var min)) return;
        if (!int.TryParse(m.Groups["max"].Value, out var max)) return;
        if (min != 1 || max != 10) return; // we only use d10

        ProcessRoll(sender, value);
    }

    // ===== Game Flow =====

    private void StartGame(string player)
    {
        if (sessions.ContainsKey(player))
        {
            Print($"{player} already has an active game.");
            return;
        }

        sessions[player] = new GameSession { Player = player, CupIndex = 0, Active = true };

        // House receives entry
        Config.HouseProfit += Config.EntryCostGil;
        Config.EnsurePlayer(player).GamesPlayed++;

        // Announce in party: welcome + current jackpot
        SendParty(Format(Config.StartPhrase, player, 0));
        SendParty(Format(Config.JackpotPhrase, player, 0));
        PromptCup(player);

        Config.Save();
    }

    private void PromptCup(string player)
    {
        if (!sessions.TryGetValue(player, out var s) || !s.Active) return;
        SendParty(Format(Config.CupPromptPhrase, player, s.CupIndex));
    }

    private void ProcessRoll(string roller, int value)
    {
        if (!sessions.TryGetValue(roller, out var s) || !s.Active) return;

        int need = Config.CupThresholds[s.CupIndex];
        if (value >= need)
        {
            SendParty(Format(Config.CupSuccessPhrase, roller, s.CupIndex));
            s.CupIndex++;

            if (s.CupIndex >= 4)
            {
                EndGame(roller, 4);
                return;
            }

            PromptCup(roller);
        }
        else
        {
            SendParty(Format(Config.CupFailPhrase, roller, s.CupIndex));
            EndGame(roller, s.CupIndex);
        }
    }

    private void EndGame(string player, int cupsCleared)
    {
        sessions.Remove(player);

        var payout = Config.Payouts.TryGetValue(cupsCleared, out var p) ? p : 0;

        // Jackpot if 4 cups
        int jackpotAward = 0;
        if (cupsCleared == 4)
        {
            jackpotAward = (int)Config.Jackpot;
            payout = jackpotAward; // jackpot replaces regular payout
            Config.Jackpot = 0;
            SendShout(Format(Config.JackpotWinPhrase, player, 3)); // index 3 used only for formatting {CUP}=4
        }

        // House pays out
        Config.HouseProfit -= payout;

        // Player net
        var ps = Config.EnsurePlayer(player);
        ps.TotalCupsCleared += cupsCleared;
        ps.NetWinnings += payout - Config.EntryCostGil;

        // History
        Config.History.Add(new GameRecord
        {
            When = DateTime.UtcNow,
            Player = player,
            CupsCleared = cupsCleared,
            EntryCost = Config.EntryCostGil,
            Payout = payout,
            JackpotAward = jackpotAward
        });

        // Profit after the game (entries already added earlier)
        var profitDelta = Config.EntryCostGil - payout - jackpotAward;
        if (profitDelta > 0)
            Config.Jackpot += (int)Math.Floor(profitDelta * 0.5); // 50% of profit flows to jackpot

        // Announce result
        var phrase = Format(Config.WinPhrase, player, cupsCleared - 1)
                     .Replace("{PAYOUT}", payout.ToString("N0"));
        SendShout(phrase);

        // Persist
        Config.Save();
    }

    // ===== Helpers =====

    private static string Format(string template, string player, int cupIndex)
    {
        // cupIndex 0..3 corresponds to need thresholds
        return template
            .Replace("{PLAYER}", player)
            .Replace("{ENTRY}", Configuration.FormatGil(Configuration.Current.EntryCostGil))
            .Replace("{CUP}", (cupIndex + 1).ToString())
            .Replace("{NEED}", Configuration.Current.CupThresholds[cupIndex].ToString())
            .Replace("{JACKPOT}", Configuration.FormatGil(Configuration.Current.Jackpot));
    }

    private static void Print(string s) => ChatGui.Print($"[FlipCup] {s}");
    private static void SendParty(string s) => ChatGui.SendMessage("/p " + s);
    private static void SendShout(string s) => ChatGui.SendMessage("/sh " + s);

    // ===== Session =====
    private sealed class GameSession
    {
        public string Player = "";
        public int CupIndex; // 0..3
        public bool Active;
    }
}
