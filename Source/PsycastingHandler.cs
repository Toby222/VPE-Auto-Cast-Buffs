﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RimWorld.Planet;
using Verse;
using Ability = VFECore.Abilities.Ability;

namespace BetterAutocastVPE;

using static Helpers.EnchantHelper;
using static Helpers.MendHelper;
using static Helpers.PawnHelper;
using static Helpers.ThingHelper;
using static Helpers.WeatherHelper;

internal static class PsycastingHandler
{
    #region private members
    private static readonly ReadOnlyDictionary<string, Func<Pawn, Ability, bool>> abilityHandlers =
        new(
            // TODO: Probably sort these more sensibly than alphabetically
            // Or allow configuring priorities
            new Dictionary<string, Func<Pawn, Ability, bool>>
            {
                { "VPE_AdrenalineRush", HandleSelfBuff },
                { "VPE_BladeFocus", HandleSelfBuff },
                { "VPE_ControlledFrenzy", HandleSelfBuff },
                { "VPE_Darkvision", HandleDarkvision },
                { "VPE_Eclipse", HandleEclipse },
                { "VPE_EnchantQuality", HandleEnchant },
                { "VPE_FiringFocus", HandleSelfBuff },
                { "VPE_GuidedShot", HandleSelfBuff },
                { "VPE_Invisibility", HandleInvisibility },
                { "VPE_Mend", HandleMend },
                { "VPE_PsychicGuidance", HandlePsychicGuidance },
                { "VPE_SpeedBoost", HandleSelfBuff },
                { "VPE_StealVitality", HandleStealVitality },
                { "VPE_WordofImmunity", HandleWordOfImmunity },
                { "VPE_WordofJoy", HandleWordOfJoy },
                { "VPE_WordofProductivity", HandleWordOfProductivity },
                { "VPE_WordofSerenity", HandleWordOfSerenity },
                { "VPEP_BrainLeech", HandleBrainLeech },
            }
        );
    #endregion private members

    #region helper functions
    internal static bool HasHandler(string abilityDefName)
    {
        return abilityHandlers.ContainsKey(abilityDefName);
    }

    internal static bool GetsCastWhileDrafted(string abilityDefName)
    {
        return BetterAutocastVPE.Settings.DraftedAutocastDefs.Contains(abilityDefName);
    }

    internal static bool GetsCastWhileUndrafted(string abilityDefName)
    {
        return BetterAutocastVPE.Settings.UndraftedAutocastDefs.Contains(abilityDefName);
    }

