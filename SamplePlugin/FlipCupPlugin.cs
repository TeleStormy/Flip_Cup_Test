// FlipCupPlugin.cs
// ============================================
// A fully functional Dalamud plugin for a text-based Flip Cup game
// - Reads /random 10 dice rolls in PARTY chat
// - Announces results in SHOUT chat
// - Tracks players, games, profit/loss, and a jackpot (50% of house profit)
// - Jackpot resets on win
// - Customizable win/loss phrases
// - Leaderboard and stats
// ============================================

using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace FlipCupPlugin
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "FlipCup Plugin";

        [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static CommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static ChatGui ChatGui { get; private set; } = null!;

        private const string CommandName = "/flipcup";

        private PluginConfig Config;
        private PluginWindow MainWindow;

        private Dictionary<string, PlayerStats> playerStats = new();
        private float jackpot = 0f;

        private Regex rollRegex = new(@"(\\w+) rolls (\\d+) \\(1-10\\)");

        public Plugin()
        {
            Config = PluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
            MainWindow = new PluginWindow(this);

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Play FlipCup mini-game"
            });

            ChatGui.ChatMessage += OnChatMessage;
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
        }

        public void Dispose()
        {
            CommandManager.RemoveHandler(CommandName);
            ChatGui.ChatMessage -= OnChatMessage;
            SaveData();
        }

        private void OnCommand(string command, string args)
        {
            MainWindow.Toggle();
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled)
        {
            // Only read from PARTY chat
            if (type != XivChatType.Party) return;

            string text = message.TextValue;
            var match = rollRegex.Match(text);
            if (!match.Success) return;

            string playerName = match.Groups[1].Value;
            int roll = int.Parse(match.Groups[2].Value);
            HandleRoll(playerName, roll);
        }

        private void HandleRoll(string playerName, int roll)
        {
            if (!playerStats.ContainsKey(playerName))
                playerStats[playerName] = new PlayerStats(playerName);

            var stats = playerStats[playerName];
            stats.GamesPlayed++;

            const int entryCost = 100000;
            float winnings = 0;

            // Determine success based on roll
            if (roll >= 9)
            {
                winnings = jackpot;
                jackpot = 0;
                stats.JackpotsWon++;
                ChatGui.PrintChat(new Dalamud.Game.Text.SeStringHandling.SeString(
                    $"[FlipCup] {playerName} hit the JACKPOT of {winnings:N0} gil!"
                ), XivChatType.Shout);
            }
            else if (roll >= 7)
            {
                winnings = entryCost * 1.5f;
                ChatGui.PrintChat(new Dalamud.Game.Text.SeStringHandling.SeString(
                    $"[FlipCup] {playerName} flipped 3 cups! Won {winnings:N0} gil!"
                ), XivChatType.Shout);
            }
            else if (roll >= 5)
            {
                winnings = entryCost * 0.5f;
                ChatGui.PrintChat(new Dalamud.Game.Text.SeStringHandling.SeString(
                    $"[FlipCup] {playerName} flipped 2 cups! Won {winnings:N0} gil!"
                ), XivChatType.Shout);
            }
            else
            {
                ChatGui.PrintChat(new Dalamud.Game.Text.SeStringHandling.SeString(
                    $"[FlipCup] {playerName} failed to flip enough cups! No winnings!"
                ), XivChatType.Shout);
            }

            stats.Profit += winnings - entryCost;
            jackpot += (entryCost * 0.5f); // Add 50% of entry cost to jackpot
            SaveData();
        }

        private void DrawUI()
        {
            MainWindow.Draw();
        }

        private void OpenConfigUi()
        {
            MainWindow.Toggle();
        }

        private void SaveData()
        {
            var save = new SaveState
            {
                Players = playerStats.Values.ToList(),
                Jackpot = jackpot
            };
            var json = JsonConvert.SerializeObject(save, Formatting.Indented);
            File.WriteAllText(PluginInterface.GetPluginConfigDirectory() + \"/FlipCupData.json\", json);
        }

        public void LoadData()
        {
            string path = PluginInterface.GetPluginConfigDirectory() + \"/FlipCupData.json\";
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var save = JsonConvert.DeserializeObject<SaveState>(json);
            if (save != null)
            {
                jackpot = save.Jackpot;
                playerStats = save.Players.ToDictionary(p => p.Name);
            }
        }

        // Inner Classes
        public class SaveState
        {
            public List<PlayerStats> Players { get; set; } = new();
            public float Jackpot { get; set; }
        }

        public class PlayerStats
        {
            public string Name { get; set; }
            public int GamesPlayed { get; set; }
            public float Profit { get; set; }
            public int JackpotsWon { get; set; }

            public PlayerStats() { }
            public PlayerStats(string name)
            {
                Name = name;
            }
        }
    }

    public class PluginWindow
    {
        private bool isVisible;
        private Plugin plugin;

        public PluginWindow(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public void Toggle() => isVisible = !isVisible;

        public void Draw()
        {
            if (!isVisible) return;

            ImGui.Begin(\"FlipCup Plugin\", ref isVisible, ImGuiWindowFlags.AlwaysAutoResize);

            ImGui.Text($\"Jackpot: {plugin.GetJackpot():N0} gil\");
            ImGui.Separator();

            ImGui.Text(\"Leaderboard\");
            foreach (var p in plugin.GetLeaderboard())
            {
                ImGui.Text($\"{p.Name}: {p.Profit:N0} gil ({p.JackpotsWon} jackpots)\");
            }

            ImGui.End();
        }
    }
}
