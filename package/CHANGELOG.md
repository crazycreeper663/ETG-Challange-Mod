# Changelog

## 0.3.2
- Removed the Fully Jammed challenge.

## 0.3.1
- Fixed: Marathon (and Arena) floors fell back to the normal layout. Reading the floor's boss list crashed because the game blocks direct access to its private boss data; it's now read by reflection.
- If building part of a Marathon/Arena floor fails (the boss list or the trail NPCs), the floor is still built without that part instead of reverting to the normal layout.
- Gun Roulette and the Starter Gun Only / Arena pop-ups no longer use game methods that might be blocked the same way.

## 0.3.0
- The challenge picker now opens on the game over and victory screens. Changes there apply to your Quick Restart or your next run from the Breach.
- Fixed: Marathon and Arena only had one boss per floor. They now chain every boss the floor can roll, taken from the game's own boss list.
- Fixed: Marathon floors could still get side rooms from the NPCs the game assigns to each floor (Chancelot, Winchester, etc.) and from mods like BetterShop. Those NPCs now sit on the trail if their room has two doors; otherwise they're skipped. Turn on `AllowInjectedSideRooms` to get them back as side rooms.
- Fixed the "targets a wrong version of BepInEx" warning on load.
- If one of the mod's game patches fails to apply (e.g. due to another mod), only that feature is disabled instead of the rest of the mod.

## 0.2.0
- Added **Marathon**: each floor is one long trail of 100 combat rooms, with chests, 2–5 shops and NPC rooms along the way, ending in every boss the floor can roll.
- Added **Arena**: each floor is one big room with 50 enemy waves, followed by chest rooms, a shop and every boss the floor can roll.

## 0.1.0
- First version: Glass Cannon, No Healing, Starter Gun Only, Gun Roulette, Bullet Sponges, Fully Jammed, Blankless.
- F6 picker in the Breach, `challenge` console commands, and config file support.
