# Smolkr Pen Gender Targets

Choose the ratio of female to male smolkr each pen keeps.

In vanilla, when a pen goes over its slaughter limit, villagers slaughter an
adult from whichever gender has more adults, so every pen drifts toward 50/50.
There is no way to ask for, say, 5 females and 1 male.

This mod adds a **Females to keep** row under the slaughter limit in the pen's
slaughter task:

- **Off**: the pen behaves exactly like vanilla.
- **0 up to the limit**: adult females to keep. Males to keep is the limit minus
  that, shown in the row's title.

Only the gender choice changes. Villagers still slaughter only when the pen is
over its limit (pups count, as in vanilla), never below the pen's minimum
population, and never a pup. They take from the gender furthest above its
target instead of the one with more adults.

## Configuration

`BepInEx/config/aska.smolkr.pen.gender.targets.cfg`

- `Enabled`: master switch; false returns every pen to vanilla.
- `ShowRow`: show the Females to keep row.
- `RowOffsetX` / `RowOffsetY`: nudge the row if it overlaps something.

## Notes

- Targets are saved with each pen. Removing the mod is safe; pens return to vanilla.
- Multiplayer: install on the host. Villager AI and saved targets are the host's.
