using System.Linq;
using ImGuiNET;

namespace FlipCup;

public sealed class MainWindow
{
    private readonly Plugin plugin;
    internal bool IsOpen = false;

    public MainWindow(Plugin plugin) => this.plugin = plugin;

    public void Draw()
    {
        if (!IsOpen) return;

        if (ImGui.Begin("FlipCup", ref IsOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var c = plugin.Config;

            if (ImGui.BeginTabBar("tabs"))
            {
                if (ImGui.BeginTabItem("Stats"))
                {
                    ImGui.Text($"House Profit: {Configuration.FormatGil(c.HouseProfit)}");
                    ImGui.Text($"Jackpot: {Configuration.FormatGil(c.Jackpot)}");
                    ImGui.Separator();

                    ImGui.Text("Top Players (Net Winnings):");
                    foreach (var p in c.Players.Values.OrderByDescending(p => p.NetWinnings).Take(10))
                        ImGui.BulletText($"{p.Name} — {p.NetWinnings:N0} gil | Games: {p.GamesPlayed} | Cups: {p.TotalCupsCleared}");

                    if (ImGui.Button("Reset Stats/Jackpot"))
                        c.ResetStats();

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Config"))
                {
                    // Simple live-edit of phrases + entry
                    var entry = c.EntryCostGil; 
                    if (ImGui.InputInt("Entry Cost", ref entry))
                        c.EntryCostGil = entry < 0 ? 0 : entry;

                    ImGui.InputText("Start Phrase", ref c.StartPhrase, 512);
                    ImGui.InputText("Jackpot Phrase", ref c.JackpotPhrase, 256);
                    ImGui.InputText("Cup Prompt", ref c.CupPromptPhrase, 256);
                    ImGui.InputText("Cup Success", ref c.CupSuccessPhrase, 256);
                    ImGui.InputText("Cup Fail", ref c.CupFailPhrase, 256);
                    ImGui.InputText("Win Phrase", ref c.WinPhrase, 256);
                    ImGui.InputText("Jackpot Win", ref c.JackpotWinPhrase, 256);

                    if (ImGui.Button("Save Configuration"))
                        c.Save();

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }
        ImGui.End();
    }
}
