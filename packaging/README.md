# Smolkr Pen Gender Targets

Choose how many female and male smolkr each pen keeps.

In vanilla, a smolkr pen has a single slaughter limit. When the pen goes over
it, villagers slaughter an adult from whichever gender has more adults, so you
cannot ask for "5 females and 1 male", and a pen can drift to a single gender
and stop breeding.

This mod adds a small window beside the pen's slaughter task:

- **Use gender targets for this pen**: off means vanilla, exactly.
- **Females to keep**: set it per pen.
- **Males to keep**: always the slaughter limit minus females to keep.

It also shows the pen's head count and what will be slaughtered next.

With targets on, villagers only slaughter adults from a gender that is over its
target. Pups are never slaughtered and, by default, are judged once they grow
up.

## Configuration

`BepInEx/config/aska.smolkr.pen.gender.targets.cfg`

- `Enabled`: master switch; false returns every pen to vanilla.
- `CountPupsTowardTargets`: count pups toward their gender's target (default off).
- `ShowPanel`: show the window beside the slaughter task.

## Notes

- Targets are saved with each pen. Removing the mod is safe; pens return to vanilla.
- Multiplayer: install on the host. Villager AI and saved targets are the host's.
