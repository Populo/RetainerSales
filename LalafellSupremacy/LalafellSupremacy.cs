using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using LalafellSupremacy.Windows;
using SamplePlugin;

namespace LalafellSupremacy;

public sealed class LalafellSupremacy : IDalamudPlugin
{
    private const string CommandName = "/lala";

    private DalamudPluginInterface PluginInterface { get; init; }
    private ICommandManager CommandManager { get; init; }
    private IPluginLog PluginLog { get; init; }
    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("LalafellSupremacy");
    private ConfigWindow ConfigWindow { get; init; }
    private Dictionary<string, byte> MaximumCustomizationNumbers { get; set; }

    private readonly byte MasterRace = 3;
    
    private unsafe delegate CharacterBase* CreateDelegate(uint modelId, CustomizeData* customize, EquipmentModelId* equipData, byte unk);
    
    private Hook<CreateDelegate>? _createHook;

    public LalafellSupremacy(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] ICommandManager commandManager,
        [RequiredVersion("1.0")] IPluginLog pluginLog,
        [RequiredVersion("1.0")] IGameInteropProvider interopProvider,
        [RequiredVersion("1.0")] ISigScanner sigScanner)
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;
        PluginLog = pluginLog;

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        MaximumCustomizationNumbers = new Dictionary<string, byte>()
        {
            { "tribe", 2 },
            { "face", 4 },
            { "jaw", 4 },
            { "eyebrows", 6 },
            { "nose", 6 },

        };

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open config menu"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        try
        {
            PluginLog.Verbose("Creating hook");
            IntPtr addr = sigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 4E 08 48 8B D0 4C 8B 01");
            unsafe
            {
                _createHook = interopProvider.HookFromAddress<CreateDelegate>(addr, DetourCreate);
            }

            _createHook?.Enable();
            PluginLog.Verbose("Created hook");
        }
        catch (Exception e)
        {
            PluginLog.Fatal(e.Message);
        }
    }

    private unsafe CharacterBase* DetourCreate(
        uint modelId, CustomizeData* customize, EquipmentModelId* equipData, byte unk)
    {
        try
        {
            if (modelId == 0 && Configuration.Enabled)
            {
                AdjustAttributes(customize);
            }
        }
        catch (Exception e)
        {
            PluginLog.Error($"Could not convert, race is {customize->Race}, modelId is {modelId}");
        }
        
        return _createHook!.Original(modelId, customize, equipData, unk);
    }

    private unsafe void AdjustAttributes(CustomizeData* customize)
    {
        customize->Race = MasterRace;
        customize->Face %= MaximumCustomizationNumbers["face"];
        customize->Jaw %= MaximumCustomizationNumbers["jaw"];
        customize->Eyebrows %= MaximumCustomizationNumbers["eyebrows"];
        customize->TailShape = 1;
        customize->Nose %= MaximumCustomizationNumbers["nose"];
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        
        _createHook?.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleConfigUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
}
