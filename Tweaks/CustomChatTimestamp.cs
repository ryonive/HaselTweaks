using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Raii;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using HaselTweaks.Utils;
using ImGuiNET;

namespace HaselTweaks.Tweaks;

public unsafe partial class CustomChatTimestamp : Tweak
{
    public override string Name => "Custom Chat Timestamp";
    public override string Description => "As it says, configurable chat timestamp format.";
    public static Configuration Config => Plugin.Config.Tweaks.CustomChatTimestamp;

    public class Configuration
    {
        public string Format = "[HH:mm] ";
    }

    public override bool HasCustomConfig => true;
    public override void DrawCustomConfig()
    {
        ImGui.TextUnformatted("Format");
        using (ImGuiUtils.ConfigIndent())
        {
            if (ImGui.InputText("##HaselTweaks_CustomChatTimestamp_Format", ref Config.Format, 50))
            {
                Plugin.Config.Save();
                ReloadChat();
            }
            ImGui.SameLine();
            if (ImGuiUtils.IconButton("##HaselTweaks_CustomChatTimestamp_FormatReset", FontAwesomeIcon.Undo, "Reset to Default: \"[HH:mm] \""))
            {
                Config.Format = "[HH:mm] ";
                Plugin.Config.Save();
                ReloadChat();
            }

            ImGui.PushStyleColor(ImGuiCol.Text, (uint)Colors.Grey);
            ImGui.TextUnformatted("This gets passed to C#'s");
            ImGuiUtils.SameLineSpace();
            using (ImRaii.PushColor(ImGuiCol.Text, (uint)Colors.White))
            {
                ImGuiUtils.DrawLink("DateTime.ToString()", "Custom date and time format strings documentation", "https://docs.microsoft.com/dotnet/standard/base-types/custom-date-and-time-format-strings");
            }
            ImGuiUtils.SameLineSpace();
            ImGui.TextUnformatted("function.");
            ImGui.PopStyleColor();
        }

        if (string.IsNullOrWhiteSpace(Config.Format))
            return;

        try
        {
            var formatted = DateTime.Now.ToString(Config.Format);

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Example with the current format:");

            if (!Service.GameConfig.UiConfig.TryGet("ColorParty", out uint colorParty))
            {
                colorParty = 0xFFFFE666;
            }
            else
            {
                var alpha = (colorParty & 0xFF000000) >> 24;
                var red = (colorParty & 0x00FF0000) >> 16;
                var green = (colorParty & 0x0000FF00) >> 8;
                var blue = colorParty & 0x000000FF;

                colorParty = (alpha << 24) | (blue << 16) | (green << 8) | red;
            }

            var size = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetStyle().WindowPadding.Y * 2 + ImGui.GetTextLineHeight() + 2);
            using var child = ImRaii.Child("##HaselTweaks_CustomChatTimestamp_FormatExample", size, true);
            if (!child || !child.Success)
                return;

            ImGuiUtils.TextUnformattedColored(Colors.White, formatted);
            ImGui.SameLine(0, 0);
            ImGuiUtils.TextUnformattedColored(colorParty, $"(\uE090Player Name) This is a test message.");
        }
        catch (FormatException)
        {
            using (ImRaii.PushIndent())
            {
                ImGuiHelpers.SafeTextColoredWrapped(Colors.Red, "The current format is not valid.");
            }
        }
        catch (Exception e)
        {
            using (ImRaii.PushIndent())
            {
                ImGuiHelpers.SafeTextColoredWrapped(Colors.Red, e.Message);
            }
        }
    }

    public override void Enable()
    {
        ReloadChat();
    }

    public override void Disable()
    {
        ReloadChat();
    }

    [SigHook("E8 ?? ?? ?? ?? 48 8B D0 48 8B CB E8 ?? ?? ?? ?? 4C 8D 87")]
    private byte* FormatAddon(nint a1, ulong addonRowId, ulong value)
    {
        if (addonRowId is 7840 or 7841 && !string.IsNullOrWhiteSpace(Config.Format))
        {
            try
            {
                var str = (Utf8String*)(a1 + 0x9C0);
                var time = DateTime.UnixEpoch.AddSeconds(value).ToLocalTime();
                var formatted = time.ToString(Config.Format);

                MemoryHelper.WriteString((nint)str->StringPtr, formatted);
                str->BufUsed = formatted.Length + 1;
                str->StringLength = formatted.Length;

                return str->StringPtr;
            }
            catch (Exception e)
            {
                Error(e, "Error formatting Chat Timestamp");
            }
        }

        return FormatAddonHook.OriginalDisposeSafe(a1, addonRowId, value);
    }

    public void ReloadChat()
    {
        for (var i = 0; i < 4; i++)
            *(bool*)((nint)RaptureLogModule.Instance() + 0x33E8 + i) = true;
    }
}
