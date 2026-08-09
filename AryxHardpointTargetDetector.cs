using UnityEngine;


namespace AryxWeaponryExpansion
{
    public sealed class AryxHardpointTargetDetector : TargetDetector
    {
        private bool initialised;

        /*
         * suppress the normal TargetDetector Awake;
         * The pod is instantiated before its Weapon-derived addon receives
         * AttachToHardpoint(), so attachedUnit and part cannot be assigned yet.
         */
        protected override void Awake()
        {
        }

        /// <summary>
        /// binds the detector to the aircraft and part, called by external laser pod
        /// remndr: make this independent later if I decide to separate shit
        /// </summary>
        /// <param name="aircraft"></param>
        /// <param name="hardpointPart"></param>
        public void Bind(Aircraft aircraft, UnitPart hardpointPart)
        {
            if (initialised)
            {
                return;
            }

            if (aircraft == null)
            {
                Debug.LogError("[Aryx Weaponry Expansion] Aircraft was null.", this);

                return;
            }

            if (hardpointPart == null)
            {
                Debug.LogError("[Aryx Weaponry Expansion] Hardpoint part was null.", this);

                return;
            }

            attachedUnit = aircraft;
            part = hardpointPart;

            if (scanner == null)
            {
                scanner = transform;
            }

            /*
             * Performs the normal subscriptions now that the required
             * references exist.
             */
            base.Awake();

            /*
             * weapon mounts are usually spawned from WeaponManager's
             * aircraft.onInitialize callback (can't wait for this to break next update). A listener added while that
             * event is already being invoked will not receive the current
             * invocation, so do it manually lmao
             */
            attachedUnit.onInitialize -= TargetDetector_OnInitialize;

            TargetDetector_OnInitialize();

            initialised = true;
        }

        protected override void OnDestroy()
        {
            if (initialised)
            {
                if (attachedUnit != null)
                {
                    attachedUnit.onInitialize -= TargetDetector_OnInitialize;

                    attachedUnit.onDisableUnit -= TargetDetector_OnUnitDisabled;
                }

                if (part != null)
                {
                    part.onApplyDamage -= TargetDetector_OnApplyDamage;
                }
            }

            base.OnDestroy();
        }
    }
}