using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace NoAimSwing
{
    [BepInPlugin("ciallo.noaimswing", "No Aim OverSwing", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ConfigEntry<float> AimSwayValue;
        public static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            AimSwayValue = Config.Bind(
                "General",
                "Aim Overswing Strength",
                0.1f,
                new ConfigDescription("Set the percentage of start-aiming overswing, when weapon has high weight and low ergo",
                    new AcceptableValueRange<float>(0f, 1f))
            );

            Harmony harmony = new Harmony("ciallo.noaimswing");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(EFT.Animations.ProceduralWeaponAnimation), "set_IsAiming")]
    public class Patch_IsAiming
    {
        static void Postfix(EFT.Animations.ProceduralWeaponAnimation __instance, bool value)
        {
            if (!value)
                return;

            float overrideValue = Plugin.AimSwayValue.Value;

            float swayStrength = Traverse.Create(__instance).Field("_aimSwayStrength").GetValue<float>();
            if (swayStrength <= 0f)
                return;

            var swayBlender = Traverse.Create(__instance).Field("_aimSwayBlender").GetValue<object>();
            if (swayBlender == null)
                return;

            Traverse.Create(swayBlender).Property("Value").SetValue(overrideValue);
        }
    }
}
