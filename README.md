# DockSpeed / 物流进出站加速

DockSpeed 是一个《戴森球计划》BepInEx 模组，用于加快物流运输机和星际物流运输船进出物流塔、起降以及曲速前后飞行段的速度。

[Thunderstore 页面](https://thunderstore.io/c/dyson-sphere-program/p/sinthome/DockSpeed/)

## 功能

- 加快物流运输机的起飞段和降落段。
- 加快星际物流运输船进出物流塔的停靠台和起降阶段。
- 加快运输船出塔后至进入曲速前，以及退出曲速后至入塔前的飞行阶段。
- 不改变飞行速度、曲速速度、装卸数量、派遣逻辑或塔内库存。
- 不依赖 CommonAPI 或 LDBTool，不新增物品；可以随时安装或卸载，不影响存档。

## 安装

只需要 BepInEx 5。

### 使用 Thunderstore

在 Thunderstore 或 r2modman 中安装 `sinthome-DockSpeed`，并确保同时安装依赖的 BepInEx 5。

### 手动安装

1. 安装 BepInEx 5。
2. 将 `DockSpeed.dll` 放入游戏的 `BepInEx/plugins` 文件夹。
3. 启动游戏一次，配置文件会自动生成。

## 配置

配置文件：`BepInEx/config/sinthome358.dsp.dockspeed.cfg`

修改配置后需要重启游戏。

| 选项 | 默认 | 范围 | 说明 |
| --- | --- | --- | --- |
| `DroneDockSpeedMultiplier` | 4 | 1–20 | 物流运输机起降倍率 |
| `ShipDockSpeedMultiplier` | 4 | 1–20 | 星际物流运输船进出塔倍率 |
| `ShipFlightPhaseMultiplier` | 4 | 1–10 | 运输船曲速前后飞行段倍率 |

`1` 表示保持原版行为。轨道采集器关联航线的飞行段会自动豁免 `ShipFlightPhaseMultiplier`，避免高引擎科技下出现曲速切换异常。

## 构建

要求：Windows、Visual Studio 或可用的 MSBuild、.NET Framework 4.7.2、BepInEx 5.4.17 包。

1. 将 `DefaultPath.props.example` 复制为 `DefaultPath.props`。
2. 修改其中的 `BepinExPath` 和 `DSPAssemblyPath`，指向实际使用的 BepInEx 配置档和游戏 Managed 文件夹。
3. 用 Visual Studio 打开 `DockSpeed.sln`，选择 `Release` 编译。
4. 编译结果位于 `release/plugins/DockSpeed.dll`。
5. 在 PowerShell 中运行 `make_package.ps1`，生成雷电上传用的 ZIP。

`DefaultPath.props` 包含本机路径，已被 `.gitignore` 排除，不应提交到仓库。

## 项目结构

```text
src/                        插件源码
thunderstore/               Thunderstore 清单、README、图标
docs/                       开发和排查文档
DockSpeed.csproj            项目文件
DockSpeed.sln               Visual Studio 解决方案
DefaultPath.props.example   本机路径模板
make_package.ps1            生成 Thunderstore ZIP
```

## 已知限制

- 联机 Nebula 环境尚未测试。
- `ShipFlightPhaseMultiplier` 设置过大时，普通航线的转向轨迹可能变得不自然，建议逐步调高。
- 游戏更新后如果日志提示补丁匹配数异常，请先禁用模组并提交日志。

## 许可

本项目采用 MIT License，详见 `LICENSE`。
