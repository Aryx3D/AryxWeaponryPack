using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace AryxWeaponryExpansion
{
    [HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.RegisterWeapon))]
    internal static class WeaponManager_RegisterWeapon_HardpointAddonPatch
    {
        private static bool Prefix(Weapon weapon, WeaponMount weaponMount, Hardpoint hardpoint)
        {
            if (!(weapon is IHardpointAddon))
            {
                return true;
            }

            if (hardpoint == null || hardpoint.part == null)
            {
                Debug.LogError("[Aryx Weaponry Expansion] Cannot initialise addon: hardpoint or part is null.",weapon);

                return false;
            }

            Aircraft aircraft = hardpoint.part.parentUnit as Aircraft;

            if (aircraft == null)
            {
                Debug.LogError("[Aryx Weaponry Expansion] Hardpoint part does not belong to an aircraft.", weapon);

                return false;
            }

            /*
             * WeaponStation.RegisterWeapon would ordinarily call this, but we're
             * deliberately preventing creation of the WeaponStation because no we do not want flare ejectors and targeting pods to be weapon stations.
             */
            weapon.AttachToHardpoint(aircraft, hardpoint, weaponMount);

            return false;
        }
    }
}
