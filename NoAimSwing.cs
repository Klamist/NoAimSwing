using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT.Animations;
using HarmonyLib;

namespace NoAimSwing
{
    [BepInPlugin("ciallo.noaimswing", "No Aim OverSwing", "1.2.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ConfigEntry<float> AimSwayValue;
        public static ConfigEntry<float> BreathSwayMultiplier;
        public static ManualLogSource Log;

        public static float ScaleBreathSway(float intensity)
        {
            return intensity * BreathSwayMultiplier.Value;
        }

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

            BreathSwayMultiplier = Config.Bind(
                "General",
                "Holding Breath Sway Multiplier",
                0.2f,
                new ConfigDescription("Multiplier for crosshair sway while holding breath",
                    new AcceptableValueRange<float>(0f, 1f))
            );

            Harmony harmony = new Harmony("ciallo.noaimswing");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(ProceduralWeaponAnimation), "set_IsAiming")]
    public class Patch_IsAiming
    {
        static void Postfix(ProceduralWeaponAnimation __instance, bool value)
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

    [HarmonyPatch(typeof(BreathEffector), "Process")]
    public static class Patch_BreathSway_Transpiler
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            MethodInfo scaleMethod = AccessTools.Method(typeof(Plugin), nameof(Plugin.ScaleBreathSway));

            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].opcode != OpCodes.Ldc_R4 || !(codes[i].operand is float intensity) ||
                    Math.Abs(intensity - 0.15f) > 0.0001f ||
                    codes[i + 1].opcode != OpCodes.Stfld ||
                    !(codes[i + 1].operand is FieldInfo field) ||
                    field.DeclaringType != typeof(BreathEffector) ||
                    (field.Name != "_breathIntensity" && field.Name != "_shakeIntensity"))
                {
                    continue;
                }

                // 只缩放原始 Process 的屏息强度，保留完整的晃动处理流程。
                codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, scaleMethod));
                i++;
            }

            return codes;
        }
    }
}