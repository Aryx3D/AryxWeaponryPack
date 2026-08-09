using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace AryxWeaponryExpansion
{
    //absolute fucking bullshit the lot of this.
    [HarmonyPatch( typeof(FuelTank),"Awake")]
    internal static class FuelTank_Awake_DropTankPatch
    {
        private static bool Prefix(
            FuelTank __instance)
        {
            return !(__instance is AryxDropTankFuelTank);
        }
    }
    //God has left us
    [HarmonyPatch(typeof(Aircraft), nameof(Aircraft.UseFuel))]
    internal static class Aircraft_UseFuel_ExternalFirstPatch
    {
        private const float Epsilon = 0.0001f;

        private static bool Prefix(Aircraft __instance, float fuelDrawn, ref bool __result)
        {
            List<FuelTank> tanks =__instance.GetFuelTanks();

            float totalFuel = 0f;
            float totalCapacity = 0f;

            for (int i = 0; i < tanks.Count; i++)
            {
                FuelTank tank = tanks[i];

                if (tank == null)
                {
                    continue;
                }

                totalFuel += tank.GetLevel();
                totalCapacity += tank.GetCapacity();
            }

            float fuelRatio = totalCapacity > Epsilon ? totalFuel / totalCapacity: 0f;

            if (totalFuel <= Epsilon)
            {
                __instance.CheckNeedsFuel(0f);
                __result = false;
                return false;
            }

            float remainingDraw = Mathf.Max(0f, fuelDrawn);

            //drain all drop tanks proportionally first
            remainingDraw = DrainGroup(tanks, remainingDraw, external: true);

            // only touch internal fuel once external fuel is exhausted
            if (remainingDraw > Epsilon)
            {
                remainingDraw = DrainGroup(tanks, remainingDraw, external: false);
            }

            __instance.CheckNeedsFuel(fuelRatio);
            __result = true;

            return false;
        }

        private static float DrainGroup(List<FuelTank> tanks, float requestedDraw, bool external)
        {
            if (requestedDraw <= Epsilon)
            {
                return 0f;
            }

            float groupFuel = 0f;

            for (int i = 0; i < tanks.Count; i++)
            {
                FuelTank tank = tanks[i];

                if (!MatchesGroup(tank, external))
                {
                    continue;
                }

                groupFuel += tank.GetLevel();
            }

            if (groupFuel <= Epsilon)
            {
                return requestedDraw;
            }

            float actualDraw = Mathf.Min(requestedDraw, groupFuel);

            
            //proportional within the group means paired wing tanks remain
            //balanced and empty together.
            
            for (int i = 0; i < tanks.Count; i++)
            {
                FuelTank tank = tanks[i];

                if (!MatchesGroup(tank, external))
                {
                    continue;
                }

                float tankFuel = tank.GetLevel();

                if (tankFuel <= Epsilon)
                {
                    continue;
                }

                float tankDraw = actualDraw * (tankFuel / groupFuel);

                tank.UseFuel(tankDraw);
            }

            return Mathf.Max(0f, requestedDraw - actualDraw);
        }

        private static bool MatchesGroup(FuelTank tank, bool external)
        {
            if (tank == null || tank.GetCapacity() <= Epsilon || tank.GetLevel() <= Epsilon)
            {
                return false;
            }

            bool isExternal = tank is IExternalFuelTank;

            return isExternal == external;
        }
    }
}
