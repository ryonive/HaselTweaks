using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using AddonExp = HaselTweaks.Structs.AddonExp;
using PlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;

namespace HaselTweaks.Tweaks;

public class EnhancedExpBarConfiguration
{
    [BoolConfig]
    public bool ForcePvPSeriesBar = true;

    [BoolConfig]
    public bool ForceSanctuaryBar = true;

    [BoolConfig]
    public bool ForceCompanionBar = true;

    [BoolConfig]
    public bool SanctuaryBarHideJob = false;

    [EnumConfig]
    public EnhancedExpBar.MaxLevelOverrideType MaxLevelOverride = EnhancedExpBar.MaxLevelOverrideType.Default;

    [BoolConfig]
    public bool DisableColorChanges = false;
}

[Tweak, IncompatibilityWarning("SimpleTweaksPlugin", "ShowExperiencePercentage")]
public unsafe partial class EnhancedExpBar : Tweak<EnhancedExpBarConfiguration>
{
    public enum MaxLevelOverrideType
    {
        Default,
        PvPSeriesBar,
        CompanionBar,
        // No SanctuaryBar, because data is only available on the island
    }

    public override void Enable()
    {
        Service.ClientState.LeavePvP += ClientState_LeavePvP;
        Service.ClientState.TerritoryChanged += ClientState_TerritoryChanged;
        _isEnabled = true;
        RunUpdate();
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_Exp", AddonExp_PostRequestedUpdate);
    }

    public override void Disable()
    {
        Service.ClientState.LeavePvP -= ClientState_LeavePvP;
        Service.ClientState.TerritoryChanged -= ClientState_TerritoryChanged;
        _isEnabled = false;
        RunUpdate();
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_Exp", AddonExp_PostRequestedUpdate);
    }

    public override void OnConfigChange(string fieldName)
    {
        if (TryGetAddon<AddonExp>("_Exp", out var addon))
        {
            addon->ClassJob--;
            addon->RequiredExp--;
            addon->AtkUnitBase.OnUpdate(
                AtkStage.GetSingleton()->GetNumberArrayData(),
                AtkStage.GetSingleton()->GetStringArrayData());
        }

        RunUpdate();
    }

    private bool _isEnabled = false;
    private bool _isUpdatePending = false;
    private ushort _lastSeriesXp = 0;
    private byte _lastSeriesClaimedRank = 0;
    private uint _lastBuddyXp;
    private byte _lastBuddyRank;
    private uint _lastBuddyObjectID;
    private uint _lastIslandExperience = 0;
    private ushort _lastSyncedFateId = 0;

    public override void OnFrameworkUpdate()
    {
        var pvpProfile = PvPProfile.Instance();
        if (pvpProfile != null && pvpProfile->IsLoaded == 0x01 && (_lastSeriesXp != pvpProfile->SeriesExperience || _lastSeriesClaimedRank != pvpProfile->SeriesClaimedRank))
        {
            _lastSeriesXp = pvpProfile->SeriesExperience;
            _lastSeriesClaimedRank = pvpProfile->SeriesClaimedRank;
            _isUpdatePending = true;
        }

        var buddy = UIState.Instance()->Buddy;
        if (_lastBuddyXp != buddy.CurrentXP || _lastBuddyRank != buddy.Rank || _lastBuddyObjectID != buddy.Companion.ObjectID)
        {
            _lastBuddyXp = buddy.CurrentXP;
            _lastBuddyRank = buddy.Rank;
            _lastBuddyObjectID = buddy.Companion.ObjectID;
            _isUpdatePending = true;
        }

        var mjiManager = MJIManager.Instance();
        if (mjiManager != null && _lastIslandExperience != mjiManager->IslandState.CurrentXP)
        {
            _lastIslandExperience = mjiManager->IslandState.CurrentXP;
            _isUpdatePending = true;
        }

        var fateManager = FateManager.Instance();
        if (fateManager != null && _lastSyncedFateId != fateManager->SyncedFateId)
        {
            _lastSyncedFateId = fateManager->SyncedFateId;
            _isUpdatePending = true;
        }

        if (_isUpdatePending)
            RunUpdate();
    }

    // request update immediately upon leaving the pvp area, because
    // otherwise it might get updated too late, like a second after the black screen ends
    private void ClientState_LeavePvP()
        => _isUpdatePending = true;

    private void ClientState_TerritoryChanged(ushort territoryType)
        => _isUpdatePending = true;

