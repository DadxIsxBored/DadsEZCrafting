using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace DadsEZCrafting
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    public sealed class DadsEZCraftingPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dadisbored.dadsezcrafting";
        public const string PluginName = "DadsEZCrafting";
        public const string PluginVersion = "0.1.0";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> BlackForestHints;
        internal static ConfigEntry<string> SwampHints;
        internal static ConfigEntry<string> MountainHints;
        internal static ConfigEntry<string> PlainsHints;
        internal static ConfigEntry<string> MistlandsHints;
        internal static ConfigEntry<string> AshlandsHints;
        internal static ConfigEntry<string> DeepNorthHints;
        internal static ManualLogSource ModLog;

        private Harmony _harmony;

        private void Awake()
        {
            ModLog = Logger;
            Enabled = Config.Bind("1 - General", "Enabled", true, "Enable native crafting-menu search, filtering, and sorting.");
            BlackForestHints = BindTier("Black Forest", "Copper,CopperOre,Tin,TinOre,Bronze,BronzeNails,CoreWood,TrollHide,SurtlingCore,AncientSeed");
            SwampHints = BindTier("Swamps", "Iron,IronScrap,IronOre,IronNails,AncientBark,Guck,Root,Bloodbag,Entrails,WitheredBone,Chain,Ooze,Turnip");
            MountainHints = BindTier("Mountain", "Silver,SilverOre,WolfPelt,WolfFang,WolfClaw,WolfHairBundle,FreezeGland,Crystal,Obsidian,DragonTear,Onion,JuteRed,Fenring");
            PlainsHints = BindTier("Plains", "BlackMetal,BlackMetalScrap,LinenThread,Flax,Barley,BarleyFlour,Tar,LoxPelt,LoxMeat,Needle,Cloudberry,GoblinTotem,VileRibcage");
            MistlandsHints = BindTier("Mistlands", "Blackmarble,YggdrasilWood,Sap,Softtissue,Eitr,Carapace,Mandible,ScaleHide,Bilebag,RoyalJelly,Magecap,JotunPuffs,blackcore,GiantBloodSack,JuteBlue,MechanicalSpring,DvergrExtractor,Wisp,SealbreakerFragment");
            AshlandsHints = BindTier("Ashlands", "FlametalNew,FlametalOreNew,Grausten,Ashwood,GemstoneRed,GemstoneBlue,GemstoneGreen,AskHide,AskBladder,AskSinew,BonemawTooth,CelestialFeather,CeramicPlate,CharcoalResin,CharredBone,CharredCogwheel,CharredSkull,Fiddlehead,MoltenCore,MorgenHeart,MorgenSinew,PotShard,ProustitePowder,ScrapBronze,ShieldCore,SmokePuff,Sulfur,Vineberry,BellFragment");
            DeepNorthHints = BindTier("Deep North", "DeepNorth,Frostcore,LuminousLarva,Mould,Timberwood,Bloodgold,PetrifiedTissue,SealPelt,SealBlubber,MooseMeat,MooseHide,MooseSinew,ElakingHair,LongClaw,FrozenBranch,DeadPulp,Lingonberry,Kale,Snowball,FrostFoundry,NordShield,NordGreatshield,IntricateKey,EmberCharge");

            RecipeFilterState.RebuildTierHints();
            foreach (ConfigEntry<string> entry in new[] { BlackForestHints, SwampHints, MountainHints, PlainsHints, MistlandsHints, AshlandsHints, DeepNorthHints })
            {
                entry.SettingChanged += OnTierHintsChanged;
            }

            _harmony = new Harmony(PluginGuid);
            CraftingPatches.Install(_harmony);
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded for Valheim 1.0.12 and BepInEx 5.4.23.5.");
        }

        private ConfigEntry<string> BindTier(string name, string defaults)
        {
            return Config.Bind("2 - Tier Detection", name + " Prefab Hints", defaults,
                "Comma-separated prefab or recipe-name fragments used to classify this progression tier.");
        }

        private static void OnTierHintsChanged(object sender, EventArgs args)
        {
            RecipeFilterState.RebuildTierHints();
            CraftingUiManager.RefreshRecipes();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
