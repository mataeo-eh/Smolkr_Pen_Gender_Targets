// Plugin.cs
//
// Role in the larger system: the BepInEx entry point. Binds the config file and
// installs every Harmony patch in this assembly.
//
// What the mod does, in one paragraph: ASKA's smolkr and kyckling pens have a
// single "slaughter limit". When a pen goes over it (young included), vanilla
// slaughters an adult from whichever gender currently has more adults, which
// drifts every pen toward a 50/50 split. This mod adds a per-pen "females to
// keep" row to the slaughter task (males to keep is the limit minus that) and,
// for pens where the player sets it, slaughters from whichever gender is
// furthest above its target, counting young animals too. If the surplus gender
// has no adult available, slaughter waits. See docs/DESIGN.md.
//
// Smolkr and kyckling pens are the same game class (AnimalPen, told apart only
// by its kycklingPen flag) and slaughter through the same method, so every
// patch here serves both kinds of pen with no per-kind code.
//
// The GUID, assembly names and save key still say "smolkr": they are the
// identity of the already-published 1.0.0, and changing them would break
// updates, config files and saved targets.
//
// Depends on: Runtime/* (pen reading, per-pen settings, logging),
// Patches/* (the Harmony patches), UI/FemalesToKeepRow (the task panel row).

using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace SmolkrPenGenderTargets;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "aska.smolkr.pen.gender.targets";
    public const string PluginName = "Smolkr and Kyckling Pen Gender Limits";
    public const string PluginVersion = "1.1.1";

    internal static new ManualLogSource Log { get; private set; }

    /// <summary>Master switch. False makes every pen behave exactly as vanilla.</summary>
    internal static ConfigEntry<bool> Enabled { get; private set; }

    /// <summary>Whether the "Females to keep" row is added to the slaughter task.</summary>
    internal static ConfigEntry<bool> ShowRow { get; private set; }

    /// <summary>Extra horizontal shift for the row, for when it lands in the wrong place.</summary>
    internal static ConfigEntry<float> RowOffsetX { get; private set; }

    /// <summary>Extra vertical shift for the row, for when it lands in the wrong place.</summary>
    internal static ConfigEntry<float> RowOffsetY { get; private set; }

    private Harmony _harmony;

    /// <summary>
    /// Called by BepInEx once at startup.
    /// Calls: <see cref="BindConfig"/>, <see cref="InstallPatches"/>.
    /// </summary>
    public override void Load()
    {
        Log = base.Log;
        BindConfig();
        InstallPatches();
        Log.LogInfo($"{PluginName} {PluginVersion} loaded (enabled={Enabled.Value}).");
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
            "Master switch. When false, every smolkr and kyckling pen slaughters exactly as in vanilla, " +
            "whatever each pen's gender targets are set to. The targets themselves are kept.");

        ShowRow = Config.Bind("Interface", "ShowRow", true,
            "Add a 'Females to keep' row under the slaughter limit in a smolkr or kyckling pen's slaughter task. " +
            "Turning this off hides the row only; pens keep using the targets they already have.");

        RowOffsetX = Config.Bind("Interface", "RowOffsetX", 0f,
            "Moves the 'Females to keep' row sideways, in UI units. Only needed if the row overlaps " +
            "something after a game update; the default places it directly under the slaughter limit.");

        RowOffsetY = Config.Bind("Interface", "RowOffsetY", 0f,
            "Moves the 'Females to keep' row up (positive) or down (negative), in UI units. Only " +
            "needed if the row overlaps something after a game update.");
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
                "Failed to install Harmony patches - smolkr and kyckling pens will behave as vanilla. " +
                "This usually means ASKA updated and the interop assemblies need regenerating. " +
                $"Details: {e}");
        }
    }
}
