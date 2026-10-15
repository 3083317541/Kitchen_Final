# Kitchen Final

俯视角 3D 厨房自动化 Demo（Unity 2022.3.62f3c1，Built-in Render Pipeline）。

参考《王之凝视》的"厨具即建筑、员工激活厨具"玩法：你扮演厨师长，在只有三名员工的厨房里实时安排员工站位与厨具，完成订单。

A top-down 3D kitchen automation demo. Cookers act as buildings and chefs activate them by standing
on their cell. All art is placeholder geometry with explicit replacement seams.

---

## 快速开始 / Quick start

1. 用 **Unity 2022.3.62f3c1** 打开本工程。
2. 打开场景 `Assets/_Game/Scenes/Kitchen.unity`。
3. 按 **Play**。

首次打开若 HUD 没有文字，执行菜单 **`Kitchen/6. Import TMP Essential Resources`**，再执行
**`Kitchen/3. Generate Everything`**。生成器会自动从系统字体创建中文 HUD 字体。

> Open the project with Unity 2022.3.62f3c1, open `Assets/_Game/Scenes/Kitchen.unity`, press Play.
> If the HUD has no text on first open, run `Kitchen/6. Import TMP Essential Resources` then
> `Kitchen/3. Generate Everything`.

---

## 操作 / Controls

| 操作 | 说明 |
|---|---|
| 左键点击厨师 | 选中，WASD 走相邻格子（站到厨具格即激活该厨具） |
| 左键点击厨具 | 打开操作面板，查看投入、产出、状态与进度 |
| 商店按钮 / 数字键 1-4 | 选择要购买的厨具，再左键点空地放置 |
| Esc / 右键 | 取消建造或移动 |
| 移动厨具按钮 | 进入移动模式，WASD 或点击相邻格完成移动 |
| 上菜按钮 | 交付当前订单（需要成品 + 干净盘子） |

---

## 玩法规则 / Rules

- 一名员工占一格，一台厨具占一格，**员工可以与厨具共格**（这就是激活方式）。
- 厨具只在有员工站上去时才加工；员工离开则暂停，**已完成进度保留**。
- 切菜台每 0.3 秒切一次、共 10 次；煎锅 5 秒；组装台 10 秒；洗碗台 5 秒一只盘子。
- 组装台的配方**数量必须精确匹配**，多一份都不算。
- 出餐必须同时有成品与干净盘子，成功后生成**待归还盘子任务**，10–20 秒后变成脏盘子，
  洗净后重新变成干净盘子——形成盘子循环。
- 订单超时判失败，奖励不重复结算。

---

## 架构 / Architecture

```
GameRuntime（唯一入口）
├── 初始化所有系统、加载 LevelConfig、创建网格/员工/厨具/库存/订单
├── 接收 InputManager 传来的玩家意图并分派
└── 按固定顺序推进：Work → PlateReturn → Orders → Staff → CookerActivation → UI

Systems（领域模块，均挂在 Systems 对象上）
├── GridManager        网格与占用（Dictionary<Vector2Int, GridData>）
├── CookerManager      购买、放置、合法性、占用、移动、删除
├── StaffManager       创建、选中、离散移动、格子中心平滑插值
├── WorkManager        统一检查"员工是否在厨具格"并推进加工
├── InventoryManager   库存唯一入口（含预定机制，防止材料被重复消费）
├── RecipeManager      配方匹配、输入锁定、完成转换
├── OrderManager       订单生成、交付验证、奖励、超时
└── PlateReturnManager 待归还盘子统一队列（每只盘子没有独立计时器）
```

设计约束（刻意为之）：

- **Manager 不读输入**，输入只在 `InputManager` 里采集，转成意图事件。
- **Manager 不互相查找场景对象**，全部依赖由 `GameRuntime` 显式注入。
- **每个厨具没有独立计时器**，由 `WorkManager` 用同一个循环推进所有厨具。
- **加工规则不在 MonoBehaviour 里**，用 ScriptableObject 策略（`CookerWorkStrategy`）按 `WorkType` 分派。
- **UI 不直接改数据**，只调用 `GameRuntime` 的公开方法。

---

## 目录 / Layout