    private void RunUpdate()
    {
        if (!TryGetAddon<AddonExp>("_Exp", out var addon))
            return;

        HandleAddonExpPostRequestedUpdate(addon);
    }

    private void AddonExp_PostRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        if (!_isEnabled || type != AddonEvent.PostRequestedUpdate)
            return;

        HandleAddonExpPostRequestedUpdate((AddonExp*)args.Addon);
    }

    private void HandleAddonExpPostRequestedUpdate(AddonExp* addon)
    {
        if (addon == null)
            return;

        var gaugeBarNode = GetNode<AtkComponentNode>(&addon->AtkUnitBase, 6);
        if (gaugeBarNode == null)
            return;

        var gaugeBar = (Structs.AtkComponentGaugeBar*)gaugeBarNode->Component;
        if (gaugeBar == null)
            return;

        var nineGridNode = GetNode<AtkNineGridNode>(gaugeBarNode->Component, 4);
        if (nineGridNode == null)
            return;

        if (Service.ClientState.LocalPlayer == null)
        {
            ResetColor(nineGridNode);
            return;
        }

        if (Service.ClientState.LocalPlayer.ClassJob.GameData == null)
        {
            ResetColor(nineGridNode);
            return;
        }

        var leftText = GetNode<AtkTextNode>(&addon->AtkUnitBase, 4);
        if (leftText == null)
        {
            ResetColor(nineGridNode);
            return;
        }

        // --- forced bars in certain locations

        if (Config.ForceCompanionBar && UIState.Instance()->Buddy.Companion.ObjectID != 0xE0000000)
        {
            HandleCompanionBar(nineGridNode, gaugeBar, leftText);
            return;
        }

        if (Config.ForcePvPSeriesBar && GameMain.IsInPvPArea())
        {
            HandlePvPBar(nineGridNode, gaugeBar, leftText);
            return;
        }

        if (Config.ForceSanctuaryBar && GameMain.Instance()->CurrentTerritoryIntendedUseId == 49)
        {
            HandleSanctuaryBar(nineGridNode, gaugeBar, leftText);
            return;
        }

        // --- max level overrides

        if (Service.ClientState.LocalPlayer.Level == PlayerState.Instance()->MaxLevel)
        {
            if (Config.MaxLevelOverride == MaxLevelOverrideType.PvPSeriesBar)
            {
                HandlePvPBar(nineGridNode, gaugeBar, leftText);
                return;
            }

            if (Config.MaxLevelOverride == MaxLevelOverrideType.CompanionBar)
            {
                HandleCompanionBar(nineGridNode, gaugeBar, leftText);
                return;
            }
        }

        // nothing matches so let it fetch fresh data
        addon->ClassJob--;
        addon->RequiredExp--;
        ResetColor(nineGridNode);

        _isUpdatePending = false;
    }

    private void HandleCompanionBar(AtkNineGridNode* nineGridNode, Structs.AtkComponentGaugeBar* gaugeBar, AtkTextNode* leftText)
    {
        var buddy = UIState.Instance()->Buddy;
        if (buddy.Rank > GetRowCount<BuddyRank>() - 1)
        {
            ResetColor(nineGridNode);
            return;
        }

        var currentRank = buddy.Rank;

        var job = Service.ClientState.LocalPlayer!.ClassJob.GameData!.Abbreviation;
        var levelLabel = (GetAddonText(4968) ?? "Rank").Trim().Replace(":", "");
        var rank = currentRank > 20 ? 20 : currentRank;
        var level = rank.ToString().Aggregate("", (str, chr) => str + (char)(SeIconChar.Number0 + byte.Parse(chr.ToString())));
        var requiredExperience = GetRow<BuddyRank>(currentRank)!.ExpRequired;

        var xpText = requiredExperience == 0 ? "" : $"   {buddy.CurrentXP}/{requiredExperience}";
        leftText->SetText($"{job}  {levelLabel} {level}{xpText}");

        gaugeBar->SetSecondaryValue(0); // rested experience bar

        // max value is set to 10000 in AddonExp_OnSetup and we won't change that, so adjust
        gaugeBar->SetValue((uint)(buddy.CurrentXP / (float)requiredExperience * 10000), 0, false);

        ResetColor(nineGridNode);

        _isUpdatePending = false;
    }

    private void HandlePvPBar(AtkNineGridNode* nineGridNode, Structs.AtkComponentGaugeBar* gaugeBar, AtkTextNode* leftText)
    {
        var pvpProfile = PvPProfile.Instance();
        if (pvpProfile == null || pvpProfile->IsLoaded != 0x01)
        {
            ResetColor(nineGridNode);
            return;
        }

        if (pvpProfile->SeriesCurrentRank > GetRowCount<PvPSeriesLevel>() - 1)
        {
            ResetColor(nineGridNode);
            return;
        }

        var claimedRank = pvpProfile->GetSeriesClaimedRank();
        var currentRank = pvpProfile->GetSeriesCurrentRank();

        var job = Service.ClientState.LocalPlayer!.ClassJob.GameData!.Abbreviation;
        var levelLabel = (GetAddonText(14860) ?? "Series Level").Trim().Replace(":", "");
        var rank = currentRank > 30 ? 30 : currentRank; // 30 = Series Max Rank, hopefully in the future too
        var level = rank.ToString().Aggregate("", (str, chr) => str + (char)(SeIconChar.Number0 + byte.Parse(chr.ToString())));
        var star = currentRank > claimedRank ? '*' : ' ';
        var requiredExperience = GetRow<PvPSeriesLevel>(currentRank)!.Unknown0;

        leftText->SetText($"{job}  {levelLabel} {level}{star}   {pvpProfile->SeriesExperience}/{requiredExperience}");

        gaugeBar->SetSecondaryValue(0); // rested experience bar

        // max value is set to 10000 in AddonExp_OnSetup and we won't change that, so adjust
        gaugeBar->SetValue((uint)(pvpProfile->SeriesExperience / (float)requiredExperience * 10000), 0, false);

        if (!Config.DisableColorChanges)
        {
            // trying to make it look like the xp bar in the PvP Profile window and failing miserably. eh, good enough
            nineGridNode->AtkResNode.MultiplyRed = 65;
            nineGridNode->AtkResNode.MultiplyGreen = 35;
        }
        else
        {
            ResetColor(nineGridNode);
        }

        _isUpdatePending = false;
    }

    private void HandleSanctuaryBar(AtkNineGridNode* nineGridNode, Structs.AtkComponentGaugeBar* gaugeBar, AtkTextNode* leftText)
    {
        var mjiManager = MJIManager.Instance();
        if (mjiManager == null)
        {
            ResetColor(nineGridNode);
            return;
        }

        if (mjiManager->IslandState.CurrentRank > GetRowCount<MJIRank>() - 1)
        {
            ResetColor(nineGridNode);
            return;
        }

        var job = Config.SanctuaryBarHideJob ? "" : Service.ClientState.LocalPlayer!.ClassJob.GameData!.Abbreviation + "  ";
        var levelLabel = (GetAddonText(14252) ?? "Sanctuary Rank").Trim().Replace(":", "");
        var level = mjiManager->IslandState.CurrentRank.ToString().Aggregate("", (str, chr) => str + (char)(SeIconChar.Number0 + byte.Parse(chr.ToString())));
        var requiredExperience = GetRow<MJIRank>(mjiManager->IslandState.CurrentRank)!.ExpToNext;

        var expStr = mjiManager->IslandState.CurrentXP.ToString();
        var reqExpStr = requiredExperience.ToString();
        if (requiredExperience == 0)
        {
            expStr = reqExpStr = "--";
        }

        leftText->SetText($"{job}{levelLabel} {level}   {expStr}/{reqExpStr}");

        gaugeBar->SetSecondaryValue(0); // rested experience bar

        // max value is set to 10000 in AddonExp_OnSetup and we won't change that, so adjust
        gaugeBar->SetValue((uint)(mjiManager->IslandState.CurrentXP / (float)requiredExperience * 10000), 0, false);

        if (!Config.DisableColorChanges)
        {
            // blue seems nice.. just like the sky ^_^
            nineGridNode->AtkResNode.MultiplyRed = 25;
            nineGridNode->AtkResNode.MultiplyGreen = 60;
            nineGridNode->AtkResNode.MultiplyBlue = 255;
        }
        else
        {
            ResetColor(nineGridNode);
        }

        _isUpdatePending = false;
    }

    private void ResetColor(AtkNineGridNode* nineGridNode)
    {
        if (nineGridNode->AtkResNode.MultiplyRed != 100)
            nineGridNode->AtkResNode.MultiplyRed = 100;

        if (nineGridNode->AtkResNode.MultiplyGreen != 100)
            nineGridNode->AtkResNode.MultiplyGreen = 100;

        if (nineGridNode->AtkResNode.MultiplyBlue != 100)
            nineGridNode->AtkResNode.MultiplyBlue = 100;
    }
}
