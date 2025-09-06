using System;
using System.Collections.Generic;
using Dalamud.Plugin;

namespace FlipCup;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // House and jackpot
    public long HouseProfit { get; set; } = 0;
    public long Jackpot { get; set; } = 0;

    // Entry + payouts
    public int EntryCostGil { get; set; } = 100_000;
    public int[] CupThresholds { get; set; } = new[] { 3, 5, 7, 9 }; // d10 thresholds
    public Dictionary<int, int> Payouts { get; set; } = new()
    {
        { 0, 0 }, { 1, 0 }, { 2, 150_000 }, { 3, 300_000 }, { 4, 0 } // 4 uses jackpot
    };

    // Phrases (customizable)
    public string StartPhrase { get; set; } =
        "{PLAYER}, welcome to FlipCup! Entry: {ENTRY}. Roll /random 10 for Cup 1 (need {NEED}+).";
    public string JackpotPhrase { get; set; } =
        "Current Jackpot is {JACKPOT}!";
    public string CupPromptPhrase { get; set; } =
        "Cup {CUP}: /random 10! Need {NEED}+.";
    public string CupSuccessPhrase { get; set; } =
        "✅ {PLAYER} flipped Cup {CUP}!";
    public string CupFailPhrase { get; set; } =
        "❌ {PLAYER} missed Cup {CUP}! Game over.";
    public string WinPhrase { get; set; } =
        "{PLAYER} cleared {CUP}/4 cups and wins {PAYOUT} gil!";
    public string JackpotWinPhrase { get; set; } =
        "🎉 JACKPOT! {PLAYER} cleared all 4 cups and takes {JACKPOT}!";

    // Stats
    public Dictionary<string, PlayerStats> Players { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<GameRecord> History { get; set; } = new();

    [NonSerialized] private DalamudPluginInterface? pi;
    public static Configuration Current { get; private set; } = null!;

    public void Initialize(DalamudPluginInterface pluginInterface)
    {
        pi = pluginInterface;
        Current = this;
    }

    public void Save() => pi?.SavePluginConfig(this);

    public void ResetStats()
    {
        HouseProfit = 0;
        Jackpot = 0;
        Players.Clear();
        History.Clear();
        Save();
    }

    public PlayerStats EnsurePlayer(string name)
    {
        if (!Players.TryGetValue(name, out var ps))
        {
            ps = new PlayerStats { Name = name };
            Players[name] = ps;
        }
        return ps;
    }

    public static string FormatGil(long v) => $"{v:N0} gil";
}

public sealed class PlayerStats
{
    public string Name { get; set; } = "";
    public int GamesPlayed { get; set; }
    public int TotalCupsCleared { get; set; }
    public long NetWinnings { get; set; }
}

public sealed class GameRecord
{
    public DateTime When { get; set; } = DateTime.UtcNow;
    public string Player { get; set; } = "";
    public int CupsCleared { get; set; }
    public int EntryCost { get; set; }
    public int Payout { get; set; }
    public int JackpotAward { get; set; }
}
