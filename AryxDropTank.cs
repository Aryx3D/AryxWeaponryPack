using System.Collections.Generic;
using UnityEngine;

namespace AryxWeaponryExpansion
{
    public sealed class AryxDropTank : MountedMissile
    {
        [Header("Drop tank")]
        [SerializeField]
        [Min(0f)]
        private float fuelCapacity = 500f;

        [SerializeField]
        [Range(0f, 1f)]
        private float initialFillFraction = 1f;
        [SerializeField]
        [Range(0f, 1f)]
        private float rearmFillFraction = 1f;
        [SerializeField]
        private bool autoJettisonWhenEmpty = true;
        [SerializeField]

        private Aircraft aircraft;
        private AryxDropTankFuelTank fuelTank;
        private bool released;
        private bool init = false;

        private void FixedUpdate() //this ought to go in a slow update runner
        {
            if (!init || !autoJettisonWhenEmpty)
            {
                return;
            }

            CheckAutoEject();
        }
        public override void AttachToHardpoint( Aircraft aircraft, Hardpoint hardpoint, WeaponMount weaponMount)
        {
            base.AttachToHardpoint(aircraft, hardpoint, weaponMount);

            this.aircraft = aircraft;
            released = false;

            fuelTank = GetComponent<AryxDropTankFuelTank>();

            if (fuelTank == null)
            {
                fuelTank = gameObject.AddComponent<AryxDropTankFuelTank>();
            }

            fuelTank.Initialise(aircraft, hardpoint.part, fuelCapacity, fuelCapacity * initialFillFraction);
            init = true;
        }

        //lowk it'd be funny to make drop tank nukes as event content
        public override void Fire(Unit owner, Unit target, Vector3 inheritedVelocity, WeaponStation weaponStation, GlobalPosition aimpoint)
        {
            if (released || GetAmmoLoaded() <= 0)
            {
                return;
            }

            released = true;

            ReleaseFuel();

            /* in order to make my life easier
             * MountedMissile parent class handles
             * - removing dry mass/drag/rcs
             * - networking
             * - rail animation
             * - spawning the unguided projectile prefab
             * - (additional) not updating the fucking hud apparently
             */
            base.Fire(owner, target, inheritedVelocity, weaponStation, aimpoint);

            //test
            weaponStation.Updated();
            weaponStation.UpdateLastFired(1);
        }

        public override void Rearm(int ammoToRearm, WeaponStation weaponStation)
        {
            bool wasReleased = released;

            base.Rearm(ammoToRearm, weaponStation);

            if (!wasReleased || GetAmmoLoaded() <= 0)
            {
                return;
            }

            released = false;

            fuelTank?.Restore(fuelCapacity, fuelCapacity * rearmFillFraction);
        }

        private void CheckAutoEject()
        {
            if (aircraft == null || fuelTank == null || released || weaponStation == null || !aircraft.LocalSim)
            {
                return;
            }
            if (fuelTank.GetLevel() > 0.001f)
            {
                return;
            }
            Fire(aircraft, null, aircraft.rb.GetPointVelocity(transform.position), weaponStation, default);
        }

        private void ReleaseFuel()
        {
            if (aircraft == null || fuelTank == null)
            {
                return;
            }

            float remainingFuel = fuelTank.GetLevel();

            float transferredFuel = 0f;

            /*
             * Fuel state only meaningfully advances on the aircraft's local
             * simulation. Remote representations still remove the tank capacity,
             * but should not independently alter the aircraft's fuel state.
             */
            if (aircraft.LocalSim && remainingFuel > 0f)
            {
                transferredFuel = DistributeFuelToInternalTanks(remainingFuel);
            }

            /*
             * Remove all remaining fuel from the external tank.
             *
             * Any amount which did not fit into an internal tank leaves with the
             * jettisoned tank.
             */
            fuelTank.SetFuelLevel(0f);
            fuelTank.SetCapacity(0f);

            aircraft.RecalcFuelCapacity();

            if (GameManager.IsLocalAircraft(aircraft))
            {
                float lostFuel = Mathf.Max( 0f, remainingFuel - transferredFuel);

                if (lostFuel > 0.01f)
                {
                    Debug.Log($"[Drop Tank] transferred {transferredFuel:F1} fuel; {lostFuel:F1} remained in the jettisoned tank.", this);
                }
            }
        }

        //this is stupid as fuck.
        private float DistributeFuelToInternalTanks(float availableFuel)
        {
            List<FuelTank> allTanks = aircraft.GetFuelTanks();

            var rejectedTanks = new HashSet<FuelTank>();

            float remainingFuel = availableFuel;

            float transferredFuel = 0f;

            /*
             * Repeat because leaking tanks can reject Refuel(), requiring their
             * assigned share to be redistributed amongst healthy tanks.
             */
            for (int pass = 0; pass < allTanks.Count &&remainingFuel > 0.001f; pass++)
            {
                float totalFreeCapacity = 0f;

                for (int i = 0; i < allTanks.Count; i++)
                {
                    FuelTank tank = allTanks[i];

                    if (!IsEligibleRecipient(tank, rejectedTanks))
                    {
                        continue;
                    }

                    totalFreeCapacity += Mathf.Max( 0f,tank.GetCapacity() - tank.GetLevel());
                }

                if (totalFreeCapacity <= 0.001f)
                {
                    break;
                }

                float fuelAtPassStart = remainingFuel;

                bool madeProgress = false;

                for (int i = 0; i < allTanks.Count; i++)
                {
                    FuelTank tank = allTanks[i];

                    if (!IsEligibleRecipient( tank,rejectedTanks))
                    {
                        continue;
                    }

                    float capacity = tank.GetCapacity();

                    float currentFuel = tank.GetLevel();

                    float freeCapacity = Mathf.Max( 0f, capacity - currentFuel);

                    if (freeCapacity <= 0.001f)
                    {
                        continue;
                    }

                    float requestedTransfer = fuelAtPassStart * (freeCapacity / totalFreeCapacity);

                    requestedTransfer = Mathf.Min(requestedTransfer,freeCapacity, remainingFuel);

                    if (requestedTransfer <= 0.001f)
                    {
                        continue;
                    }

                    float desiredFuel =
                        currentFuel + requestedTransfer;

                    float desiredRatio =
                        desiredFuel / capacity;

                    if (!tank.Refuel(desiredRatio))
                    {
                         //FuelTank.Refuel() returns false for a leaking tank.

                        rejectedTanks.Add(tank);
                        continue;
                    }

                    float actualTransfer = Mathf.Max( 0f, tank.GetLevel() - currentFuel);

                    remainingFuel -= actualTransfer;
                    transferredFuel += actualTransfer;
                    madeProgress |= actualTransfer > 0.001f;
                }

                if (!madeProgress)
                {
                    break;
                }
            }

            return transferredFuel;
        }

        private bool IsEligibleRecipient(FuelTank tank, HashSet<FuelTank> rejectedTanks)
        {
            //le brick selection
            return tank != null &&
                   tank != fuelTank &&
                   !rejectedTanks.Contains(tank) &&
                   tank.GetCapacity() > 0f &&
                   tank.GetLevel() <
                   tank.GetCapacity() - 0.001f;
        }
    }

    //stubs! interfaces! excitement! [DING!]
    public interface IExternalFuelTank
    {
    }
}