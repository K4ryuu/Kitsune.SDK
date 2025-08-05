using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Base;
using Menu;
using Menu.Enums;
using Microsoft.Extensions.Logging;

namespace K4Zenith.Stats;

public partial class StatsPlugin
{
    private KitsuneMenu? _kitsuneMenu;

    private void InitializeMenu()
    {
        try
        {
            _kitsuneMenu = new KitsuneMenu(this);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("KitsuneMenu not available, falling back to chat display: {Error}", ex.Message);
            _kitsuneMenu = null;
        }
    }

    private void ShowStatsMenu(CCSPlayerController player, PlayerStatsData statsData)
    {
        if (_kitsuneMenu != null && Config.UseHtmlMenu)
        {
            ShowCenterStatsMenu(player, statsData);
        }
        else
        {
            ShowChatStatsMenu(player, statsData);
        }
    }

    private void ShowCenterStatsMenu(CCSPlayerController player, PlayerStatsData statsData)
    {
        if (_kitsuneMenu == null)
        {
            ShowChatStatsMenu(player, statsData);
            return;
        }

        List<MenuItem> items = [];

        // Add calculated stats first (KD, KDA, KPR, Accuracy)
        if (statsData.Deaths > 0 || statsData.Kills > 0)
        {
            string kd = CalculateKD(player);
            items.Add(new MenuItem(MenuItemType.Text, new MenuValue($"<font color='#FF6666'>{Localizer.ForPlayer(player, "k4.stats.kd")}:</font> {kd}")));
        }

        if (statsData.Deaths > 0 || statsData.Kills > 0 || statsData.Assists > 0)
        {
            string kda = CalculateKDA(player);
            items.Add(new MenuItem(MenuItemType.Text, new MenuValue($"<font color='#FF6666'>{Localizer.ForPlayer(player, "k4.stats.kda")}:</font> {kda}")));
        }

        if (statsData.RoundsOverall > 0)
        {
            string kpr = CalculateKPR(player);
            items.Add(new MenuItem(MenuItemType.Text, new MenuValue($"<font color='#FF6666'>{Localizer.ForPlayer(player, "k4.stats.kpr")}:</font> {kpr}")));
        }

        if (statsData.Shoots > 0)
        {
            string accuracy = CalculateAccuracy(player);
            items.Add(new MenuItem(MenuItemType.Text, new MenuValue($"<font color='#FF6666'>{Localizer.ForPlayer(player, "k4.stats.accuracy")}:</font> {accuracy}")));
        }

        var statsDataType = typeof(PlayerStatsData);

        // Then add raw statistics using reflection
        foreach (var property in statsDataType.GetProperties())
        {
            if (property.PropertyType == typeof(int))
            {
                var value = (int)property.GetValue(statsData)!;

                // Only show non-zero values
                if (value > 0)
                {
                    var snakeCase = string.Concat(property.Name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
                    var localizationKey = $"k4.stats.{snakeCase}";
                    var displayText = $"<font color='#FF6666'>{Localizer.ForPlayer(player, localizationKey)}:</font> {value:N0}";

                    // Add special formatting for certain properties
                    if (property.Name == "Headshots" && statsData.Kills > 0)
                    {
                        var hsPercent = (double)statsData.Headshots / statsData.Kills * 100;
                        displayText = $"<font color='#FF6666'>{Localizer.ForPlayer(player, localizationKey)}:</font> {value:N0} ({hsPercent:F1}%)";
                    }

                    items.Add(new MenuItem(MenuItemType.Text, new MenuValue(displayText)));
                }
            }
        }

        if (items.Count == 0)
        {
            items.Add(new MenuItem(MenuItemType.Text, new MenuValue($"<font color='#FF6666'>{Localizer.ForPlayer(player, "k4.stats.no_stats")}</font>")));
        }

        _kitsuneMenu.ShowScrollableMenu(player, Localizer.ForPlayer(player, "k4.stats.title"), items, (buttons, menu, selected) =>
        {
            // No selection handle as all items are just for display
        }, false, false, disableDeveloper: true);
    }

    private void ShowChatStatsMenu(CCSPlayerController player, PlayerStatsData statsData)
    {
        var chatMenu = new ChatMenu(Localizer.ForPlayer(player, "k4.stats.title"));

        // Always add an "All Statistics" option first
        chatMenu.AddMenuOption(Localizer.ForPlayer(player, "k4.stats.show_all"), (selectedPlayer, option) =>
        {
            ShowAllStatsInChat(selectedPlayer, statsData);
        });

        chatMenu.Open(player);
    }

    private void ShowAllStatsInChat(CCSPlayerController player, PlayerStatsData statsData)
    {
        var chatMenu = new ChatMenu(Localizer.ForPlayer(player, "k4.stats.title"));

        // Add calculated stats first (KD, KDA, KPR, Accuracy)
        if (statsData.Deaths > 0 || statsData.Kills > 0)
        {
            string kd = CalculateKD(player);
            chatMenu.AddMenuOption($"{Localizer.ForPlayer(player, "k4.stats.kd")}: {kd}", (selectedPlayer, option) => { }, true);
        }

        if (statsData.Deaths > 0 || statsData.Kills > 0 || statsData.Assists > 0)
        {
            string kda = CalculateKDA(player);
            chatMenu.AddMenuOption($"{Localizer.ForPlayer(player, "k4.stats.kda")}: {kda}", (selectedPlayer, option) => { }, true);
        }

        if (statsData.RoundsOverall > 0)
        {
            string kpr = CalculateKPR(player);
            chatMenu.AddMenuOption($"{Localizer.ForPlayer(player, "k4.stats.kpr")}: {kpr}", (selectedPlayer, option) => { }, true);
        }

        if (statsData.Shoots > 0)
        {
            string accuracy = CalculateAccuracy(player);
            chatMenu.AddMenuOption($"{Localizer.ForPlayer(player, "k4.stats.accuracy")}: {accuracy}", (selectedPlayer, option) => { }, true);
        }

        var statsDataType = typeof(PlayerStatsData);

        // Then add raw statistics using reflection
        foreach (var property in statsDataType.GetProperties())
        {
            if (property.PropertyType == typeof(int))
            {
                var value = (int)property.GetValue(statsData)!;

                // Only show non-zero values
                if (value > 0)
                {
                    var snakeCase = string.Concat(property.Name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
                    var localizationKey = $"k4.stats.{snakeCase}";

                    // Add special formatting for certain properties
                    if (property.Name == "Headshots" && statsData.Kills > 0)
                    {
                        var hsPercent = (double)statsData.Headshots / statsData.Kills * 100;
                        chatMenu.AddMenuOption($"{Localizer.ForPlayer(player, localizationKey)}: {value:N0} ({hsPercent:F1}%)", (selectedPlayer, option) => { }, true);
                    }
                    else
                    {
                        chatMenu.AddMenuOption($"{Localizer.ForPlayer(player, localizationKey)}: {value:N0}", (selectedPlayer, option) => { }, true);
                    }
                }
            }
        }

        chatMenu.Open(player);
    }
}