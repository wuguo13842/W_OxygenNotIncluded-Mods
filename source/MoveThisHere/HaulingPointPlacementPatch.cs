using HarmonyLib;
using STRINGS;
using System;
using System.Reflection;
using UnityEngine;
using static MoveThisHere.STRINGS;

namespace MoveThisHere
{
    // 允许 HaulingPoint 放在已有建筑所在的格子上。
    // 原版 IsValidPlaceLocation 在 Building 层已有建筑时返回 HELP_BUILDLOCATION_OCCUPIED，
    // 这里对 HaulingPoint 忽略这个失败原因，其余检查保持原样。
    //
    // 用 TargetMethod() 指定带 out string 的 6 参数重载，因为特性实参里不能写
    // typeof(string).MakeByRefType()。
    [HarmonyPatch]
    public static class BuildingDef_IsValidPlaceLocation_HaulingPoint_Patch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(BuildingDef),
                "IsValidPlaceLocation",
                new Type[]
                {
                    typeof(GameObject),
                    typeof(int),
                    typeof(Orientation),
                    typeof(bool),
                    typeof(string).MakeByRefType(),
                    typeof(bool)
                });
        }

        public static void Postfix(GameObject source_go, ref bool __result, ref string fail_reason)
        {
            if (__result) return;
            if (source_go == null) return;

            // source_go 是 BuildTool 的 visualizer，其 Building.Def 就是当前建筑的 Def
            var building = source_go.GetComponent<Building>();
            if (building == null || building.Def == null) return;
            if (building.Def.PrefabID != HaulingPointConfig.Id) return;

            // 只放过"位置被占用"这一种失败，其他失败原因（非法格、Unobtanium 等）仍保留
            string occupied = UI.TOOLTIPS.HELP_BUILDLOCATION_OCCUPIED;
            if (fail_reason == occupied)
            {
                __result = true;
                fail_reason = null;
            }
        }
    }
}