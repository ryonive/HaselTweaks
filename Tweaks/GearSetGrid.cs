using System.Collections.Generic;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Component.GUI;
using HaselTweaks.Windows;
using Lumina.Excel.GeneratedSheets;

namespace HaselTweaks.Tweaks;

[Tweak(
    Name: "Gear Set Grid",
    Description: "A window that displays a grid view of all the items of each gear set."
)]
public unsafe partial class GearSetGrid : Tweak
{
    private GearSetGridWindow? _window;
    private const string GSGCommand = "/gsg";

    public static Configuration Config => Plugin.Config.Tweaks.GearSetGrid;
    public Dictionary<short, (uint Min, uint Max)> MaxLevelRanges { get; init; } = new();

    public class Configuration
    {
        [ConfigField(Label = "Auto-open/close with Gear Set List")]
        public bool AutoOpenWithGearSetList = true;

        [ConfigField(Label = "Register /gsg command to toggle window", OnChange = nameof(OnConfigChange))]
        public bool RegisterCommand = true;

        [ConfigField(Label = "Allow switching gearsets", Description = "Makes the id column clickable.")]
        public bool AllowSwitchingGearsets = true;

        [ConfigField(Label = "Convert separator gear set with spacing", Description = "When using separator gear sets (e.g. a gearset with name ===========) this option automatically converts it into spacing between rows (in the Gear Set Grid).")]
        public bool ConvertSeparators = true;

        [ConfigField(Label = "Gear set name of the separator", DependsOn = nameof(ConvertSeparators), DefaultValue = "===========")]
        public string SeparatorFilter = "===========";

        [ConfigField(Label = "Disable spacing", DependsOn = nameof(ConvertSeparators))]
        public bool DisableSeparatorSpacing = false;
    }

    public GearSetGrid()
    {
        short level = 50;
        foreach (var exVersion in Service.Data.GetExcelSheet<ExVersion>()!)
        {
            var entry = (Min: uint.MaxValue, Max: 0u);

            foreach (var item in Service.Data.GetExcelSheet<Item>()!)
            {
                if (item.LevelEquip != level || item.LevelItem.Row <= 1)
                    continue;

                if (entry.Min > item.LevelItem.Row)
                    entry.Min = item.LevelItem.Row;

                if (entry.Max < item.LevelItem.Row)
                    entry.Max = item.LevelItem.Row;
            }

            MaxLevelRanges.Add(level, entry);
            level += 10;
        }
    }

    private void OnConfigChange()
    {
        UnregisterCommand();
        RegisterCommands();
    }

    public override void Enable()
    {
        RegisterCommands();

        if (Config.AutoOpenWithGearSetList && GetAddon("GearSetList", out var addon))
            OnAddonOpen("GearSetList", addon);
    }

    public override void Disable()
    {
        UnregisterCommand(true);
        CloseWindow();
    }

    public override void OnAddonOpen(string addonName, AtkUnitBase* unitbase)
    {
        if (Config.AutoOpenWithGearSetList && addonName == "GearSetList")
            OpenWindow();
    }

    public override void OnAddonClose(string addonName, AtkUnitBase* unitbase)
    {
        if (Config.AutoOpenWithGearSetList && addonName == "GearSetList")
            CloseWindow();
    }

    private void RegisterCommands()
    {
        if (Config.RegisterCommand)
        {
            Service.Commands.RemoveHandler(GSGCommand);
            Service.Commands.AddHandler(GSGCommand, new CommandInfo(OnGsgCommand)
            {
                HelpMessage = $"Usage: {GSGCommand} <id>",
                ShowInHelp = true
            });
        }
    }

    private static void UnregisterCommand(bool forceRemoval = false)
    {
        if (!Config.RegisterCommand || forceRemoval)
        {
            Service.Commands.RemoveHandler(GSGCommand);
        }
    }

    private void OnGsgCommand(string command, string arguments)
    {
        ToggleWindow();
    }

    private void ToggleWindow()
    {
        if (_window == null)
        {
            Plugin.WindowSystem.AddWindow(_window = new(this));
            _window.IsOpen = true;
        }
        else
        {
            _window.IsOpen = !_window.IsOpen;
        }
    }

    private void OpenWindow()
    {
        if (_window == null)
            Plugin.WindowSystem.AddWindow(_window = new(this));

        _window.IsOpen = true;
    }

    private void CloseWindow()
    {
        if (_window == null)
            return;

        Plugin.WindowSystem.RemoveWindow(_window);
        _window = null;
    }
}