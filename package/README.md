# Challenge Runs

Choose challenge modifiers in the Breach, then start a run. You can stack as many as you like.

## How to pick challenges
- Press **F6** in the Breach to open the picker.
- When you die or win, the picker opens by itself on the game over screen. Change challenges there, then **Quick Restart** or head back to the Breach and the next run uses them.
- Or use the console (F2): `challenge list`, `challenge on glass noheal`, `challenge off glass`, `challenge clear`.

Your picks are saved between sessions. They lock in when a run starts and can only be changed in the Breach or on the game over screen.

## Challenges
| id | Challenge | Effect |
|---|---|---|
| `glass` | Glass Cannon | 1 heart, no armor (the Robot gets 1 armor), 2× damage |
| `noheal` | No Healing | Healing does nothing |
| `starter` | Starter Gun Only | You can't pick up new guns |
| `roulette` | Gun Roulette | Random gun after every room clear, no switching guns |
| `sponge` | Bullet Sponges | Enemies and bosses have 2× HP |
| `jammed` | Fully Jammed | Every enemy is Jammed |
| `blankless` | Blankless | You can't hold blanks |
| `marathon` | Marathon | Each floor is one long trail of 100 rooms with chests, shops and NPCs along the way, then every boss the floor can roll, back to back |
| `arena` | Arena | Each floor is one big room with 50 waves of enemies, then chest rooms, a shop and every boss the floor can roll |

Starter Gun Only / Gun Roulette and Marathon / Arena are mutually exclusive pairs.

## Settings
Settings are in `BepInEx/config/oakesnoah.etg.challengeruns.cfg`; r2modman's Config editor can change them.
- `[Marathon]`: rooms per floor, min/max shops, and whether the game can add its usual side rooms.
- `[Arena]`: number of waves and number of chest rooms.

If Marathon floors fail to load, lower `CombatRoomsPerFloor`.
