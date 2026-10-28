using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace DockSpeed
{
    // 加快物流运输机（行星内）与星际物流运输船“进出物流塔”的动画/停靠阶段。
    // 只改这些阶段的计时步长，不改飞行速度、不改装卸数量，也不会让货物瞬间送达。
    [BepInPlugin(GUID, MOD_NAME, VERSION)]
    public sealed class DockSpeedPlugin : BaseUnityPlugin
    {
        public const string GUID = "sinthome358.dsp.dockspeed";
        public const string MOD_NAME = "Dock Speed";
        public const string VERSION = "1.1.3";

        internal static ManualLogSource Log;
        internal static ConfigEntry<float> DroneMultiplier;
        internal static ConfigEntry<float> ShipMultiplier;
        internal static ConfigEntry<float> ShipFlightMultiplier;

        // 运行时按目标站识别轨道采集器航线。每种航线状态只记一次日志，避免每帧刷屏。
        private static FieldInfo CollectorFlagField;
        private static readonly HashSet<string> LoggedCollectorRouteStates = new HashSet<string>();

        // 游戏 0.10.35 中对应常量（已从游戏代码核对）。
        private const float SHIP_PAD_A = 0.03335f;   // 起点塔停靠台阶段（stage -2）
        private const float SHIP_PAD_B = 0.0334f;    // 终点塔停靠台阶段（stage 2）
        private const float SHIP_LIFT_BASE = 0.006f; // 起降阶段（stage -1 / 1）步长系数
        private const float SHIP_LIFT_ADD = 1e-5f;
        private const float DRONE_STEP = 0.016666668f;

        private void Awake()
        {
            Log = Logger;

            DroneMultiplier = Config.Bind("General", "DroneDockSpeedMultiplier", 4f,
                new ConfigDescription(
                    "物流运输机（行星内）起飞和降落阶段的速度倍率。1 = 原版；4 = 这两段耗时变为约四分之一。飞行速度不受影响。",
                    new AcceptableValueRange<float>(1f, 20f)));
            ShipMultiplier = Config.Bind("General", "ShipDockSpeedMultiplier", 4f,
                new ConfigDescription(
                    "星际物流运输船进出物流塔（停靠台与起降）阶段的速度倍率。1 = 原版；4 = 这几段耗时变为约四分之一。航行和曲速速度不受影响。",
                    new AcceptableValueRange<float>(1f, 20f)));

            ShipFlightMultiplier = Config.Bind("General", "ShipFlightPhaseMultiplier", 4f,
                new ConfigDescription(
                    "星际物流运输船出塔后到进入曲速前（起飞加速、转向对准）以及退出曲速后到入塔前（减速、对准、降落）的速度倍率。"
                    + "1 = 原版。曲速阶段本身不受影响。倍率越大，这两段越短，但过大可能让运输船的转向轨迹变得不自然。"
                    + "只要该运输船的远端目标站是轨道采集器，往返该站的飞行段都会自动豁免此倍率；运行时会记录识别结果，便于确认特判是否命中。",
                    new AcceptableValueRange<float>(1f, 10f)));

            Type station = AccessTools.TypeByName("StationComponent");
            if (station == null)
            {
                Log.LogError("找不到 StationComponent，模组不会生效（游戏版本可能变化）。");
                return;
            }

            var harmony = new Harmony(GUID);
            TryPatch(harmony, station, "InternalTickLocal", nameof(DroneTranspiler));
            TryPatch(harmony, station, "InternalTickRemote", nameof(ShipTranspiler));

            Log.LogInfo("倍率：运输机 x" + DroneMultiplier.Value + "，运输船进出塔 x" + ShipMultiplier.Value
                + "，运输船曲速前后飞行段 x" + ShipFlightMultiplier.Value);
        }

        private static void TryPatch(Harmony harmony, Type type, string method, string transpiler)
        {
            try
            {
                MethodInfo target = AccessTools.Method(type, method);
                if (target == null)
                {
                    Log.LogError("找不到 StationComponent." + method + "，对应部分不会生效。");
                    return;
                }

                harmony.Patch(target, transpiler: new HarmonyMethod(typeof(DockSpeedPlugin), transpiler));
            }
            catch (Exception ex)
            {
                Log.LogError("修补 StationComponent." + method + " 失败：" + ex);
            }
        }

        private static bool IsLdcR4(CodeInstruction ins, float value)
        {
            if (ins.opcode != OpCodes.Ldc_R4 || !(ins.operand is float))
                return false;
            return Math.Abs((float)ins.operand - value) < 1e-7f;
        }

        // ---------- 运输机：起飞/降落阶段 ----------
        // 原版：float step = 0.016666668f * Mathf.Sqrt(droneSpeed / 8f);
        // 对应 IL：ldc.r4 0.016666668 ; ldloc.0 ; mul ; stloc  → 在 mul 之后再乘以倍率。
        private static IEnumerable<CodeInstruction> DroneTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            float k = DroneMultiplier.Value;
            if (k <= 1f)
                return original;

            var hits = new List<int>();
            for (int i = 0; i + 2 < original.Count; i++)
            {
                if (IsLdcR4(original[i], DRONE_STEP)
                    && original[i + 1].opcode == OpCodes.Ldloc_0
                    && original[i + 2].opcode == OpCodes.Mul)
                    hits.Add(i + 2);
            }

            if (hits.Count != 1)
            {
                Log.LogWarning("运输机补丁：匹配数为 " + hits.Count + "（应为 1），为安全起见不修改。游戏版本可能已变化。");
                return original;
            }

            var code = new List<CodeInstruction>(original);
            code.Insert(hits[0] + 1, new CodeInstruction(OpCodes.Ldc_R4, k));
            code.Insert(hits[0] + 2, new CodeInstruction(OpCodes.Mul));
            Log.LogInfo("运输机补丁已应用（x" + k + "）。");
            return code;
        }

        private static bool IsLocal(CodeInstruction ins, OpCode op, int index)
        {
            LocalBuilder lb = ins.operand as LocalBuilder;
            return ins.opcode == op && lb != null && lb.LocalIndex == index;
        }

        // ---------- 运输船 ----------
        // A) 进出塔（ShipDockSpeedMultiplier）
        //    1) 停靠台：t ± 0.03335f（起点，2 处）和 t ± 0.0334f（终点，2 处）→ 常量乘倍率。
        //    2) 起降：step = pow(v/600, 0.4) * 0.006f + 1e-5f，用于 stage -1 与 stage 1 → 在 add 之后乘倍率。
        // B) 曲速前后飞行段（ShipFlightPhaseMultiplier）
        //    原版性能系数 p = pow(v/600, 0.4); if (p > 1) p = ln(p) + 1。
        //    该系数影响非曲速飞行时的加速度、目标速度和转向阻尼。
        //    注意：性能系数原本在运输船循环外计算、由同一物流塔上的所有船共享。
        //    因此特殊航线豁免必须放在每艘船自己的循环中，不能在方法入口统一把倍率设为 1。
        private const float SHIP_FLIGHT_ACCEL = 0.12f;

        private static bool IsLoadArgument(CodeInstruction ins, int argumentIndex)
        {
            if (ins.opcode == OpCodes.Ldarg_0) return argumentIndex == 0;
            if (ins.opcode == OpCodes.Ldarg_1) return argumentIndex == 1;
            if (ins.opcode == OpCodes.Ldarg_2) return argumentIndex == 2;
            if (ins.opcode == OpCodes.Ldarg_3) return argumentIndex == 3;

            if (ins.opcode != OpCodes.Ldarg && ins.opcode != OpCodes.Ldarg_S)
                return false;

            if (ins.operand is ParameterInfo parameter)
                return parameter.Position + 1 == argumentIndex;
            if (ins.operand is byte byteIndex)
                return byteIndex == argumentIndex;
            if (ins.operand is short shortIndex)
                return shortIndex == argumentIndex;
            if (ins.operand is int intIndex)
                return intIndex == argumentIndex;
            return false;
        }

        private static bool IsLoadLocal(CodeInstruction ins, LocalBuilder local)
        {
            if (local == null || (ins.opcode != OpCodes.Ldloc && ins.opcode != OpCodes.Ldloc_S))
                return false;
            LocalBuilder candidate = ins.operand as LocalBuilder;
            return candidate != null && candidate.LocalIndex == local.LocalIndex;
        }

        // true 表示该船的远端目标站是轨道采集器，应使用原版飞行性能系数。
        // 为避免原版状态在靠站动画中短暂变化导致漏判，这里不再依赖 direction/itemCount 才豁免；
        // 只要当前航线关联的远端站是 collector，就对该路线的去程和返程都豁免。
        private static bool ShouldBypassFlightMultiplier(
            object remoteStation, int otherGId, int direction, int itemCount)
        {
            bool isCollector = false;
            if (remoteStation != null && CollectorFlagField != null)
            {
                try
                {
                    object value = CollectorFlagField.GetValue(remoteStation);
                    isCollector = value is bool && (bool)value;
                }
                catch (Exception ex)
                {
                    string errorKey = "collector-field-error:" + otherGId;
                    lock (LoggedCollectorRouteStates)
                    {
                        if (LoggedCollectorRouteStates.Add(errorKey))
                            Log.LogWarning("读取轨道采集器标记失败（otherGId=" + otherGId + "）：" + ex.Message);
                    }
                }
            }

            // 成功识别时按去程/返程各记一次；如果“载货返航”候选却不是 collector，也记一条诊断。
            string key = null;
            string message = null;
            bool warning = false;
            if (isCollector)
            {
                string leg = direction < 0 ? "返航方向" : "前往目标站方向";
                key = "collector:" + otherGId + ":" + (direction < 0 ? "return" : "outbound");
                message = "轨道采集器航线识别成功：otherGId=" + otherGId
                    + "，方向=" + direction + "（" + leg + "），itemCount=" + itemCount
                    + "；该航线当前运输船的 ShipFlightPhaseMultiplier 已豁免（有效倍率 x1）。";
            }
            else if (direction < 0 && itemCount > 0)
            {
                key = "non-collector-loaded-return:" + otherGId;
                string stationType = remoteStation == null ? "null" : remoteStation.GetType().FullName;
                message = "返航候选未识别为轨道采集器：otherGId=" + otherGId
                    + "，direction=" + direction + "，itemCount=" + itemCount
                    + "，目标站类型=" + stationType + "，isCollector=false。"
                    + "这表明当前游戏/其他模组中的站点标记与预期不同。";
                warning = true;
            }

            if (key != null)
            {
                lock (LoggedCollectorRouteStates)
                {
                    if (LoggedCollectorRouteStates.Add(key))
                    {
                        if (warning) Log.LogWarning(message);
                        else Log.LogInfo(message);
                    }
                }
            }

            return isCollector;
        }

        private static LocalBuilder StoredLocal(CodeInstruction ins)
        {
            if (ins.opcode != OpCodes.Stloc && ins.opcode != OpCodes.Stloc_S)
                return null;
            return ins.operand as LocalBuilder;
        }

        // 查找 ref ShipData ptr2 = ref this.workShipDatas[j] 对应的 IL。
        // 这里故意按字段名和元素类型双重校验；若游戏更新改变方法结构，就不应用飞行段补丁。
        private static int FindShipDataPointerStore(
            IList<CodeInstruction> code, out LocalBuilder shipDataRefLocal, out Type shipDataType)
        {
            shipDataRefLocal = null;
            shipDataType = null;
            int match = -1;

            for (int i = 2; i + 1 < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Ldelema || !(code[i].operand is Type elementType)
                    || elementType.Name != "ShipData")
                    continue;

                FieldInfo arrayField = code[i - 2].operand as FieldInfo;
                if (code[i - 2].opcode != OpCodes.Ldfld || arrayField == null
                    || arrayField.Name != "workShipDatas")
                    continue;

                LocalBuilder candidate = StoredLocal(code[i + 1]);
                if (candidate == null || !candidate.LocalType.IsByRef
                    || candidate.LocalType.GetElementType() == null
                    || candidate.LocalType.GetElementType().Name != "ShipData")
                    continue;

                if (match >= 0)
                    return -1; // 不止一个候选点时，为安全起见拒绝修改。

                match = i + 1;
                shipDataRefLocal = candidate;
                shipDataType = elementType;
            }

            return match;
        }

        private static IEnumerable<CodeInstruction> ShipTranspiler(
            IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var original = new List<CodeInstruction>(instructions);
            float dockK = ShipMultiplier.Value;
            float flightK = ShipFlightMultiplier.Value;
            bool doDock = dockK > 1f;
            bool doFlight = flightK > 1f;
            if (!doDock && !doFlight)
                return original;

            var padA = new List<int>();
            var padB = new List<int>();
            var lift = new List<int>();
            var perf = new List<int>();
            for (int i = 0; i < original.Count; i++)
            {
                if (IsLdcR4(original[i], SHIP_PAD_A)) padA.Add(i);
                else if (IsLdcR4(original[i], SHIP_PAD_B)) padB.Add(i);

                if (i + 3 < original.Count
                    && IsLdcR4(original[i], SHIP_LIFT_BASE)
                    && original[i + 1].opcode == OpCodes.Mul
                    && IsLdcR4(original[i + 2], SHIP_LIFT_ADD)
                    && original[i + 3].opcode == OpCodes.Add)
                    lift.Add(i + 3);

                // call Mathf.Log ; ldc.r4 1 ; add ; stloc.s 10 ; ldloc.s 8
                // 识别原版性能系数计算完成的位置。
                MethodInfo mi = original[i].operand as MethodInfo;
                if (original[i].opcode == OpCodes.Call && mi != null && mi.Name == "Log"
                    && mi.DeclaringType != null && mi.DeclaringType.Name == "Mathf"
                    && i + 4 < original.Count
                    && IsLdcR4(original[i + 1], 1f)
                    && original[i + 2].opcode == OpCodes.Add
                    && IsLocal(original[i + 3], OpCodes.Stloc_S, 10)
                    && IsLocal(original[i + 4], OpCodes.Ldloc_S, 8))
                    perf.Add(i + 4);
            }

            bool dockOk = padA.Count == 2 && padB.Count == 2 && lift.Count == 1;
            bool flightOk = false;
            LocalBuilder perfLocal = null;
            LocalBuilder accelerationLocal = null;
            LocalBuilder shipDataRefLocal = null;
            Type shipDataType = null;
            FieldInfo otherGIdField = null;
            FieldInfo directionField = null;
            FieldInfo itemCountField = null;
            FieldInfo isCollectorField = null;
            Type stationType = AccessTools.TypeByName("StationComponent");
            int shipDataPointerStore = -1;
            int accelerationHits = 0;

            if (doFlight && perf.Count == 1 && perf[0] > 0)
            {
                perfLocal = original[perf[0] - 1].operand as LocalBuilder;
                if (perfLocal != null)
                {
                    // 找到原版 num15 = shipSailSpeed * 0.12f * performanceCoefficient 的目标局部变量，
                    // 以便每艘船都用自己的有效性能系数重算它。
                    for (int i = 0; i + 5 < original.Count; i++)
                    {
                        if (IsLoadArgument(original[i], 3)
                            && IsLdcR4(original[i + 1], SHIP_FLIGHT_ACCEL)
                            && original[i + 2].opcode == OpCodes.Mul
                            && IsLoadLocal(original[i + 3], perfLocal)
                            && original[i + 4].opcode == OpCodes.Mul)
                        {
                            LocalBuilder candidate = StoredLocal(original[i + 5]);
                            if (candidate != null)
                            {
                                accelerationHits++;
                                accelerationLocal = candidate;
                            }
                        }
                    }
                }

                shipDataPointerStore = FindShipDataPointerStore(
                    original, out shipDataRefLocal, out shipDataType);

                if (shipDataType != null)
                {
                    otherGIdField = AccessTools.Field(shipDataType, "otherGId");
                    directionField = AccessTools.Field(shipDataType, "direction");
                    itemCountField = AccessTools.Field(shipDataType, "itemCount");
                }
                if (stationType != null)
                    isCollectorField = AccessTools.Field(stationType, "isCollector");

                flightOk = perf.Count == 1
                    && perfLocal != null
                    && accelerationHits == 1
                    && accelerationLocal != null
                    && shipDataPointerStore >= 0
                    && shipDataRefLocal != null
                    && otherGIdField != null
                    && directionField != null
                    && itemCountField != null
                    && isCollectorField != null
                    && generator != null;
            }

            if (doDock && !dockOk)
            {
                Log.LogWarning("运输船进出塔补丁：匹配数异常（起点停靠台 " + padA.Count + "/2，终点停靠台 " + padB.Count
                    + "/2，起降 " + lift.Count + "/1），这一部分不修改。游戏版本可能已变化。");
                doDock = false;
            }
            if (doFlight && !flightOk)
            {
                Log.LogWarning("运输船飞行段补丁：性能系数、加速度局部变量或 ShipData 字段的匹配结果异常，"
                    + "为安全起见不修改飞行段；请检查游戏版本是否变化。");
                doFlight = false;
            }
            if (!doDock && !doFlight)
                return original;

            var code = new List<CodeInstruction>(original);

            if (doDock)
            {
                // 先改常量（不改变指令数量），再在 add 之后插入乘法。
                foreach (int idx in padA)
                    code[idx] = new CodeInstruction(code[idx]) { operand = SHIP_PAD_A * dockK };
                foreach (int idx in padB)
                    code[idx] = new CodeInstruction(code[idx]) { operand = SHIP_PAD_B * dockK };

                code.Insert(lift[0] + 1, new CodeInstruction(OpCodes.Ldc_R4, dockK));
                code.Insert(lift[0] + 2, new CodeInstruction(OpCodes.Mul));
                Log.LogInfo("运输船进出塔补丁已应用（x" + dockK + "）。");
            }

            if (doFlight)
            {
                // 在进入飞船循环前保存未加倍率的原版系数。分支标签必须落在保存动作之前，
                // 确保性能系数 <= 1、跳过 Mathf.Log 的路径也会保存基准值。
                var basePerfLocal = generator.DeclareLocal(typeof(float));
                int at = perf[0];
                CodeInstruction target = code[at];
                var saveBaseLoad = new CodeInstruction(OpCodes.Ldloc, perfLocal);
                saveBaseLoad.labels.AddRange(target.labels);
                saveBaseLoad.blocks.AddRange(target.blocks);
                target.labels.Clear();
                target.blocks.Clear();
                code.Insert(at, saveBaseLoad);
                code.Insert(at + 1, new CodeInstruction(OpCodes.Stloc, basePerfLocal));

                // 每艘船自己的循环：从巨行星轨道采集器载货返航时，性能系数保持原版；
                // 其他船仍使用配置倍率。这样同一物流塔派出的其他正常航线不会被连带影响。
                int pointerStoreIndex = FindShipDataPointerStore(
                    code, out shipDataRefLocal, out shipDataType);
                if (pointerStoreIndex < 0)
                {
                    // 理论上在上方检查已经排除此情况；留一道保护，避免产生不完整 IL。
                    Log.LogWarning("运输船飞行段补丁：无法重新定位 ShipData 循环，取消飞行段修改。");
                    return code;
                }

                CollectorFlagField = isCollectorField;

                Label useConfiguredMultiplier = generator.DefineLabel();
                Label afterMultiplierSelection = generator.DefineLabel();
                MethodInfo bypassHelper = AccessTools.Method(typeof(DockSpeedPlugin), nameof(ShouldBypassFlightMultiplier));
                if (bypassHelper == null)
                {
                    Log.LogWarning("运输船飞行段补丁：找不到轨道采集器判定辅助方法，取消飞行段修改。请检查源码版本。");
                    return original;
                }

                var configuredMultiplierStart = new CodeInstruction(OpCodes.Ldloc, basePerfLocal);
                configuredMultiplierStart.labels.Add(useConfiguredMultiplier);
                var originalMultiplierStart = new CodeInstruction(OpCodes.Ldloc, basePerfLocal);
                var afterMultiplierStart = new CodeInstruction(OpCodes.Ldarg, (short)3); // shipSailSpeed
                afterMultiplierStart.labels.Add(afterMultiplierSelection);
                var injected = new List<CodeInstruction>
                {
                    // 读取当前运输船的远端目标站。otherGId 无效时走普通倍率路径。
                    new CodeInstruction(OpCodes.Ldloc, shipDataRefLocal),
                    new CodeInstruction(OpCodes.Ldfld, otherGIdField),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Ble, useConfiguredMultiplier),

                    new CodeInstruction(OpCodes.Ldloc, shipDataRefLocal),
                    new CodeInstruction(OpCodes.Ldfld, otherGIdField),
                    new CodeInstruction(OpCodes.Ldarg, (short)6), // gStationPool
                    new CodeInstruction(OpCodes.Ldlen),
                    new CodeInstruction(OpCodes.Conv_I4),
                    new CodeInstruction(OpCodes.Bge, useConfiguredMultiplier),

                    // helper(remoteStation, otherGId, direction, itemCount)；
                    // 只要远端目标站是 collector，就整条关联航线豁免，避免严格状态条件漏判。
                    new CodeInstruction(OpCodes.Ldarg, (short)6),
                    new CodeInstruction(OpCodes.Ldloc, shipDataRefLocal),
                    new CodeInstruction(OpCodes.Ldfld, otherGIdField),
                    new CodeInstruction(OpCodes.Ldelem_Ref),
                    new CodeInstruction(OpCodes.Ldloc, shipDataRefLocal),
                    new CodeInstruction(OpCodes.Ldfld, otherGIdField),
                    new CodeInstruction(OpCodes.Ldloc, shipDataRefLocal),
                    new CodeInstruction(OpCodes.Ldfld, directionField),
                    new CodeInstruction(OpCodes.Ldloc, shipDataRefLocal),
                    new CodeInstruction(OpCodes.Ldfld, itemCountField),
                    new CodeInstruction(OpCodes.Call, bypassHelper),
                    new CodeInstruction(OpCodes.Brfalse, useConfiguredMultiplier),

                    // 命中 collector 路线：原版性能系数 × 1。
                    originalMultiplierStart,
                    new CodeInstruction(OpCodes.Stloc, perfLocal),
                    new CodeInstruction(OpCodes.Br, afterMultiplierSelection),

                    // 其他路线：原版性能系数 × 配置倍率。
                    configuredMultiplierStart,
                    new CodeInstruction(OpCodes.Ldc_R4, flightK),
                    new CodeInstruction(OpCodes.Mul),
                    new CodeInstruction(OpCodes.Stloc, perfLocal),

                    // 原版还预先计算了加速度系数 num15；按当前船的有效系数同步重算。
                    afterMultiplierStart,
                    new CodeInstruction(OpCodes.Ldc_R4, SHIP_FLIGHT_ACCEL),
                    new CodeInstruction(OpCodes.Mul),
                    new CodeInstruction(OpCodes.Ldloc, perfLocal),
                    new CodeInstruction(OpCodes.Mul),
                    new CodeInstruction(OpCodes.Stloc, accelerationLocal)
                };

                // 在 ptr2 的 ref 局部变量初始化后执行。把原有跳转标签移到检测代码起点，避免绕过选择逻辑。
                int insertAt = pointerStoreIndex + 1;
                if (insertAt < code.Count && code[insertAt].labels.Count > 0)
                {
                    injected[0].labels.AddRange(code[insertAt].labels);
                    code[insertAt].labels.Clear();
                }
                code.InsertRange(insertAt, injected);

                Log.LogInfo("运输船飞行段补丁已应用（普通航线 x" + flightK
                    + "；轨道采集器关联航线往返均自动豁免；运行时识别状态将各记录一次）。");
            }

            return code;
        }
    }
}
