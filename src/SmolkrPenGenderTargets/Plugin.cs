// Plugin.cs
//
// Role in the larger system: the BepInEx entry point. Binds the config file,
// installs every Harmony patch in this assembly and creates the object that
// draws the in-game "gender targets" panel.
//
// What the mod does, in one paragraph: ASKA's smolkr pen has a single
// "slaughter limit". When the pen goes over it, vanilla slaughters an adult from
// whichever gender currently has more adults, which drifts every pen toward a
// 50/50 split. This mod adds a per-pen "females to keep" number (males to keep is
// the limit minus that) and, for pens where the player turns it on, replaces
// vanilla's choice with one that holds that ratio. See docs/DESIGN.md for how
// the vanilla code was read and why the patch sits where it does.
//
// Depends on: Runtime/* (pen reading, per-pen settings, logging),
// Patches/* (the Harmony patches), UI/GenderTargetsPanel (the panel).

using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using SmolkrPenGenderTargets.UI;
using UnityEngine;

namespace SmolkrPenGenderTargets;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "aska.smolkr.pen.gender.targets";
    public const string PluginName = "Smolkr Pen Gender Targets";
    public const string PluginVersion = "0.1.0";

    internal static new ManualLogSource Log { get; private set; }

    /// <summary>Master switch. False makes every pen behave exactly as vanilla.</summary>
    internal static ConfigEntry<bool> Enabled { get; private set; }

    /// <summary>Whether pups count toward their gender's target. See CullPlanner.</summary>
    internal static ConfigEntry<bool> CountPupsTowardTargets { get; private set; }

    /// <summary>Whether the panel is drawn beside the pen's task panel.</summary>
    internal static ConfigEntry<bool> ShowPanel { get; private set; }

    private Harmony _harmony;

    /// <summary>
    /// Called by BepInEx once at startup.
    /// Calls: <see cref="BindConfig"/>, <see cref="InstallPatches"/>, <see cref="CreatePanelHost"/>.
    /// </summary>
    public override void Load()
    {
        Log = base.Log;
        BindConfig();
        InstallPatches();
        CreatePanelHost();
        Log.LogInfo($"{PluginName} {PluginVersion} loaded (enabled={Enabled.Value}, count pups={CountPupsTowardTargets.Value}).");
    }

    /// <summary>
    /// Called by BepInEx when the plugin is unloaded. Removes the patches so the
    /// game is left exactly as it was found.
    /// </summary>
    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        return true;
    }

    /// <summary>Creates every config entry. Called by: <see cref="Load"/>.</summary>
    private void BindConfig()
    {
        Enabled = Config.Bind("General", "Enabled", true,
            "Master switch. When false, every smolkr pen slaughters exactly as in vanilla, " +
            "whatever each pen's gender targets are set to. The targets themselves are kept.");

        CountPupsTowardTargets = Config.Bind("Behaviour", "CountPupsTowardTargets", false,
            "False: targets count grown-up smolkr only. Pups are left alone and judged when they " +
            "grow up, so a breeding herd is never thinned to make room for its own young. " +
            "True: pups count toward their gender's target, closer to how the vanilla slaughter " +
            "limit counts every animal - but a newborn male can then get the herd's only adult " +
            "male slaughtered while he grows up.");

        ShowPanel = Config.Bind("Interface", "ShowPanel", true,
            "Show the gender targets panel next to a smolkr pen's slaughter task. Turning this off " +
            "hides the panel only; pens keep using the targets they already have.");
    }

    /// <summary>
    /// Applies every Harmony patch in this assembly.
    ///
    /// A failure is logged rather than thrown: a patch that cannot bind (because a
    /// game update renamed its target) should disable the mod, not stop the game
    /// from starting.
    /// </summary>
    private void InstallPatches()
    {
        try
        {
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Log.LogInfo("Harmony patches installed.");
        }
        catch (Exception e)
        {
            Log.LogError(
                "Failed to install Harmony patches - smolkr pens will behave as vanilla. " +
                "This usually means ASKA updated and the interop assemblies need regenerating. " +
                $"Details: {e}");
        }
    }

    /// <summary>
    /// Creates the persistent GameObject that hosts the panel.
    ///
    /// The panel type must be registered with Il2CppInterop before Unity can
    /// attach it, because it is a managed class used inside an IL2CPP runtime.
    /// Calls: ClassInjector.RegisterTypeInIl2Cpp, AddComponent.
    /// </summary>
    private void CreatePanelHost()
    {
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<GenderTargetsPanel>();

            var host = new GameObject("SmolkrPenGenderTargets.PanelHost");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<GenderTargetsPanel>();
        }
        catch (Exception e)
        {
            Log.LogError(
                "Could not create the gender targets panel. Pens that already have targets " +
                $"keep using them, but new targets cannot be set. Details: {e}");
        }
    }
}
