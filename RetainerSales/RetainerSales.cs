using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Game;
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

    private Dictionary<string, int> RetainerSaleNumbers { get; set; } = new();
    private Dictionary<string, uint> RetainerIndex { get; set; } = new();

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
            PluginLog.Fatal(e.Message);
        }
    }

    private void HandleOpen(AddonEvent type, AddonArgs args)
    {
        PluginLog.Info("RetainerList Opened");
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
                    RetainerIndex[retainer->NameString] = i;
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
            PluginLog.Fatal(e.Message);
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
                                                           ret.Value == 0 ? 4 : 41000 + ret.Value,
                                                           (NodeType)1011);

                var saleColumn = (AtkTextNode*)retRow->Component->UldManager.SearchNodeById(11);
                var saleText = saleColumn->NodeText.ToString();
                if (!saleText.Contains("<<<"))
                    saleColumn->SetText($"{saleText} <<<");
            }
        }
        catch (Exception e)
        {
            PluginLog.Fatal(e.Message);
        }
    }
    
    private void HandleRetainerSelect(AddonEvent type, AddonArgs args)
    {
        try
        {
            var addon = (AtkUnitBase*)args.Addon;
            var titleTextNode = (AtkTextNode*)addon->GetNodeById(2);

            var titleText = titleTextNode->NodeText.ToString();
            var retName = ClientState.ClientLanguage switch
            {
                ClientLanguage.Japanese => titleText.Split('\r').Last().Replace("）", "").Replace("（", ""),
                ClientLanguage.German => titleText.Split("Du hast ").Last().Split(" herbeigerufen").First(),
                ClientLanguage.French => titleText.Split("Menu de ").Last().Split(" [").First(),
                ClientLanguage.English => titleText.Split('\r').First().Split(": ").Last(),
                _ => ""
            };
            PluginLog.Verbose(retName);

            RetainerIndex.Remove(retName);
            Configuration.ItemsForSale[retName] = RetainerSaleNumbers[retName];
            Configuration.Save();
        }
        catch (Exception e)
        {
            PluginLog.Fatal(e.Message);
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
