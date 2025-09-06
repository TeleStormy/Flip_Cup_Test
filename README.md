# FlipCup Dalamud Plugin


## Description
FlipCup is a text-based mini-game plugin for FFXIV using Dalamud. Players flip cups by rolling a /random 10 in party chat, with results announced in shout chat. The plugin tracks games, profit/loss, jackpot, player stats, and maintains a live leaderboard.


## Features
- 🎲 Dice rolls via `/random 10` in **Party chat**
- 📢 Results announced in **Shout chat**
- 💰 Jackpot system (50% of house profit, resets on win)
- 🏆 Leaderboard and player stats tracking
- 🖼️ ImGui interface for configuration & live stats
- 🔒 Persistent saving/loading of all data
- 🔧 Customizable win/loss/start phrase,


## Usage
- Use `/flipcup start <player>` in party chat to begin a game.
- Players roll `/random 10` in party chat for each cup.
- Results are announced in shout chat.
- Check ImGui UI for live stats, jackpot, and leaderboard.
- Reset stats with `/flipcup reset`.


## Configuration
- Open the configuration UI via the Plugin Manager or `/flipcup config`.
- Customize phrases for game start, cup success/failure, and wins.
- View current jackpot and player statistics in real-time.
