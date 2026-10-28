# DockSpeed 1.1.2：轨道采集器航线豁免与运行时诊断

## 背景

1.1.1 的“补丁已应用”只表示 Harmony 成功安装了补丁，并不能证明运行时已经进入轨道采集器航线的豁免分支。原先的判断同时要求 `direction < 0`、`itemCount > 0` 和 `isCollector`，条件过严时会漏掉靠站或转向阶段。

## 1.1.2 的行为

- 不再依赖 `direction` 和 `itemCount` 才决定豁免。运输船的 `gStationPool[otherGId].isCollector` 为 `true` 时，该航线去程和返程的飞行性能系数都使用原版值。
- 其他航线仍然使用 `ShipFlightPhaseMultiplier`。
- 识别到轨道采集器目标站时，去程和返程各输出一次诊断日志。
- 如果出现“载货返航候选”，但目标站 `isCollector=false`，会输出警告。
- `ShipDockSpeedMultiplier` 和运输机倍率逻辑不变。

## 日志排查

启动日志应包含：

```text
运输船飞行段补丁已应用（普通航线 x...；轨道采集器关联航线往返均自动豁免；运行时识别状态将各记录一次）。
```

飞船实际执行相关路线后，日志中应出现类似：

```text
轨道采集器航线识别成功：otherGId=...，方向=-1（返航方向），itemCount=...；该航线当前运输船的 ShipFlightPhaseMultiplier 已豁免（有效倍率 x1）。
```

如果出现：

```text
返航候选未识别为轨道采集器 ... isCollector=false
```

说明目标站在当前游戏状态或模组组合下没有被标记为 collector，需要结合完整日志和实际目标站对象继续排查。

如果前往 collector 和返航时完全没有运行时日志，请确认实际加载的 DLL 来自 1.1.2 源码，并且运行路径经过 `InternalTickRemote`。

## 验证建议

1. 使用 Visual Studio 打开 `DockSpeed.sln`，选择 Release 编译。
2. 备份旧 `DockSpeed.dll`，将新 DLL 放入实际使用的 BepInEx profile 的 `BepInEx/plugins`。
3. 重启游戏，复现原来的轨道采集器航线问题。
4. 提交包含“轨道采集器航线识别成功”或“返航候选未识别”的完整 BepInEx 日志片段。
