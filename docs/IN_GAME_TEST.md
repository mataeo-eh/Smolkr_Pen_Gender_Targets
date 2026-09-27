# In-game test for 0.2.0

`0.2.0` has been built, unit-tested, and statically checked against the
Test_Mods profile's interop assemblies. It has **not** been run in game. This
test covers the things only the running game can show:

1. the row appears in the right place and works;
2. the villager slaughters the smolkr the mod picks (the `out` parameter
   reaches the game);
3. the target survives a save and reload.

## Setup

```powershell
# from the Aska_Mods harness root; mod.json targets the Test_Mods profile
.\scripts\deploy.ps1 -Mod Smolkr_Pen_Gender_Targets
```

Use a world with a smolkr pen that has a villager assigned, a slaughter tool
available, and room in the meat/leather/horn storages.

## 1. The row

1. Open the pen's workstation menu and open its slaughter task.
2. Under the slaughter limit there should be a second row, **Females to keep
   (off - vanilla 50/50)**, reading **Off**.
3. Press **+**. It should read **0** with the title **Females to keep (males:
   N)**, where N is the limit. Keep pressing; the males count drops as the
   females count rises, and the value stops at the limit.
4. Lower the slaughter limit with the game's own control. The row's maximum
   should follow it, and a value above the new limit should be capped.
5. Press **-** until it reads **Off** again. The pen is back to vanilla.
6. Check that changing the row never moves the slaughter limit.

If the row is missing, run `watch-log.ps1` and look for `row_unavailable` or
`row_failed`. If it overlaps something, the log line `SPGT event=row_layout`
describes the surrounding UI. Send that line, or set `RowOffsetX/Y` in the
config.

## 2. The slaughter

The mod only slaughters when vanilla would (pen over its limit, pups included),
so set up a pen that is over its limit with a ratio vanilla would get "wrong".
For example, with 4 adult females and 3 adult males, set the limit to 6 and
females to keep to 5. Vanilla would take a female (the majority). The mod should
take a male.

After the villager works, run:

```powershell
.\scripts\watch-log.ps1 -Mod Smolkr_Pen_Gender_Targets
```

Expected, for the same `pen=`:

```text
SPGT event=targets_changed pen=... keep_f=5 limit=6
SPGT event=slaughter_selected pen=... reason=MaleChosen victim_female=False ...
SPGT event=slaughter_confirmed pen=... female=False
```

| Log shows | Meaning |
| --- | --- |
| `slaughter_selected` then `slaughter_confirmed` | Working. |
| `slaughter_selected` then `smolkr_died_unselected` with the other gender | The `out` value did not reach the game, so vanilla's choice was killed. Report this. |
| No `slaughter_selected` at all, but smolkr still die | The prefix is not running. Check for `Harmony patches installed.` near startup. |
| `selection_failed` | The patch threw; the full exception follows it. Vanilla ran instead. |
| `slaughter_declined reason=WithinLimit` | Working. The pen is not over its limit, so vanilla would not slaughter either. |

## 3. Save and reload

1. With a target set, save and quit to the menu.
2. Reload the world. The log should show
   `SPGT event=targets_loaded pen=... keep_f=<your number>`.
3. Reopen the slaughter task. The row should show the same number.

## Undo

Set `Enabled = false` under `[General]` in
`BepInEx\config\aska.smolkr.pen.gender.targets.cfg` to return every pen to
vanilla without uninstalling. Removing the mod entirely is also safe: the one
extra key it saves is ignored by the game.
