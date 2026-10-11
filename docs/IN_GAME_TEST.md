# In-game tests

## Newborn regression (1.1.1)

With a limit of 6 and female target 5, start with 5 adult females and 1 adult male.

- A female pup should produce `FemaleChosen`, followed by a confirmed female death. After maturation the pen should be 5F/1M.
- A male pup should produce `MaleChosen`, even when this removes the only adult male. After maturation the pen should be 5F/1M.
- With 5 adult females and 2 male pups, expect `NoSurplusAdults` and no slaughter until a male matures. It must not slaughter a female to make room.

These are gameplay checks; automated core tests do not confirm native game behavior.

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
SPGT event=targets_changed pen=... kind=smolkr keep_f=5 limit=6
SPGT event=slaughter_selected pen=... kind=smolkr reason=MaleChosen victim_female=False ...
SPGT event=slaughter_confirmed pen=... kind=smolkr female=False
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

## 4. Kyckling pen (1.1.0)

Kyckling pens share every line of code with smolkr pens (see DESIGN.md,
"Kyckling pens"), so this is steps 1 to 3 again in a kyckling pen. Every log
line that names a pen now carries `kind=`; for this pen it must read
`kind=kyckling`.

1. Open a kyckling pen's slaughter task. The **Females to keep** row should be
   under its slaughter limit and behave as in step 1.
2. Set up an over-limit pen as in step 2 and let a villager work it. Expected:

   ```text
   SPGT event=targets_changed pen=... kind=kyckling keep_f=5 limit=6
   SPGT event=slaughter_selected pen=... kind=kyckling reason=MaleChosen victim_female=False ...
   SPGT event=slaughter_confirmed pen=... kind=kyckling female=False
   ```

3. Save, reload, and check `targets_loaded ... kind=kyckling`.

If the row appears but a kyckling pen that is over its limit only ever logs
`slaughter_declined` with `adults_f=0 adults_m=0`, kycklings do not carry
`DomesticSmolkr` after all. Report that: the mod would need a second animal
reader for them.

## Undo

Set `Enabled = false` under `[General]` in
`BepInEx\config\aska.smolkr.pen.gender.targets.cfg` to return every pen to
vanilla without uninstalling. Removing the mod entirely is also safe: the one
extra key it saves is ignored by the game.
