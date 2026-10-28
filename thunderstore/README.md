# DockSpeed / 物流进出站加速

加快 **物流运输机（行星内）** 与 **星际物流运输船** 的以下阶段：

- 物流运输机：起飞段、降落段
- 星际物流运输船：进出物流塔（停靠台、起降）
- 星际物流运输船：出塔后到进入曲速前、退出曲速后到入塔前的起飞加速、对准、减速、降落

**不改变** 飞行速度和曲速速度（照常受科技影响）、装卸数量、派遣间隔，不会瞬间送达。

## 配置
`BepInEx/config/sinthome358.dsp.dockspeed.cfg`，修改后重启游戏生效。

| 选项 | 默认 | 范围 | 说明 |
| --- | --- | --- | --- |
| `DroneDockSpeedMultiplier` | 4 | 1–20 | 运输机起降倍率 |
| `ShipDockSpeedMultiplier` | 4 | 1–20 | 运输船进出塔倍率 |
| `ShipFlightPhaseMultiplier` | 4 | 1–10 | 运输船曲速前后飞行段倍率 |

1 = 原版。

---

# DockSpeed / Logistics Docking Speed-up

Speeds up the docking / lift-off / landing phases of **logistics drones** and **interstellar logistics vessels**, including the slow acceleration and approach phases before and after warp.

**Unchanged:** Sail speed, warp speed (still affected by tech upgrades), loading amounts, and dispatch logic. Nothing is delivered instantly.

## Configuration

Config: `BepInEx/config/sinthome358.dsp.dockspeed.cfg` (restart required).

| Option | Default | Range | Description |
| --- | --- | --- | --- |
| `DroneDockSpeedMultiplier` | 4 | 1–20 | Docking/lift-off speed multiplier for logistics drones |
| `ShipDockSpeedMultiplier` | 4 | 1–20 | Docking/lift-off speed multiplier for interstellar logistics vessels |
| `ShipFlightPhaseMultiplier` | 4 | 1–10 | Speed multiplier for pre/post-warp flight phases |

`1` = vanilla.

---

源码与问题反馈：https://github.com/Lesinthome0/DockSpeed
