# DadsEZCrafting

DadsEZCrafting adds compact native-styled controls to Valheim 1.0.12 crafting-station recipe lists without replacing the crafting panel.

The default view remains `All`, `All Tiers`, and `Native` ordering. Recipes can be filtered by item type and progression tier. Food can additionally be ordered by Health, Stamina, or Eitr value.

## Item filters

- All
- Weapons
- Armor
- Shields
- Ammo / Throwables
- Food
- Tools
- Materials
- Other

## Tier filters

- All Tiers
- Meadows
- Black Forest
- Swamps
- Mountain
- Plains
- Mistlands
- Ashlands
- Deep North

Tier detection uses recipe output, required-material, and crafting-station prefab names. The prefab hints for every tier after Meadows are configurable for modded recipes and future content.

The default material and progression groups are aligned with the Valheim Fandom Wiki biome, materials, food, crafting, and item-ID tables. Runtime recipe values remain authoritative for Health, Stamina, and Eitr ordering.
