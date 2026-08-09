using System.Reflection;
using UnityEngine;

namespace AryxWeaponryExpansion
{
    public sealed class AryxExternalTargetingPodAddon : Weapon, IHardpointAddon
    {
        [Header("Laser designation")]
        [SerializeField]
        [Min(0)]
        private int addedLaserTargets = 1;

        [Header("Target detection")]
        [SerializeField]
        private AryxHardpointTargetDetector[] targetDetectors;

        private Aircraft aircraft;
        private LaserDesignator laserDesignator;

        private int appliedLaserTargets;
        private bool contributionApplied;

        private static readonly FieldInfo MaxTargetsField = typeof(LaserDesignator).GetField("maxTargets", BindingFlags.Instance | BindingFlags.NonPublic);
        public override void AttachToHardpoint(Aircraft aircraft, Hardpoint hardpoint, WeaponMount weaponMount)
        {
            RemoveContribution();

            base.AttachToHardpoint(aircraft, hardpoint, weaponMount);

            this.aircraft = aircraft;

            //defensive bullshit
            if (aircraft == null)
            {
                Debug.LogError("[Aryx Weaponry Expansion] Aircraft was null.", this);

                return;
            }

            if (hardpoint == null || hardpoint.part == null)
            {
                Debug.LogError("[Aryx Weaponry Expansion] Hardpoint or hardpoint part was null.", this);

                return;
            }

            ApplyLaserContribution();
            InitialiseTargetDetectors(hardpoint.part);

            contributionApplied = true;
        }

        private void ApplyLaserContribution()
        {
            if (addedLaserTargets <= 0)
            {
                return;
            }

            laserDesignator = aircraft.GetLaserDesignator();

            if (laserDesignator == null)
            {
                Debug.LogWarning("[Aryx Weaponry Expansion] Aircraft has no LaserDesignator; laser target contribution was not applied.", aircraft);

                return;
            }

            if (MaxTargetsField == null)
            {
                Debug.LogError("[Aryx Weaponry Expansion] Could not locate LaserDesignator.maxTargets.", this);

                laserDesignator = null;
                return;
            }

            int currentMaximum = laserDesignator.GetMaxTargets();

            appliedLaserTargets = Mathf.Max(0, addedLaserTargets);

            MaxTargetsField.SetValue(laserDesignator, currentMaximum + appliedLaserTargets);
        }

        private void InitialiseTargetDetectors(UnitPart hardpointPart)
        {
            if (targetDetectors == null || targetDetectors.Length == 0)
            {
                targetDetectors = GetComponentsInChildren<AryxHardpointTargetDetector>(true);
            }

            for (int i = 0; i < targetDetectors.Length; i++)
            {
                AryxHardpointTargetDetector detector = targetDetectors[i];

                if (detector == null)
                {
                    continue;
                }

                detector.Bind(aircraft, hardpointPart);
            }
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

            if (laserDesignator != null && appliedLaserTargets > 0 && MaxTargetsField != null)
            {
                int reducedMaximum = Mathf.Max(0, laserDesignator.GetMaxTargets() - appliedLaserTargets);

                MaxTargetsField.SetValue(laserDesignator, reducedMaximum);
            }

            aircraft = null;
            laserDesignator = null;
            appliedLaserTargets = 0;
        }
    }
}