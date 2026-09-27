# Smolkr Pen Gender Targets

An ASKA BepInEx mod that lets a smolkr pen keep a chosen ratio of females to
males, instead of the 50/50 split vanilla drifts toward.

## The problem

A smolkr pen has one setting, the **slaughter limit**. When the pen holds more
animals than that (pups included), the game slaughters the last adult of
whichever gender has more adults, with a tie going to males. Every pen is pulled
toward 50/50, and there's no way to say "keep 5 females and 1 male".

That description comes from disassembling the game, not from observation. See
[docs/DESIGN.md](docs/DESIGN.md).

## What this mod changes

A **Females to keep** row is added directly under the slaughter limit in the
pen's slaughter task. It uses the same -/+ control as the game.

- **Off** (step below 0) means the pen behaves exactly like vanilla.
- **0 up to the limit** sets the adult females to keep. **Males to keep** is the
  limit minus that, and the row's title shows it.

That's the only change. **When** a villager slaughters is still vanilla's rule:
only while the pen is over its limit, pups included, and never below the pen's
minimum population. **Who** can be slaughtered is still any adult, never a pup.
The one difference is **which gender**: the one furthest above its target,
instead of the one with more adults.

With a limit of 6 and 5 females to keep:

| Pen (7 animals) | Vanilla takes | This mod takes |
| --- | --- | --- |
| 5 F, 2 M | a female (the majority) | a male |
| 6 F, 0 M, 1 pup | a female | a female |
| 4 F, 2 M, 1 pup | a female | a male |

Targets are stored in the world save, with the pen. Removing the mod is safe:
the pen goes back to vanilla.

## Configuration

`BepInEx\config\aska.smolkr.pen.gender.targets.cfg`

| Key | Default | Purpose |
| --- | --- | --- |
| `General.Enabled` | `true` | Master switch. False = every pen vanilla; targets kept. |
| `Interface.ShowRow` | `true` | Add the Females to keep row to the slaughter task. |
| `Interface.RowOffsetX` / `RowOffsetY` | `0` | Nudge the row if a game update makes it overlap something. |

## Build, test, deploy

From the `Aska_Mods` harness root. `mod.json` targets the **Test_Mods** profile.

```powershell
.\scripts\verify.ps1 -Mod Smolkr_Pen_Gender_Targets    # build + static Harmony check
.\scripts\deploy.ps1 -Mod Smolkr_Pen_Gender_Targets    # copy into Test_Mods
.\scripts\watch-log.ps1 -Mod Smolkr_Pen_Gender_Targets # this mod's log lines
```

Unit tests for the slaughter rule (no game needed), including a parity check
against a transcription of the vanilla method:

```powershell
dotnet test tests\SmolkrPenGenderTargets.Core.Tests
```

To research game code for a future change, use `.\scripts\disasm.ps1`. It
disassembles game methods with names resolved, and DESIGN.md shows how it was
used here.

## Layout

| Path | Role |
| --- | --- |
| `src/SmolkrPenGenderTargets.Core` | The slaughter rule. Plain C#, no game references. |
| `src/SmolkrPenGenderTargets/Patches` | Harmony patches: selection, save/load, task panel, diagnostics. |
| `src/SmolkrPenGenderTargets/Runtime` | Reading pens, per-pen settings, logging. |
| `src/SmolkrPenGenderTargets/UI` | The Females to keep row. |
| `tests/` | Unit tests for Core. |
| `docs/DESIGN.md` | How vanilla chooses, and why each patch sits where it does. |
| `docs/IN_GAME_TEST.md` | The in-game checks that confirm a build works. |
| `packaging/` | Thunderstore manifest, README, changelog and icon. |

## Status

`0.2.0` is built, unit-tested and statically verified, but **not yet run in
game**. Follow [docs/IN_GAME_TEST.md](docs/IN_GAME_TEST.md) before relying on
it. In multiplayer it's host only: villager AI and the saved targets are the
host's.
