using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace AryxWeaponryExpansion
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Aryx Dynamics: {MyPluginInfo.PLUGIN_GUID} active.");

            Harmony harmony = new Harmony("com.aryx.weaponpack1");
            harmony.PatchAll();
        }
    }
}
