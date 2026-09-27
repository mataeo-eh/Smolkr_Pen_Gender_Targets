# Smolkr Pen Gender Targets

An ASKA BepInEx mod that lets a smolkr pen keep a chosen number of females and
males, instead of slaughtering from whichever gender happens to have more adults.

## The problem

A smolkr pen has one setting, the **slaughter limit**. When the pen goes over it,
the game slaughters the last adult of whichever gender has more adults (a tie
goes to males). There is no way to say "keep 5 females and 1 male", and the
count can drift until a pen holds only one gender and stops breeding.

That description comes from disassembling the game, not from observation. See
[docs/DESIGN.md](docs/DESIGN.md).

## What this mod adds

A small window beside the pen's slaughter task, with a per-pen:

- **Use gender targets for this pen** toggle. Off means vanilla, exactly.
- **Females to keep**, editable.
- **Males to keep**, always *slaughter limit minus females to keep*, so it can
  never disagree with the game's own limit.

The window also shows the pen's head count and what will be slaughtered next.

With targets on, a villager slaughters an adult only from a gender that is over
its target. With a limit of 6 and 5 females to keep:

| Adults in pen | Vanilla | This mod |
| --- | --- | --- |
| 6 F, 0 M | nothing (6 is not over 6) | a female |
| 4 F, 3 M | a female (the majority) | a male |
| 5 F, 1 M, plus a pup | an adult of the majority | nothing until the pup grows up |

Pups are never slaughtered (vanilla doesn't either) and by default don't count
toward targets. They're judged once grown. `CountPupsTowardTargets` changes
that.

Targets are stored in the world save, with the pen. Removing the mod is safe:
the pen returns to vanilla behaviour.

## Configuration

`BepInEx\config\aska.smolkr.pen.gender.targets.cfg`

| Key | Default | Purpose |
| --- | --- | --- |
| `General.Enabled` | `true` | Master switch. False = every pen vanilla; targets kept. |
| `Behaviour.CountPupsTowardTargets` | `false` | Count pups toward their gender's target. |
| `Interface.ShowPanel` | `true` | Show the window beside the slaughter task. |

## Build, test, deploy

From the `Aska_Mods` harness root:

```powershell
.\scripts\verify.ps1 -Mod Smolkr_Pen_Gender_Targets    # build + static Harmony check
.\scripts\deploy.ps1 -Mod Smolkr_Pen_Gender_Targets    # copy into the JustMe profile
.\scripts\watch-log.ps1 -Mod Smolkr_Pen_Gender_Targets # this mod's log lines
```

Unit tests for the slaughter rule, no game needed:

```powershell
dotnet test tests\SmolkrPenGenderTargets.Core.Tests
```

## Layout

| Path | Role |
| --- | --- |
| `src/SmolkrPenGenderTargets.Core` | The slaughter rule. Plain C#, no game references. |
| `src/SmolkrPenGenderTargets/Patches` | Harmony patches: selection, save/load, panel tracking, diagnostics. |
| `src/SmolkrPenGenderTargets/Runtime` | Reading pens, per-pen settings, logging. |
| `src/SmolkrPenGenderTargets/UI` | The IMGUI window and the tracker that decides when it shows. |
| `tests/` | Unit tests for Core. |
| `docs/DESIGN.md` | How vanilla chooses, and why the patch sits where it does. |
| `docs/IN_GAME_TEST.md` | The in-game checks that confirm a build works. |
| `packaging/` | Thunderstore manifest, README, changelog and icon. |

## Status

`0.1.0` is built, unit-tested, and statically verified, but **not yet run in
game**. Follow [docs/IN_GAME_TEST.md](docs/IN_GAME_TEST.md) before relying on it.
Host only in multiplayer: villager AI and the saved targets are the host's.
