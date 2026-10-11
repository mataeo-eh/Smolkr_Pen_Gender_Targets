# Smolkr and Kyckling Pen Gender Limits

An ASKA BepInEx mod that lets a smolkr pen or kyckling pen keep a chosen ratio
of females to males, instead of the 50/50 split vanilla drifts toward.

Formerly *Smolkr Pen Gender Targets*. The folder, project names, plugin GUID,
Thunderstore package id and save key keep the old name on purpose: 1.0.0 is
published under them, and changing them would break updates, config files and
saved settings.

Kyckling pens need no code of their own. In the game they are the same
`AnimalPen` class as smolkr pens (only a `kycklingPen` flag differs) and slaughter
through the same method, so every patch here serves both. See
[docs/DESIGN.md](docs/DESIGN.md#kyckling-pens).

## The problem

A smolkr or kyckling pen has one setting, the **slaughter limit**. When the pen holds more
animals than that (pups included), the game slaughters the last adult of
whichever gender has more adults, with a tie going to males. Every pen is pulled
toward 50/50, and there's no way to say "keep 5 females and 1 male".

That description comes from disassembling the game, not from observation. See
[docs/DESIGN.md](docs/DESIGN.md).

## What this mod changes

A **Females to keep** row is added directly under the slaughter limit in the
pen's slaughter task. It uses the same -/+ control as the game.

- **Off** (step below 0) means the pen behaves exactly like vanilla.
- **0 up to the limit** sets the females to keep (young included). **Males to keep** is the
  limit minus that, and the row's title shows it.

Slaughter still requires a pen over its limit and respects its minimum population. Only adults can be slaughtered, but young animals now count toward their gender's target as well as the population limit. The adult victim comes from the gender furthest above its target. If no surplus gender has an adult available, slaughter waits.

With a limit of 6 and 5 females to keep, 5 adult females and 1 adult male plus a female pup selects a female. A male pup instead selects the male, allowing the pup to replace him. Pens at their limit are left alone even if their ratio is wrong.

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

Version 1.1.1 fixes newborn gender counting. Unit tests and static Harmony verification cover the selection rule and patch bindings; gameplay verification is separate. In multiplayer, villager AI and saved targets are the host's.
