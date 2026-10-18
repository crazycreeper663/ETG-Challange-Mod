# Challenge Runs (Enter the Gungeon mod)

Choose challenge modifiers in the Breach, then start a run. You can stack them.

| id | Challenge | Effect |
|---|---|---|
| `glass` | Glass Cannon | 1 heart, no armor (the Robot gets 1 armor), 2x damage |
| `noheal` | No Healing | Healing does nothing |
| `starter` | Starter Gun Only | You can't pick up new guns |
| `roulette` | Gun Roulette | Random gun after every room clear, no switching guns |
| `sponge` | Bullet Sponges | Enemies and bosses have 2x HP |
| `blankless` | Blankless | You can't hold blanks |
| `marathon` | Marathon | Each floor is a single trail of 100 combat rooms. Chests, 2–5 shops and NPC rooms sit along it, and it ends with every boss the floor can roll, one after another |
| `arena` | Arena | Each floor is one big room with 50 waves of enemies; each wave is one of that floor's combat rooms' worth of enemies. Clearing it opens the chest rooms, a shop and the full boss gauntlet |

Marathon and Arena can't be on at the same time. Either one can be combined with the other challenges.

### Floor modes in detail
- **Marathon** is strictly linear: each room leads to exactly one new room, with no forks. Shops and NPC rooms sit on the path, so only versions of those rooms with 2+ doors are used. NPC rooms that only come in a one-door version are skipped. If a floor has no shop with two doors, that shop becomes a short dead end, and a warning is logged.
- The game normally adds extras as side rooms: some NPCs, secret floor entrances, the rat. Marathon turns these off by default to keep the trail linear. Set `AllowInjectedSideRooms = true` to get them back as small side branches.
- **Arena** seals the room when you walk in and runs the waves. Ammo and a half heart drop every 10 waves.
- **Bosses:** on floors with several possible bosses (Keep, Gungeon Proper, Black Powder Mine, Hollow), you fight all of them in random order, and each gives its normal reward. The Forge and Bullet Hell only have one boss each, so those floors are unchanged.
- **Skipped floors:** the Breach, the tutorial, the Resourceful Rat's lair and character Pasts are never changed.

Settings live in the config file under `[Marathon]` (rooms per floor, min/max shops, side rooms) and `[Arena]` (waves, chest rooms).

## Choosing challenges
- **In game:** press **F6** in the Breach to open the picker. It also opens by itself on the game over and victory screens, so you can change challenges before you Quick Restart or go back to the Breach.
- **Console:** `challenge list`, `challenge on glass noheal`, `challenge off glass`, `challenge clear`.
- **Config file:** `BepInEx/config/oakesnoah.etg.challengeruns.cfg`

Your choices are saved between sessions. They lock in when a run starts and can only be changed in the Breach or on the game over/victory screen.

## Installing with r2modman
1. In r2modman, select Enter the Gungeon and your profile.
2. Install **Mod the Gungeon API** from the online list if you don't already have it. It brings in the BepInEx pack.
3. Go to **Settings → Profile → Import local mod**, choose `dist/oakesnoah-ChallengeRuns-0.3.3.zip`, and confirm.
4. Launch with **Start modded**. In the Breach, press F6, or open the console with F2 and type `challenge help`.

## Building
Install the .NET SDK (6 or newer; the project targets net35 using reference packages), then:

```
dotnet build -c Release
```

This compiles the DLL and writes the r2modman-ready zip to `dist/<team>-ChallengeRuns-<version>.zip`.
The zip contains `ChallengeRuns.dll`, `manifest.json`, `icon.png`, `README.md` and `CHANGELOG.md`.
Package-only files (manifest template, icon, player-facing README, changelog) live in `package/`.

The build needs the game, BepInEx and MtGAPI DLLs to compile against. By default it looks for:
- the game at `C:/Program Files (x86)/Steam/steamapps/common/Enter the Gungeon`
- BepInEx and MtGAPI in r2modman's `Default` profile (`%APPDATA%/r2modmanPlus-local/ETG/profiles/Default/BepInEx`)

Override either path on the command line if yours differ:
`dotnet build -c Release -p:GameDir="D:/Games/Enter the Gungeon" -p:BepInExDir="D:/r2/ETG/profiles/Mine/BepInEx"`

To release a new version, bump `<Version>` in `ChallengeRuns.csproj` and `Plugin.VERSION` together, and add a
`package/CHANGELOG.md` entry.

## Adding a challenge
Add an entry to `ChallengeManager.All` with an `Id`, `Name`, and `Description`. Use `OnRunStart` for one-time setup,
or check `ChallengeManager.IsActive("yourid")` from a Harmony patch in `Patches.cs`.
