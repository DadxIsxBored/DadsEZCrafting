using System;
using System.Collections.Generic;
using System.Linq;

namespace DadsEZCrafting
{
    internal enum RecipeCategory
    {
        All,
        Weapons,
        Armor,
        Shields,
        Ammunition,
        Food,
        Tools,
        Materials,
        Other
    }

    internal enum ProgressionTier
    {
        All,
        Meadows,
        BlackForest,
        Swamps,
        Mountain,
        Plains,
        Mistlands,
        Ashlands,
        DeepNorth
    }

    internal enum RecipeSort
    {
        Native,
        NameAscending,
        NameDescending,
        Health,
        Stamina,
        Eitr
    }

    internal static class RecipeFilterState
    {
        internal static readonly RecipeCategory[] Categories = (RecipeCategory[])Enum.GetValues(typeof(RecipeCategory));
        internal static readonly ProgressionTier[] Tiers = (ProgressionTier[])Enum.GetValues(typeof(ProgressionTier));

        internal static RecipeCategory Category { get; private set; } = RecipeCategory.All;
        internal static ProgressionTier Tier { get; private set; } = ProgressionTier.All;
        internal static RecipeSort Sort { get; private set; } = RecipeSort.Native;
        internal static string Search { get; set; } = string.Empty;

        private static readonly Dictionary<ProgressionTier, string[]> TierHints = new Dictionary<ProgressionTier, string[]>();

        internal static void RebuildTierHints()
        {
            TierHints.Clear();
            TierHints[ProgressionTier.BlackForest] = Split(DadsEZCraftingPlugin.BlackForestHints?.Value);
            TierHints[ProgressionTier.Swamps] = Split(DadsEZCraftingPlugin.SwampHints?.Value);
            TierHints[ProgressionTier.Mountain] = Split(DadsEZCraftingPlugin.MountainHints?.Value);
            TierHints[ProgressionTier.Plains] = Split(DadsEZCraftingPlugin.PlainsHints?.Value);
            TierHints[ProgressionTier.Mistlands] = Split(DadsEZCraftingPlugin.MistlandsHints?.Value);
            TierHints[ProgressionTier.Ashlands] = Split(DadsEZCraftingPlugin.AshlandsHints?.Value);
            TierHints[ProgressionTier.DeepNorth] = Split(DadsEZCraftingPlugin.DeepNorthHints?.Value);
        }

        internal static void NextCategory()
        {
            Category = Categories[((int)Category + 1) % Categories.Length];
            if (Category != RecipeCategory.Food && Sort >= RecipeSort.Health) Sort = RecipeSort.Native;
        }

        internal static void NextTier()
        {
            Tier = Tiers[((int)Tier + 1) % Tiers.Length];
        }

        internal static void NextSort()
        {
            RecipeSort[] options = Category == RecipeCategory.Food
                ? (RecipeSort[])Enum.GetValues(typeof(RecipeSort))
                : new[] { RecipeSort.Native, RecipeSort.NameAscending, RecipeSort.NameDescending };
            int current = Array.IndexOf(options, Sort);
            Sort = options[(current + 1 + options.Length) % options.Length];
        }

        internal static void Apply(List<Recipe> recipes)
        {
            if (recipes == null) return;
            recipes.RemoveAll(recipe => !Matches(recipe));
            Comparison<Recipe> comparison = GetComparison();
            if (comparison != null) recipes.Sort(comparison);
        }

