using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Plugin;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RetainerSales;

public sealed unsafe class RetainerSales : IDalamudPlugin
{
    private IDalamudPluginInterface PluginInterface { get; init; }
    private IPluginLog PluginLog { get; init; }
    public Configuration Configuration { get; init; }
    public IAddonLifecycle AddonLifecycle { get; init; }
    public IClientState ClientState { get; init; }

    private Dictionary<string, byte> RetainerSaleNumbers { get; set; } = new();
    private Dictionary<string, byte> RetainerIndex { get; set; } = new();
    private string OpenedRetainer { get; set; } = string.Empty;

    public RetainerSales(
        IDalamudPluginInterface pluginInterface,
        IPluginLog pluginLog,
        IAddonLifecycle addonLifecycle,
        IClientState clientState)
    {
        PluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        PluginLog = pluginLog ?? throw new ArgumentNullException(nameof(pluginLog));
        AddonLifecycle = addonLifecycle ?? throw new ArgumentNullException(nameof(addonLifecycle));
        ClientState = clientState ?? throw new ArgumentNullException(nameof(clientState));

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        try
        {
            AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "RetainerList", HandleOpen);
            AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "RetainerList", HandleRefresh);

            AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectString", HandleRetainerSelect);
        }
        catch (Exception e)
        {
            PluginLog.Error(e.Message);
        }
    }

    private void HandleOpen(AddonEvent type, AddonArgs args)
    {
        PluginLog.Verbose("RetainerList Opened");
        try
        {
            var manager = RetainerManager.Instance();
            for (uint i = 0; i < manager->GetRetainerCount(); ++i)
            {
                var retainer = manager->GetRetainerBySortedIndex(i);

                if (!Configuration.ItemsForSale.ContainsKey(retainer->NameString))
                    Configuration.ItemsForSale[retainer->NameString] = retainer->MarketItemCount;

                RetainerSaleNumbers[retainer->NameString] = retainer->MarketItemCount;
                
                if (Configuration.ItemsForSale[retainer->NameString] > retainer->MarketItemCount)
                {
                    if (OpenedRetainer != retainer->NameString)
                    {
                        RetainerIndex[retainer->NameString] = (byte)i;
                    }
                    else
                    {
                        OpenedRetainer = "";
                        Configuration.ItemsForSale[retainer->NameString] = retainer->MarketItemCount;
                        Configuration.Save();
                    }
                }
                else if (Configuration.ItemsForSale[retainer->NameString] < retainer->MarketItemCount)
                {
                    Configuration.ItemsForSale[retainer->NameString] = retainer->MarketItemCount;
                    Configuration.Save();
                }
            }
        }
        catch (Exception e)
        {
            PluginLog.Error(e.Message);
        }
    }

    private void HandleRefresh(AddonEvent type, AddonArgs args)
    {
        try
        {
            var addon = (AtkUnitBase*)args.Addon;
            var retainerListNode = (AtkComponentNode*)addon->GetNodeById(27);

            foreach (var ret in RetainerIndex)
            {
                var retRow = GetNodeByID<AtkComponentNode>(&retainerListNode->Component->UldManager,
                                                           ret.Value == 0 ? 4u : 41000u + ret.Value,
                                                           (NodeType)1011);

                var saleColumn = (AtkTextNode*)retRow->Component->UldManager.SearchNodeById(11);
                var saleText = saleColumn->NodeText.ToString();
                if (!saleText.Contains("<<<"))
                    saleColumn->SetText($"{saleText} <<<");
            }
        }
        catch (Exception e)
        {
            PluginLog.Error(e.Message);
        }
    }
    
    private void HandleRetainerSelect(AddonEvent type, AddonArgs args)
    {
        try
        {
            var addon = (AtkUnitBase*)args.Addon;
            var titleTextNode = (AtkTextNode*)addon->GetNodeById(2);

            var titleText = titleTextNode->NodeText.ToString();
            var curr = CurrencyManager.Instance()->ItemBucket[21072].Count;
            PluginLog.Verbose($"{curr} ventures");

            if (!titleText.Contains(curr.ToString()))
            {
                PluginLog.Verbose($"Addon does not appear to be retainer screen, ignoring.");
                return;
            }
            
            var retName = ClientState.ClientLanguage switch
            {
                ClientLanguage.Japanese => titleText.Split('\r').Last().Replace("）", "").Replace("（", ""),
                ClientLanguage.German => titleText.Split("Du hast ").Last().Split(" herbeigerufen").First(),
                ClientLanguage.French => titleText.Split("Menu de ").Last().Split(" [").First(),
                ClientLanguage.English => titleText.Split('\r').First().Split(": ").Last(),
                _ => ""
            };

#pragma warning disable CA1854
            if (!RetainerSaleNumbers.ContainsKey(retName))
#pragma warning restore CA1854
            {
                PluginLog.Error($"Could not find retainer name, got {retName}. Create an issue in the github or send a dm to @populo on discord with a screenshot of this error.");
            }
            else
            {
                PluginLog.Info($"Retainer: {retName}");

                OpenedRetainer = retName;
                RetainerIndex.Remove(retName);
                Configuration.ItemsForSale[retName] = RetainerSaleNumbers[retName];
                Configuration.Save();
            }
            
        }
        catch (Exception e)
        {
            PluginLog.Error(e.Message);
        }
    }
    
    // ripped from simple tweaks (https://github.com/Caraxi/SimpleTweaksPlugin/blob/7758e07bd836f7d4ceb78ee87a6723170cc0b5fb/Utility/Common.cs#L269)
    private T* GetNodeByID<T>(AtkUldManager* uldManager, uint nodeId, NodeType? type = null) where T : unmanaged {
        if (uldManager == null) return null;
        if (uldManager->NodeList == null) return null;
        for (var i = 0; i < uldManager->NodeListCount; i++) {
            var n = uldManager->NodeList[i];
            if (n == null || n->NodeId != nodeId || type != null && n->Type != type.Value) continue;
            return (T*)n;
        }

        return null;
    }
    
    public void Dispose()
    {
        AddonLifecycle.UnregisterListener(HandleOpen, HandleRefresh, HandleRetainerSelect);
    }
}
