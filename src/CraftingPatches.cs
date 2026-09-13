using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace DadsEZCrafting
{
    internal static class CraftingPatches
    {
        internal static void Install(Harmony harmony)
        {
            Patch(harmony, AccessTools.Method(typeof(InventoryGui), "Awake"), nameof(InventoryGuiAwakePostfix), false);
            Patch(harmony, AccessTools.Method(typeof(InventoryGui), "UpdateRecipeList", new[] { typeof(List<Recipe>) }), nameof(UpdateRecipeListPrefix), true);
            Patch(harmony, AccessTools.Method(typeof(InventoryGui), "OnCraftPressed"), nameof(OnCraftPressedPrefix), true);
        }

        private static void Patch(Harmony harmony, MethodBase original, string patchName, bool prefix)
        {
            if (original == null)
            {
                DadsEZCraftingPlugin.ModLog.LogError($"Required Valheim method was not found for {patchName}; no invalid Harmony patch was applied.");
                return;
            }
            HarmonyMethod patch = new HarmonyMethod(typeof(CraftingPatches), patchName);
            harmony.Patch(original, prefix ? patch : null, prefix ? null : patch);
        }

        private static void InventoryGuiAwakePostfix(InventoryGui __instance)
        {
            CraftingUiManager.Attach(__instance);
        }

        private static void UpdateRecipeListPrefix(List<Recipe> recipes)
        {
            if (DadsEZCraftingPlugin.Enabled?.Value == true)
            {
                RecipeFilterState.Apply(recipes);
            }
        }

        private static void OnCraftPressedPrefix(InventoryGui __instance)
        {
            CraftingUiManager.PrepareCrafting(__instance);
        }
    }
}