        internal static bool Matches(Recipe recipe)
        {
            if (recipe?.m_item?.m_itemData?.m_shared == null) return false;
            if (Category != RecipeCategory.All && ClassifyCategory(recipe) != Category) return false;
            if (Tier != ProgressionTier.All && ClassifyTier(recipe) != Tier) return false;

            string query = (Search ?? string.Empty).Trim();
            if (query.Length == 0) return true;
            string haystack = BuildSearchText(recipe);
            return query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .All(term => haystack.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static RecipeCategory ClassifyCategory(Recipe recipe)
        {
            ItemDrop.ItemData.SharedData shared = recipe?.m_item?.m_itemData?.m_shared;
            if (shared == null) return RecipeCategory.Other;
            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable &&
                (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f || shared.m_foodRegen > 0f))
                return RecipeCategory.Food;
            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Shield) return RecipeCategory.Shields;
            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Helmet ||
                shared.m_itemType == ItemDrop.ItemData.ItemType.Chest ||
                shared.m_itemType == ItemDrop.ItemData.ItemType.Legs ||
                shared.m_itemType == ItemDrop.ItemData.ItemType.Shoulder) return RecipeCategory.Armor;
            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo ||
                shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable) return RecipeCategory.Ammunition;
            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Tool) return RecipeCategory.Tools;
            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Material) return RecipeCategory.Materials;
            if (recipe.m_item.m_itemData.IsWeapon()) return RecipeCategory.Weapons;
            return RecipeCategory.Other;
        }

        internal static ProgressionTier ClassifyTier(Recipe recipe)
        {
            ProgressionTier result = StationTier(recipe?.m_craftingStation);
            Promote(ref result, Identifier(recipe?.name));
            Promote(ref result, Identifier(recipe?.m_item?.gameObject?.name));
            if (recipe?.m_resources != null)
            {
                foreach (Piece.Requirement requirement in recipe.m_resources)
                {
                    Promote(ref result, Identifier(requirement?.m_resItem?.gameObject?.name));
                }
            }
            return result == ProgressionTier.All ? ProgressionTier.Meadows : result;
        }

        internal static string CategoryLabel()
        {
            switch (Category)
            {
                case RecipeCategory.Ammunition: return "Ammo / Throwables";
                default: return Category.ToString();
            }
        }

        internal static string TierLabel()
        {
            switch (Tier)
            {
                case ProgressionTier.All: return "All Tiers";
                case ProgressionTier.BlackForest: return "Black Forest";
                case ProgressionTier.DeepNorth: return "Deep North";
                default: return Tier.ToString();
            }
        }

        internal static string SortLabel()
        {
            switch (Sort)
            {
                case RecipeSort.NameAscending: return "Name A-Z";
                case RecipeSort.NameDescending: return "Name Z-A";
                default: return Sort.ToString();
            }
        }

        private static Comparison<Recipe> GetComparison()
        {
            switch (Sort)
            {
                case RecipeSort.NameAscending:
                    return (left, right) => CompareNames(left, right);
                case RecipeSort.NameDescending:
                    return (left, right) => CompareNames(right, left);
                case RecipeSort.Health:
                    return (left, right) => CompareFoodDescending(left, right, item => item.m_shared.m_food);
                case RecipeSort.Stamina:
                    return (left, right) => CompareFoodDescending(left, right, item => item.m_shared.m_foodStamina);
                case RecipeSort.Eitr:
                    return (left, right) => CompareFoodDescending(left, right, item => item.m_shared.m_foodEitr);
                default:
                    return null;
            }
        }

        private static int CompareFoodDescending(Recipe left, Recipe right, Func<ItemDrop.ItemData, float> selector)
        {
            float leftValue = left?.m_item?.m_itemData == null ? 0f : selector(left.m_item.m_itemData);
            float rightValue = right?.m_item?.m_itemData == null ? 0f : selector(right.m_item.m_itemData);
            int result = rightValue.CompareTo(leftValue);
            return result != 0 ? result : CompareNames(left, right);
        }

        private static int CompareNames(Recipe left, Recipe right)
        {
            return StringComparer.CurrentCultureIgnoreCase.Compare(ItemName(left), ItemName(right));
        }

        private static string ItemName(Recipe recipe)
        {
            string token = recipe?.m_item?.m_itemData?.m_shared?.m_name ?? string.Empty;
            return Localization.instance == null ? token : Localization.instance.Localize(token);
        }

        private static string BuildSearchText(Recipe recipe)
        {
            List<string> parts = new List<string>
            {
                ItemName(recipe),
                recipe?.name ?? string.Empty,
                recipe?.m_item?.gameObject?.name ?? string.Empty,
                Localize(recipe?.m_item?.m_itemData?.m_shared?.m_description)
            };
            if (recipe?.m_resources != null)
            {
                foreach (Piece.Requirement requirement in recipe.m_resources)
                {
                    parts.Add(requirement?.m_resItem?.gameObject?.name ?? string.Empty);
                    parts.Add(Localize(requirement?.m_resItem?.m_itemData?.m_shared?.m_name));
                }
            }
            return string.Join(" ", parts);
        }

        private static string Localize(string text)
        {
            return Localization.instance == null ? text ?? string.Empty : Localization.instance.Localize(text ?? string.Empty);
        }

        private static ProgressionTier StationTier(CraftingStation station)
        {
            string name = Identifier(station?.gameObject?.name);
            if (name.Contains("frostfoundry")) return ProgressionTier.DeepNorth;
            if (name.Contains("blackforge") || name.Contains("galdrtable")) return ProgressionTier.Mistlands;
            if (name.Contains("artisan")) return ProgressionTier.Plains;
            if (name.Contains("stonecutter")) return ProgressionTier.Swamps;
            if (name.Contains("forge")) return ProgressionTier.BlackForest;
            return ProgressionTier.Meadows;
        }

        private static void Promote(ref ProgressionTier current, string identifier)
        {
            if (identifier.Length == 0) return;
            for (ProgressionTier tier = ProgressionTier.DeepNorth; tier >= ProgressionTier.BlackForest; tier--)
            {
                if (TierHints.TryGetValue(tier, out string[] hints) && hints.Any(identifier.Contains))
                {
                    if (tier > current) current = tier;
                    return;
                }
            }
        }

        private static string Identifier(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        private static string[] Split(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Identifier)
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