```
Assets/_Game
├── Scripts
│   ├── Common      枚举、ItemStack
│   ├── Data        ScriptableObject 定义 + 默认内容工厂
│   ├── Grid        GridData、GridManager、KitchenGridMath（编辑器与运行时共用）
│   ├── Cookers     厨具运行时、管理器、四种加工策略
│   ├── Staff       员工管理器        Entities  员工运行时
│   ├── Work        推进循环
│   ├── Inventory   库存
│   ├── Recipes     配方
│   ├── Orders      订单与盘子归还
│   ├── Input       InputManager（唯一读输入的地方）
│   ├── UI          UIManager（唯一碰 UI 的地方）
│   ├── View        CookerView / StaffView / GridView / PlacementPreview / ViewFactory / Whitebox
│   └── Runtime     GameRuntime（入口）与 GameSystem 基类
├── Editor          生成器：数据资产、prefab、场景、UI；以及 78 项自动验收
├── Art             美术替换指南（Markdown）
├── Data            生成出来的 ScriptableObject 配置（可直接在 Inspector 改数值）
├── Prefabs         生成出来的美术 prefab
├── Resources       LevelConfig 与加工策略（供 Resources.Load 回退）
└── Scenes          Kitchen.unity
```

---

## 调整与替换 / Customising

三份文档：

| 文档 | 内容 |
|---|---|
| [说明书_UI_数值_Prefab.md](Assets/_Game/Art/说明书_UI_数值_Prefab.md) | **主说明书**：怎么改 UI、数值、Prefab |
| [美术资源替换指南.md](Assets/_Game/Art/美术资源替换指南.md) | 美术接口逐字段清单 |

要点：

- **数值**都在 `Assets/_Game/Data/**.asset`，Inspector 直接改，改完 Play 即生效。
- **UI** 在 `Assets/_Game/Editor/KitchenUIBuilder.cs`（布局与配色）+ `Scripts/UI/UIManager.cs`（文案刷新）。
- **美术**改 `Assets/_Game/Prefabs/**.prefab`（保留 prefab 连接，Scene 与 Play 同步），
  或把自己的 prefab 赋给 `Data/**/*.asset` 的 `View Prefab` 字段。

### Editor 菜单

| 菜单 | 作用 |
|---|---|
| `Kitchen/1. Generate Data Assets` | 只重建 ScriptableObject 配置 |
| `Kitchen/2. Generate Kitchen Scene` | 只重建场景 |
| `Kitchen/3. Generate Everything` | 配置 + Prefab + 场景 全量重建 |
| `Kitchen/4. Open Kitchen Scene` | 打开 Kitchen 场景 |
| `Kitchen/5. Verify Acceptance Criteria` | 跑自动验收，报告写入 `Logs/acceptance_report.json` |
| `Kitchen/6. Import TMP Essential Resources` | 补齐 TextMeshPro 资源 |

---

## 自动验收 / Verification

`Kitchen/5. Verify Acceptance Criteria` 会驱动**真实运行时**（同样的组件图、同样的
`GameRuntime.Boot` / `AdvanceFrame`）跑一段脚本化的游戏过程，并把每一项的**实测值**写进 JSON。

无图形界面时也可以用批处理执行：

```
Unity.exe -batchmode -nographics -quit ^
  -projectPath "<项目路径>" ^
  -logFile "<项目路径>/Logs/batch_generate.log" ^
  -executeMethod Kitchen.EditorTools.KitchenSceneGenerator.GenerateAndVerify
```

覆盖范围包括：网格对齐（含奇数/偶数尺寸）、员工移动与不重叠、建造扣款与拒绝、
四种厨具的加工时序与暂停恢复、组装台精确匹配、洗碗台循环、订单交付与超时、
盘子 10–20 秒归还、UI 与地面输入的隔离、HUD 面板不重叠与文字不溢出、
中文字形覆盖、相机取景、场景内容与 prefab 绑定。

---

## 依赖 / Dependencies

无第三方插件、无网络服务、无外部资产。仅使用 Unity 内置模块以及
**TextMeshPro** 与 **Unity UI**（随 Unity 官方包管理）。

字体：HUD 使用系统中文字体生成的**动态图集**。源字体文件（微软雅黑）因版权原因
未提交，首次生成时会自动重建，因此克隆后无需手动处理。
