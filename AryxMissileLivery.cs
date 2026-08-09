using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace AryxWeaponryExpansion
{
    //ugh
    //a bastard abomination because colourablemount depends on the WMngr
    //do not use this on regular lit shaders, use on colorable ones like recolourableattachment
    public sealed class AryxMissileLivery : MonoBehaviour
    {
        [Header("solid aircraft colour")]
        [SerializeField]
        private Renderer[] colorableRenderers;

        [Header("aircraft livery texture")]
        [SerializeField]
        private Renderer[] skinnableRenderers;

        [SerializeField]
        private int maximumWaitFrames = 120;

        private readonly HashSet<Material> instantiatedMaterials = new HashSet<Material>();
        //I am so bored of reflection vro
        private static readonly FieldInfo LiveryDataField = AccessTools.Field(typeof(WeaponManager), "liveryData");

        private void Start()
        {
            ApplyWhenReady().Forget();
        }

        private async UniTaskVoid ApplyWhenReady()
        {
            CancellationToken cancel = destroyCancellationToken;

            Missile missile = GetComponentInParent<Missile>();

            if (missile == null)
            {
                return;
            }

            //SpawnMissile may initialise the owner after the projectile's
            //components have received Awake/Start. ask me how I fucking know.
            for (int frame = 0; frame < maximumWaitFrames && missile.owner == null; frame++)
            {
                await UniTask.Yield();

                if (cancel.IsCancellationRequested)
                {
                    return;
                }
            }

            Aircraft aircraft = missile.owner as Aircraft;

            if (aircraft == null || aircraft.weaponManager == null)
            {
                return;
            }

            LiveryData liveryData = null;

            for (int frame = 0; frame < maximumWaitFrames && liveryData == null; frame++)
            {
                liveryData = LiveryDataField?.GetValue(aircraft.weaponManager) as LiveryData;

                if (liveryData != null)
                {
                    break;
                }

                await UniTask.Yield();

                if (cancel.IsCancellationRequested)
                {
                    return;
                }
            }

            if (liveryData == null)
            {
                Debug.LogWarning("[Aryx Weaponry Expansion] Aircraft livery data was unavailable.", this);

                return;
            }

            ApplyColours(liveryData);
            ApplySkin(liveryData);
        }

        private void ApplyColours(
            LiveryData liveryData)
        {
            LiveryData.TextureColor[] colours = liveryData.Colors;

            if (colours == null || colours.Length == 0)
            {
                return;
            }

            Color colour = colours[0].Color;

            if (colorableRenderers == null)
            {
                return;
            }

            for (int i = 0; i < colorableRenderers.Length; i++)
            {
                Renderer renderer =
                    colorableRenderers[i];

                if (renderer == null)
                {
                    continue;
                }

                Material material =
                    GetInstancedMaterial(renderer);

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", colour);
                }
            }
        }

        private void ApplySkin(LiveryData liveryData)
        {
            if (skinnableRenderers == null)
            {
                return;
            }

            for (int i = 0; i < skinnableRenderers.Length; i++)
            {
                Renderer renderer =
                    skinnableRenderers[i];

                if (renderer == null)
                {
                    continue;
                }

                Material material = GetInstancedMaterial(renderer);

                if (material.HasProperty("_Livery"))
                {
                    material.SetTexture("_Livery", liveryData.Texture);
                }

                if (material.HasProperty("_Glossiness"))
                {
                    material.SetFloat("_Glossiness",liveryData.Glossiness);
                }
            }
        }

        private Material GetInstancedMaterial(Renderer renderer)
        {
            Material material = renderer.material;

            instantiatedMaterials.Add(material);

            return material;
        }

        private void OnDestroy()
        {
            foreach (Material material in instantiatedMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            instantiatedMaterials.Clear();
        }
    }
}
