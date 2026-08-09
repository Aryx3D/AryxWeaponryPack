using HarmonyLib;
using UnityEngine;

namespace AryxWeaponryExpansion
{
    //God have mercy on my soul that I have to make this shit publicly viewable.
    //If you're reading this, I am sorry for this abomination.
    public sealed class AryxDropTankFuelTank : FuelTank, IExternalFuelTank
    {
        //I HATE REFLECTION!
        private static readonly System.Reflection.FieldInfo CapacityField = AccessTools.Field(typeof(FuelTank), "fuelCapacity");

        private static readonly System.Reflection.FieldInfo PartField = AccessTools.Field( typeof(FuelTank),"part");

        private static readonly System.Reflection.FieldInfo AircraftField = AccessTools.Field(typeof(FuelTank),"aircraft");

        private Aircraft aircraft;
        private UnitPart mountedPart;

        private bool initialised;
        private bool registered;

        /*
         * suppress Unity from invoking FuelTank.Awake() before the weapon system
         * supplies the aircraft and hardpoint references.
         */
        private void Awake()
        {
            enabled = false;
        }

        public void Initialise(Aircraft aircraft, UnitPart mountedPart, float capacity, float initialFuel)
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

            if (mountedPart == null)
            {
                Debug.LogError("[Aryx Weaponry Expansion] Hardpoint part was null.", this);

                return;
            }

            this.aircraft = aircraft;
            this.mountedPart = mountedPart;

            PartField.SetValue(this, mountedPart);

            AircraftField.SetValue(this, aircraft);

            fuelMass = 0f;

            aircraft.RegisterFuelTank(this);
            registered = true;
            initialised = true;

            SetCapacity(capacity);
            SetFuelLevel(initialFuel);

            aircraft.RecalcFuelCapacity();

            /*
             * only want FuelTank's public storage/consumption methods.
             * Its FixedUpdate handles physical tank damage and leakage, which
             * belongs to the aircraft part rather than this proxy.
             */
            enabled = false;
        }
        public void Restore(float capacity, float fuel)
        {
            if (!initialised)
            {
                return;
            }

            SetCapacity(capacity);
            SetFuelLevel(fuel);

            aircraft?.RecalcFuelCapacity();
        }

        public void SetCapacity(float capacity)
        {
            capacity =
                Mathf.Max(0f, capacity);

            CapacityField.SetValue(
                this,
                capacity);

            if (fuelMass > capacity)
            {
                SetFuelLevel(capacity);
            }
        }

        public void SetFuelLevel(float fuel)
        {
            float capacity = GetCapacity();

            fuel = Mathf.Clamp(fuel, 0f, capacity);

            float massChange = fuel - fuelMass;

            fuelMass = fuel;

            if (mountedPart != null && Mathf.Abs(massChange) > 0.0001f)
            {
                mountedPart.ModifyMass(massChange);
            }
        }

        private void OnDestroy()
        {
            if (!initialised)
            {
                return;
            }

            /*
             * Loadout changes destroy the mounted prefab.  Aircraft has no public
             * DeregisterFuelTank(), but GetFuelTanks() returns the underlying list.
             * I hate everything about this.
             */
            SetFuelLevel(0f);
            SetCapacity(0f);

            if (aircraft != null)
            {
                if (registered)
                {
                    aircraft.GetFuelTanks().Remove(this);

                    registered = false;
                }

                aircraft.RecalcFuelCapacity();
            }

            aircraft = null;
            mountedPart = null;
            initialised = false;
        }
    }
}