    /// <summary>
    /// Tries to create a job to cast the given ability on the given target
    /// </summary>
    /// <returns>If a job was successfully created</returns>
    private static bool CastAbilityOnTarget(Ability ability, Thing target)
    {
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));
        if (target is null)
            throw new ArgumentNullException(nameof(target));

        ability.CreateCastJob(new GlobalTargetInfo(target));
        return true;
    }
    #endregion helper functions

    #region worker functions
    /// <summary>
    /// Tries to auto-cast the ability
    /// </summary>
    /// <returns>If the ability was successfullly autocast</returns>
    internal static bool HandleAbility(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        if (
            (!__instance.Drafted && GetsCastWhileUndrafted(ability.def.defName))
            || (__instance.Drafted && GetsCastWhileDrafted(ability.def.defName))
        )
        {
            return abilityHandlers[ability.def.defName](__instance, ability);
        }

        return false;
    }
    #endregion worker functions

    #region handlers
    #region generic
    private static bool HandleSelfBuff(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        // note, this method only works right if the buff hediff defName and the ability hediff defName are the same
        if (__instance.HasHediff(ability.def.defName))
            return false;
        else
            return CastAbilityOnTarget(ability, __instance);
    }
    #endregion generic

    #region Protector
    private static bool HandleInvisibility(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        Pawn? target = null;

        if (
            BetterAutocastVPE.Settings.InvisibilityTargetSelf
            && !__instance.HasHediff("PsychicInvisibility")
        )
        {
            target ??= __instance;
        }

        if (BetterAutocastVPE.Settings.InvisibilityTargetColonists && target is null)
        {
            float range = ability.GetRangeForPawn();
            IEnumerable<Pawn> pawnsInRange = GetPawnsInRange(__instance, range);
            IEnumerable<Pawn> pawnsWithoutHediff = GetPawnsWithoutHediff(
                pawnsInRange,
                "PsychicInvisibility"
            );
            IEnumerable<Pawn> eligibleColonists = GetColonists(GetPawnsNotDown(pawnsWithoutHediff));

            target ??= GetClosestTo(eligibleColonists, __instance);
        }

        return target is not null && CastAbilityOnTarget(ability, target);
    }

    private static bool HandleWordOfImmunity(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        float range = ability.GetRangeForPawn();
        IEnumerable<Pawn> pawnsInRange = GetPawnsInRange(__instance, range);
        IEnumerable<Pawn> pawnsWithoutHediff = GetPawnsWithoutHediff(pawnsInRange, "VPE_Immunity");
        Pawn[] eligiblePawns = GetImmunizablePawns(pawnsWithoutHediff).ToArray();

        Pawn? target = null;

        if (BetterAutocastVPE.Settings.WordOfImmunityTargetColonists)
            target ??= GetClosestTo(GetColonists(eligiblePawns), __instance);
        if (BetterAutocastVPE.Settings.WordOfImmunityTargetColonyAnimals)
            target ??= GetClosestTo(GetColonyAnimals(eligiblePawns), __instance);
        if (BetterAutocastVPE.Settings.WordOfImmunityTargetSlaves)
            target ??= GetClosestTo(GetSlaves(eligiblePawns), __instance);
        if (BetterAutocastVPE.Settings.WordOfImmunityTargetPrisoners)
            target ??= GetClosestTo(GetPrisoners(eligiblePawns), __instance);
        if (BetterAutocastVPE.Settings.WordOfImmunityTargetVisitors)
            target ??= GetClosestTo(GetVisitors(eligiblePawns), __instance);

        return target is not null && CastAbilityOnTarget(ability, target);
    }
    #endregion Protector

    #region Necropath
    private static bool HandleStealVitality(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        if (__instance.HasHediff("VPE_GainedVitality"))
            return false;

        Pawn[] pawnsInRange = GetPawnsInRange(__instance, ability.GetRangeForPawn()).ToArray();

        Pawn? target = null;

        if (BetterAutocastVPE.Settings.StealVitalityFromPrisoners)
            target ??= GetHighestSensitivity(GetPrisoners(pawnsInRange));
        if (BetterAutocastVPE.Settings.StealVitalityFromSlaves)
            target ??= GetHighestSensitivity(GetSlaves(pawnsInRange));
        if (BetterAutocastVPE.Settings.StealVitalityFromColonists)
            target ??= GetHighestSensitivity(GetColonists(pawnsInRange));
        if (BetterAutocastVPE.Settings.StealVitalityFromVisitors)
            target ??= GetHighestSensitivity(GetVisitors(pawnsInRange));

        return target is not null && CastAbilityOnTarget(ability, target);
    }
    #endregion Necropath

    #region Harmonist
    private static bool HandlePsychicGuidance(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        float range = ability.GetRangeForPawn();
        IEnumerable<Pawn> pawnsInRange = GetPawnsInRange(__instance, range);
        IEnumerable<Pawn> eligiblePawns = GetColonists(GetPawnsNotDown(pawnsInRange))
            .Where(pawn => !pawn.HasHediff("VPE_PsychicGuidance"));

        Pawn? target = eligiblePawns.GetRandomElement(weightSelector: null);

        return target is not null && CastAbilityOnTarget(ability, target);
    }
    #endregion Harmonist

    #region Nightstalker
    private static bool HandleDarkvision(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        Pawn? target = null;

        if (
            BetterAutocastVPE.Settings.DarkvisionTargetSelf
            && !__instance.HasHediff("VPE_Darkvision")
        )
        {
            target ??= __instance;
        }

        if (BetterAutocastVPE.Settings.DarkvisionTargetColonists && target is null)
        {
            IEnumerable<Pawn> eligibleTargets = GetPawnsWithoutHediff(
                GetColonists(
                    GetPawnsNotDown(GetPawnsInRange(__instance, ability.GetRangeForPawn()))
                ),
                "VPE_Darkvision"
            );

            target ??= GetClosestTo(eligibleTargets, __instance);
        }

        return target is not null && CastAbilityOnTarget(ability, target);
    }

    private static bool HandleEclipse(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        return !EclipseOnMap(__instance.Map) && CastAbilityOnTarget(ability, __instance);
    }
    #endregion Nightstalker

    #region Puppeteer
    private static bool HandleBrainLeech(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        if (__instance.HasHediff("VPEP_Leeching"))
            return false;

        List<Pawn> pawnsInRange = GetPawnsInRange(__instance, ability.GetRangeForPawn()).ToList();
        Pawn? target = null;

        if (BetterAutocastVPE.Settings.BrainLeechTargetPrisoners)
            GetPrisoners(pawnsInRange).TryRandomElement(out target);
        if (BetterAutocastVPE.Settings.BrainLeechTargetSlaves && target is null)
            GetSlaves(pawnsInRange).TryRandomElement(out target);

        return target is not null && CastAbilityOnTarget(ability, target);
    }
    #endregion Puppeteer

    #region Empath
    private static bool HandleWordOfSerenity(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        float range = ability.GetRangeForPawn();
        IEnumerable<Pawn> pawnsInRange = GetPawnsInRange(__instance, range);
        IEnumerable<Pawn> pawnsWithMentalBreak = GetPawnsWithMentalBreak(pawnsInRange)
            // TODO: Add proper deny-/allowlist
            .Where(pawn =>
                pawn.MentalStateDef.defName != "Crying" && pawn.MentalStateDef.defName != "Giggling"
            );

        Pawn? target = GetClosestTo(pawnsWithMentalBreak, __instance);

        return target is not null && CastAbilityOnTarget(ability, target);
    }

    private static bool HandleWordOfJoy(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        float range = ability.GetRangeForPawn();
        IEnumerable<Pawn> pawnsInRange = GetPawnsInRange(__instance, range);
        IEnumerable<Pawn> pawnsWithoutHediff = GetPawnsWithoutHediff(pawnsInRange, "Joyfuzz");
        IEnumerable<Pawn> notDownColonists = GetColonists(GetPawnsNotDown(pawnsWithoutHediff));
        IEnumerable<Pawn> lowJoyPawns = GetLowJoyPawns(notDownColonists);

        lowJoyPawns.TryRandomElement(out Pawn? target);

        return target is not null && CastAbilityOnTarget(ability, target);
    }
    #endregion Empath

    #region Archon
    private static bool HandleWordOfProductivity(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        float range = ability.GetRangeForPawn();
        IEnumerable<Pawn> pawnsInRange = GetPawnsInRange(__instance, range);
        IEnumerable<Pawn> pawnsWithoutHediff = GetPawnsWithoutHediff(
            pawnsInRange,
            "VPE_Productivity"
        );
        IEnumerable<Pawn> eligibleColonists = GetColonists(GetPawnsNotDown(pawnsWithoutHediff));

        eligibleColonists.TryRandomElement(out Pawn? target);

        return target is not null && CastAbilityOnTarget(ability, target);
    }
    #endregion

    #region Technomancer
    private static bool HandleMend(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        if (BetterAutocastVPE.Settings.MendPawns && HandleMendByPawn(__instance, ability))
            return true;
        if (BetterAutocastVPE.Settings.MendInStockpile && HandleMendByZone(__instance, ability))
            return true;
        if (BetterAutocastVPE.Settings.MendInStorage && HandleMendByStorage(__instance, ability))
            return true;
        return false;
    }

    private static bool HandleEnchant(Pawn __instance, Ability ability)
    {
        if (__instance is null)
            throw new ArgumentNullException(nameof(__instance));
        if (ability is null)
            throw new ArgumentNullException(nameof(ability));

        if (
            BetterAutocastVPE.Settings.EnchantInStockpile
            && HandleEnchantByZone(__instance, ability)
        )
        {
            return true;
        }
        if (
            BetterAutocastVPE.Settings.EnchantInStorage
            && HandleEnchantByStorage(__instance, ability)
        )
        {
            return true;
        }
        return false;
    }

    #region Technomancer helpers
    private static bool HandleMendByPawn(Pawn __instance, Ability ability)
    {
        float range = ability.GetRangeForPawn();
        IEnumerable<Pawn> pawnsInRange = GetPawnsInRange(__instance, range);
        IEnumerable<Pawn> colonistPawns = GetColonists(pawnsInRange);

        GetRandomPawnsWithDamagedEquipment(colonistPawns).TryRandomElement(out Pawn? target);

        return target is not null && CastAbilityOnTarget(ability, target);
    }

    private static bool HandleMendByZone(Pawn __instance, Ability ability)
    {
        Thing? target = GetRandomAllowedDamagedThingInStockpile(__instance.Map, __instance);

        return target is not null && CastAbilityOnTarget(ability, target);
    }

    private static bool HandleMendByStorage(Pawn __instance, Ability ability)
    {
        Thing? target = GetRandomAllowedDamagedThingInStorage(__instance.Map, __instance);

        return target is not null && CastAbilityOnTarget(ability, target);
    }

    private static bool HandleEnchantByZone(Pawn __instance, Ability ability)
    {
        Thing? target = GetRandomEnchantableThingInStockpile(__instance.Map, ability);

        return target is not null && CastAbilityOnTarget(ability, target);
    }

    private static bool HandleEnchantByStorage(Pawn __instance, Ability ability)
    {
        Thing? target = GetRandomEnchantableThingInStorage(__instance.Map, ability);

        return target is not null && CastAbilityOnTarget(ability, target);
    }
    #endregion Technomancer helpers
    #endregion Technomancer
    #endregion handlers
}
