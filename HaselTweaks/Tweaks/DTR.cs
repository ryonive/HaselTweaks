using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using HaselCommon.Extensions;
using HaselCommon.Structs;
using HaselCommon.Utils;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using GameFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace HaselTweaks.Tweaks;

public class DTRConfiguration
{
    public string FormatUnitText = " fps";
}

[Tweak]
public unsafe class DTR : Tweak<DTRConfiguration>
{
    public override void DrawConfig()
    {
        ImGuiUtils.DrawSection(t("HaselTweaks.Config.SectionTitle.Configuration"));

        ImGui.TextUnformatted(t("DTR.Config.Explanation.Pre"));
        ImGuiUtils.TextUnformattedColored(HaselColor.From(ImGuiColors.DalamudRed), t("DTR.Config.Explanation.DalamudSettings"));
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        if (ImGui.IsItemClicked())
        {
            static void OpenSettings()
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    Service.Framework.RunOnTick(OpenSettings, delayTicks: 2);
                    return;
                }

                Service.CommandManager.ProcessCommand("/xlsettings");
            }
            Service.Framework.RunOnTick(OpenSettings, delayTicks: 2);
        }
        ImGuiUtils.SameLineSpace();
        ImGui.TextUnformatted(t("DTR.Config.Explanation.Post"));

        ImGuiUtils.DrawSection(t("HaselTweaks.Config.SectionTitle.Configuration"));

        ImGui.TextUnformatted(t("DTR.Config.FormatUnitText.Label"));
        if (ImGui.InputText("##FormatUnitTextInput", ref Config.FormatUnitText, 20))
        {
            Plugin.Config.Save();
            _lastFrameRate = 0; // trigger update
        }
        ImGui.SameLine();
        if (ImGuiUtils.IconButton("##Reset", FontAwesomeIcon.Undo, t("HaselTweaks.Config.ResetToDefault", " fps")))
        {
            Config.FormatUnitText = " fps";
            Plugin.Config.Save();
        }
        if (Service.TranslationManager.TryGetTranslation("DTR.Config.FormatUnitText.Description", out var description))
        {
            ImGuiHelpers.SafeTextColoredWrapped(Colors.Grey, description);
        }
    }

    public DtrBarEntry? DtrInstance;
    public DtrBarEntry? DtrFPS;
    public DtrBarEntry? DtrBusy;
    private int _lastFrameRate;

    public override void Enable()
    {
        DtrInstance = Service.DtrBar.Get("[HaselTweaks] Instance");
        DtrFPS = Service.DtrBar.Get("[HaselTweaks] FPS");
        DtrBusy = Service.DtrBar.Get("[HaselTweaks] Busy",
            new SeString(
                new UIForegroundPayload(1),
                new UIGlowPayload(16),
                new TextPayload(GetRow<OnlineStatus>(12)?.Name.ToDalamudString().ToString()),
                UIGlowPayload.UIGlowOff,
                UIForegroundPayload.UIForegroundOff));
    }

    public override void Disable()
    {
        DtrInstance?.Dispose();
        DtrFPS?.Dispose();
        DtrBusy?.Dispose();
    }

    public override void OnFrameworkUpdate()
    {
        UpdateInstance();
        UpdateFPS();
        UpdateBusy();
    }

    private void UpdateInstance()
    {
        if (DtrInstance == null)
            return;

        var uiState = UIState.Instance();
        if (uiState == null)
        {
            DtrInstance.SetVisibility(false);
            return;
        }

        var instanceId = uiState->AreaInstance.Instance;
        if (instanceId <= 0 || instanceId >= 10)
        {
            DtrInstance.SetVisibility(false);
            return;
        }

        DtrInstance.SetText(((char)(SeIconChar.Instance1 + (byte)(instanceId - 1))).ToString());
        DtrInstance.SetVisibility(true);
    }

    private void UpdateBusy()
    {
        if (DtrBusy == null)
            return;

        DtrBusy.SetVisibility(Service.ClientState.LocalPlayer?.OnlineStatus.Id == 12);
    }

    private void UpdateFPS()
    {
        if (DtrFPS == null)
            return;

        var gameFramework = GameFramework.Instance();
        if (gameFramework == null)
        {
            DtrFPS.SetVisibility(false);
            return;
        }

        var frameRate = (int)(gameFramework->FrameRate + 0.5f);
        if (_lastFrameRate != frameRate)
        {
            DtrFPS.SetText(t("DTR.FPS.Format", frameRate, Config.FormatUnitText));
            DtrFPS.SetVisibility(true);
            _lastFrameRate = frameRate;
        }
    }
}
