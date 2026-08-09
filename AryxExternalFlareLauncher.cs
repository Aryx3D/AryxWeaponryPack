using System;
using System.Reflection;
using UnityEngine;

namespace AryxWeaponryExpansion
{
    //industrial grade jank
    public sealed class AryxExternalFlareLauncher : Weapon, IHardpointAddon
    {
        [Header("External flare launcher")]
        [SerializeField]
        [Min(1)]
        private int addedFlares = 16;

        [SerializeField]
        private Transform ejectionTransform;

        private Aircraft aircraft;
        private FlareEjector flareEjector;
        private FlareEjector.EjectionPoint contributedPoint;

        private int appliedFlareCount;
        private bool contributionApplied;

        private static readonly FieldInfo AmmoField = FindInstanceField(typeof(FlareEjector), "ammo");

        private static readonly FieldInfo MaxAmmoField = FindInstanceField(typeof(FlareEjector), "maxAmmo");

        private static readonly FieldInfo EjectionPointsField = FindInstanceField(typeof(FlareEjector), "ejectionPoints");

        public override void AttachToHardpoint( Aircraft aircraft, Hardpoint hardpoint, WeaponMount weaponMount)
        {
            base.AttachToHardpoint(aircraft, hardpoint, weaponMount);

            // comically defensive because I cannot be bothered reading stacktraces anymore with this game's obsession (2026) with not null checking.
            RemoveContribution();

            this.aircraft = aircraft;

            if (aircraft == null)
            {
                Debug.LogError(
                    "[Aryx Weaponry Expansion] Attached without an aircraft.",
                    this);
                return;
            }

            if (hardpoint == null || hardpoint.part == null)
            {
                Debug.LogError("[Aryx Weaponry Expansion] Hardpoint has no associated part.", this); 
                return;
            }

            if (ejectionTransform == null)
            {
                Debug.LogError( "[Aryx Weaponry Expansion] No ejection transform configured.",this); 
                return;
            }

            if (addedFlares <= 0)
            {
                Debug.LogWarning("[Aryx Weaponry Expansion] Added flare count is zero.", this);
                return;
            }
            //this breaks if an aircraft has its ejectors not on its root part bc of complex physics
            //if anyone does this and it breaks my shit I'm coming after you pal.
            FlareEjector[] ejectors = aircraft.GetComponentsInChildren<FlareEjector>(true);

            if (ejectors.Length == 0)
            {
                Debug.LogError("[Aryx Weaponry Expansion] Aircraft has no FlareEjector on root part", aircraft);
                return;
            }

            if (ejectors.Length > 1)
            {
                Debug.LogWarning( $"[Aryx Weaponry Expansion] Aircraft has {ejectors.Length} FlareEjectors; using the first one.", aircraft);
            }

            flareEjector = ejectors[0];

            contributedPoint = new FlareEjector.EjectionPoint
            {
                // Deliberately use the aircraft part carrying the hardpoint.
                part = hardpoint.part,
                transform = ejectionTransform
            };

            AddEjectionPoint(contributedPoint);

            appliedFlareCount = addedFlares;

            SetAmmo(GetAmmo() + appliedFlareCount);
            SetMaxAmmo(GetMaxAmmo() + appliedFlareCount);

            contributionApplied = true;

            RefreshHud();
        }

        private void OnDestroy()
        {
            RemoveContribution();
        }

        private void RemoveContribution()
        {
            if (!contributionApplied)
            {
                return;
            }

            contributionApplied = false;

            // During complete aircraft destruction, object teardown order is undefined and cleanup no longer matters.
            if (flareEjector == null)
            {
                return;
            }

            RemoveEjectionPoint(contributedPoint);

            int reducedMaximum =
                Mathf.Max(0, GetMaxAmmo() - appliedFlareCount);

            /*
             * Do not blindly subtrac the pack capacity from current ammo.
             *
             * Example:
             *   internal capacity: 60
             *   external capacity: 30
             *   current ammo:      40
             *
             * The external flares may already have been expended. Clamping to the
             * reduced maximum prsrv the remaining shared stock sensibly.
             */
            SetMaxAmmo(reducedMaximum);
            SetAmmo(Mathf.Min(GetAmmo(), reducedMaximum));

            RefreshHud();

            contributedPoint = null;
            flareEjector = null;
            aircraft = null;
            appliedFlareCount = 0;
        }

        private void AddEjectionPoint(FlareEjector.EjectionPoint newPoint)
        {
            FlareEjector.EjectionPoint[] existing = GetEjectionPoints();

            var expanded = new FlareEjector.EjectionPoint[existing.Length + 1];

            Array.Copy(existing, expanded, existing.Length);
            expanded[existing.Length] = newPoint;

            EjectionPointsField.SetValue(flareEjector, expanded);
        }

        private void RemoveEjectionPoint(FlareEjector.EjectionPoint point)
        {
            if (point == null)
            {
                return;
            }

            FlareEjector.EjectionPoint[] existing = GetEjectionPoints();

            int retainedCount = 0;

            for (int i = 0; i < existing.Length; i++)
            {
                if (!ReferenceEquals(existing[i], point))
                {
                    retainedCount++;
                }
            }

            if (retainedCount == existing.Length)
            {
                return;
            }

            var reduced = new FlareEjector.EjectionPoint[retainedCount];

            int destinationIndex = 0;

            for (int i = 0; i < existing.Length; i++)
            {
                if (ReferenceEquals(existing[i], point))
                {
                    continue;
                }

                reduced[destinationIndex++] = existing[i];
            }

            EjectionPointsField.SetValue(flareEjector, reduced);
        }

        private FlareEjector.EjectionPoint[] GetEjectionPoints()
        {
            return EjectionPointsField.GetValue(flareEjector) as FlareEjector.EjectionPoint[] ?? Array.Empty<FlareEjector.EjectionPoint>();
        }

        private int GetAmmo()
        {
            return (int)AmmoField.GetValue(flareEjector);
        }

        private void SetAmmo(int value)
        {
            AmmoField.SetValue(flareEjector, Mathf.Max(0, value));
        }

        private int GetMaxAmmo()
        {
            return (int)MaxAmmoField.GetValue(flareEjector);
        }

        private void SetMaxAmmo(int value)
        {
            MaxAmmoField.SetValue(flareEjector, Mathf.Max(0, value));
        }

        private void RefreshHud()
        {
            if (aircraft == null ||
                flareEjector == null ||
                !GameManager.IsLocalAircraft(aircraft))
            {
                return;
            }

            if (SceneSingleton<CombatHUD>.i != null)
            {
                flareEjector.UpdateHUD();
            }
        }

        private static FieldInfo FindInstanceField(
            Type startingType,
            string fieldName)
        {
            for (Type type = startingType; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(fieldName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                if (field != null)
                {
                    return field;
                }
            }

            throw new MissingFieldException(startingType.FullName, fieldName);
        }
    }

    //stub for cleaner integration
    public interface IHardpointAddon
    {
    }
}