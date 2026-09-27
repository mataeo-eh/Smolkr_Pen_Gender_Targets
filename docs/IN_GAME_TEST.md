# In-game test for 0.1.0

`0.1.0` has been built, unit-tested, and statically checked against the game's
interop assemblies. It has **not** been run in game yet. This test covers the
three things only the running game can show:

1. the panel appears beside a pen's slaughter task;
2. the villager slaughters the smolkr the mod picks (the `out` parameter
   reaches the game);
3. the target survives a save and reload.

## Setup

```powershell
# from the Aska_Mods harness root
.\scripts\deploy.ps1 -Mod Smolkr_Pen_Gender_Targets
```

Use a world with a smolkr pen that has a villager assigned, a slaughter tool
available, and room in the meat/leather/horn storages. Ideally the pen holds
both adult males and adult females.

## 1. The panel

1. Open the pen's workstation menu and select the slaughter task.
2. A window titled **Smolkr gender targets** should appear on the right.
3. Tick **Use gender targets for this pen**. Females to keep starts at
   "limit - 1" and males to keep at 1.
4. Change the game's slaughter limit with its own control. **Slaughter limit**
   and **Males to keep** in the window should follow it.
5. Close the menu. The window should disappear with it.

If the window never appears, check the log for `SPGT event=task_panel_failed`.

## 2. The slaughter

Set targets so exactly one gender is over. With 3 adult females and 3 adult
males, for example, set the limit to 5 and females to keep to 1: males to keep
becomes 4, so the females are the surplus. **Next slaughter** should read *An
adult female*.

Let the villager work, then run:

```powershell
.\scripts\watch-log.ps1 -Mod Smolkr_Pen_Gender_Targets
```

Expected, for the same `pen=`:

```text
SPGT event=slaughter_selected pen=... reason=SurplusFemale victim_female=True ...
SPGT event=slaughter_confirmed pen=... female=True
```

What other outcomes mean:

| Log shows | Meaning |
| --- | --- |
| `slaughter_selected` then `slaughter_confirmed` | Working. |
| `slaughter_selected` then `smolkr_died_unselected` with the other gender | The `out` value did not reach the game, so vanilla's choice was killed. Report this; the patch point is right but the hand-back failed. |
| No `slaughter_selected` at all, but smolkr still die | The prefix is not running. Check for `Harmony patches installed.` near startup. |
| `selection_failed` | The patch threw; the full exception follows it. Vanilla ran instead. |
| `slaughter_declined reason=WithinTargets` | Working. Nothing was over target. |

## 3. Save and reload

1. With targets on, save and quit to the menu.
2. Reload the world. The log should show
   `SPGT event=targets_loaded pen=... keep_f=<your number>`.
3. Reopen the slaughter task. The window should show the same numbers.

## Undo

Set `Enabled = false` under `[General]` in
`BepInEx\config\aska.smolkr.pen.gender.targets.cfg` to return every pen to
vanilla without uninstalling. Removing the mod entirely is also safe: the one
extra key it saves is ignored by the game.
