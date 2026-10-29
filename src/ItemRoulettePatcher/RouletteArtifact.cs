using R2API;
using RoR2;
using UnityEngine;

namespace ItemRoulettePatcher
{
    /// <summary>
    /// The "Artifact of the Roulette". Enabling it on the lobby screen turns ItemRoulette's mechanics on for that run;
    /// leaving it off makes the run play as if ItemRoulette were not installed (see <see cref="RouletteGate"/>).
    /// </summary>
    internal static class RouletteArtifact
    {
        private const string NameToken = "ITEMROULETTEPATCHER_ARTIFACT_NAME";
        private const string DescriptionToken = "ITEMROULETTEPATCHER_ARTIFACT_DESC";

        public static ArtifactDef Def { get; private set; }

        public static void Register()
        {
            LanguageAPI.Add(NameToken, "Artifact of the Roulette");
            LanguageAPI.Add(DescriptionToken,
                "Enables the ItemRoulette mod for this run: the on-screen item reel, replaced chests and drops, the casino roulette, " +
                "Life Bet and chaos events.\n<style=cStack>Leave it disabled to play the run without ItemRoulette's mechanics.</style>");
            LanguageAPI.Add(NameToken, "Артефакт Рулетки", "ru");
            LanguageAPI.Add(DescriptionToken,
                "Включает мод ItemRoulette в этом забеге: рулетку предметов на экране, замену сундуков и наград, казино-рулетку, " +
                "ставку жизни и события хаоса.\n<style=cStack>Оставьте выключенным, чтобы играть без механик ItemRoulette.</style>",
                "ru");

            var def = ScriptableObject.CreateInstance<ArtifactDef>();
#pragma warning disable CS0618 // The lobby still reads the direct Sprite fields when no addressable reference is set.
            def.cachedName = "ItemRoulettePatcher_Roulette";
            def.nameToken = NameToken;
            def.descriptionToken = DescriptionToken;
            def.smallIconSelectedSprite = ArtifactIcon.Create(selected: true);
            def.smallIconDeselectedSprite = ArtifactIcon.Create(selected: false);
#pragma warning restore CS0618

            if (!ContentAddition.AddArtifactDef(def))
            {
                Plugin.Log.LogError("R2API refused the roulette artifact; ItemRoulette will not be gated.");
                return;
            }

            Def = def;
        }
    }
}
