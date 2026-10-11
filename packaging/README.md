# Smolkr and Kyckling Pen Gender Limits

Choose the ratio of females to males each smolkr pen and kyckling pen keeps.

In vanilla, when a pen goes over its slaughter limit, villagers slaughter an
adult from whichever gender has more adults, so every pen drifts toward 50/50.
There is no way to ask for, say, 5 females and 1 male.

This mod adds a **Females to keep** row under the slaughter limit in the pen's
slaughter task. It works the same way in smolkr pens and kyckling pens:

- **Off**: the pen behaves exactly like vanilla.
- **0 up to the limit**: females to keep (young included). Males to keep is the limit minus
  that, shown in the row's title.

Villagers slaughter only when the pen is over its limit and never slaughter young animals. Young animals count toward both the population limit and their gender target. An adult is taken from the gender furthest above its target. If that gender has no adults, slaughter waits for the young to mature. A male pup can replace the only adult male.

## Configuration

`BepInEx/config/aska.smolkr.pen.gender.targets.cfg`

- `Enabled`: master switch; false returns every pen to vanilla.
- `ShowRow`: show the Females to keep row.
- `RowOffsetX` / `RowOffsetY`: nudge the row if it overlaps something.

## Notes

- Formerly *Smolkr Pen Gender Targets*. Updating keeps your config and every
  pen's saved setting.
- Settings are saved with each pen. Removing the mod is safe; pens return to vanilla.
- Multiplayer: install on the host. Villager AI and saved settings are the host's.
