# 厨房 Demo 调整说明书

本说明书覆盖三件事：**调 UI**、**调数值**、**调 Prefab（美术资源）**。

> 本文档路径：`Assets/_Game/Art/说明书_UI_数值_Prefab.md`
> 同目录的 `美术资源替换指南.md` 是更偏"接口清单"的版本，内容互补。

---

## 0. 为什么 Prefabs 文件夹以前是空的？

因为**第一版根本没有生成 prefab 资产**。运行时的模型是这样来的：

```
GameRuntime.Boot()
   └── CookerManager / StaffManager
          └── ViewFactory.CreateCookerView(definition)
                 └── definition.ViewPrefab == null ?  →  用图元现场拼一个白模
                                                    →  否则实例化你的 prefab
```

也就是说：**`ViewPrefab` 字段是空的，所以代码走了"现场拼图元"那条路**。Prefabs 文件夹只是建了空目录，没有任何 `.prefab` 文件。
结果是：
- Game 视图里有模型（运行时拼出来的）；
- Scene 视图里什么都没有（那时连预览都还没做）；
- Prefabs 文件夹是空的。

现在这一版已经补上：**生成器会真的创建 prefab 资产，并把它写进对应的配置里**。所以：

| | 现在 |
|---|---|
| `Prefabs/Cookers/` | 4 个厨具 prefab |
| `Prefabs/Staff/` | 1 个员工 prefab |
| `Prefabs/Grid/` | 地板 prefab、网格格 prefab |
| `Prefabs/Ingredients/` | 每种食材/成品/盘子一个 prefab |
| Scene 视图 | 实例化这些 prefab，**保持 prefab 连接**（蓝色标记） |
| 运行时 | 同样实例化这些 prefab |

于是**改 prefab → Scene 立刻变，Play 也立刻变**，不需要重新生成任何东西。

---

## 1. 调 UI

UI 是一个**纯代码生成**的 Canvas，全部集中在 `Assets/_Game/Editor/KitchenUIBuilder.cs`。

### 1.1 改配色

文件顶部常量：

```csharp
private static readonly Color PanelColor        = new Color(0.07f, 0.08f, 0.11f, 0.93f);  // 面板底色
private static readonly Color CardColor         = new Color(0.13f, 0.15f, 0.19f, 0.95f);  // 分组底框
private static readonly Color ButtonColor       = new Color(0.20f, 0.33f, 0.50f, 1f);     // 普通按钮
private static readonly Color ButtonColorServe  = new Color(0.18f, 0.46f, 0.26f, 1f);     // 上菜按钮
private static readonly Color ButtonColorNeutral= new Color(0.28f, 0.30f, 0.36f, 1f);     // 中性按钮
private static readonly Color ButtonColorDanger = new Color(0.46f, 0.21f, 0.21f, 1f);     // 拆除按钮
private static readonly Color TextColor         = new Color(0.93f, 0.95f, 0.98f, 1f);     // 主文字
private static readonly Color TextMuted         = new Color(0.70f, 0.74f, 0.82f, 1f);     // 次要文字
private static readonly Color TextMoney         = new Color(0.65f, 0.95f, 0.70f);        // 金钱
private static readonly Color ProgressFillColor = new Color(0.36f, 0.78f, 0.48f, 1f);     // 进度条
```

### 1.2 改字号

```csharp
private const float FontTitle = 30f;   // 面板标题
private const float FontBody  = 24f;   // 正文
private const float FontSmall = 20f;   // 提示小字
private const float FontHuge  = 40f;   // 金钱
```

### 1.3 改位置 / 大小

每个面板一个方法，用**锚点 + 像素偏移**定位。例如订单面板：

```csharp
private static void BuildOrderPanel(Transform canvas)
{
    RectTransform panel = Panel(canvas, "OrderPanel",
        new Vector2(1f, 1f),        // anchorMin：右上角
        new Vector2(1f, 1f),        // anchorMax：右上角（不拉伸）
        new Vector2(-24f, -120f),   // 距右上角的偏移
        PanelColor).GetComponent<RectTransform>();
    panel.pivot = new Vector2(1f, 1f);
    panel.sizeDelta = new Vector2(680f, 430f);   // 面板尺寸
    ...
}
```

画布参考分辨率在 `CanvasWidth / CanvasHeight`（默认 1920×1080），
`CanvasScaler` 用 `MatchWidthOrHeight = 0.5`，所以任意窗口比例都等比缩放。

**注意**：面板里的文字用 `VerticalLayoutGroup` / `HorizontalLayoutGroup` 自动排布，
所以**不要**给文字写死坐标，否则又会重叠。改文字长度是安全的。

### 1.4 改文案

文案在 `Assets/_Game/Scripts/UI/UIManager.cs` 里（运行时刷新），初始文案在 `KitchenUIBuilder.cs` 里。
两处都要改，否则第一帧会显示旧文字然后被覆盖。

### 1.5 改完怎么生效

菜单 **`Kitchen/3. Generate Everything`** 重新生成场景即可。
（UI 不是 prefab，所以不能在 Scene 里可视化改；它是代码生成的。）

### 1.6 加一个图标

`ItemDefinition` 上有 `Icon` 字段（`Sprite`），已经预留但没有被 HUD 使用。
要用图标化 HUD：在 `UIManager.RefreshTopBar()` 里把 `inventoryText.text` 换成一组 `Image`
（`Image.sprite = item.Icon`）。

---

## 2. 调数值

### 2.1 数值都在 ScriptableObject 里

| 想改什么 | 文件 | 字段 |
|---|---|---|
| 初始资金 | `Data/Levels/Level_01.asset` | `Starting Money` |
| 初始库存 | 同上 | `Starting Inventory`（列表） |
| 地图尺寸 / 格子大小 | 同上 | `Width` `Height` `Cell Size` |
| 地图中心 / 地面高度 | 同上 | `Map Center` `Ground Height` |
| 员工人数 | 同上 | `Staff Count` |
| 员工初始位置 | 同上 | `Staff Spawn Offsets`（相对中心格） |
| 开局厨具摆放 | 同上 | `Starting Cookers`（厨具 + 格子坐标） |
| 订单顺序 | 同上 | `Order Sequence` |
| 厨具价格 | `Data/Cookers/Cooker_*.asset` | `Price` |
| 煎锅/组装/洗碗耗时 | 同上 | `Duration`（秒） |
| 切菜台节奏 | 同上 | `Tick Interval`（0.3）+ `Required Ticks`（10） |
| 加工类型 | 同上 | `Work Type`（Chopping / Timed / Assembly / Washing） |
| 配方材料与产出 | `Data/Recipes/Recipe_*.asset` | `Inputs`（材料+数量）`Outputs` |
| 配方优先匹配 | 同上 | `Priority`（大者先匹配） |
| 订单要求与奖励 | `Data/Orders/Order_*.asset` | `Dish` `Dish Count` `Clean Plates Required` `Reward` `Time Limit` |
| 物品名称/颜色/大小 | `Data/Ingredients/Item_*.asset` | `Display Name` `View Color` `View Scale` |

### 2.2 示例：把沙拉订单改成 3 份、奖励 500、限时 3 分钟

打开 `Data/Orders/Order_order_salad.asset`：

```
Dish Count            : 3
Clean Plates Required : 3      ← 一般跟份数一致
Reward                : 500
Time Limit            : 180
```

保存后**直接 Play 就生效**，不用重新生成场景（运行时读取资产）。

### 2.3 示例：把切菜改成 1 秒一次、共 3 次

打开 `Data/Cookers/Cooker_chopping_station.asset`：

```
Tick Interval  : 1
Required Ticks : 3
```

### 2.4 示例：改地图为 7×7

打开 `Data/Levels/Level_01.asset`，把 `Width` / `Height` 改成 7。
**注意**：相机的取景距离是按地图尺寸算的，改完地图后要重新生成场景
（菜单 `Kitchen/3. Generate Everything`），否则相机会按旧尺寸取景。
`Starting Cookers` 里如果有超出 7×7 的坐标会被跳过并打印警告。

### 2.5 需要重新生成 vs 不需要

| 改动 | 是否要重新生成场景 |
|---|---|
| 订单、配方、价格、耗时、物品外观 | **不用**，直接 Play |
| 初始库存、初始资金、员工人数 | **不用**，直接 Play |
| 地图尺寸、格子大小、地图中心 | **要**（相机取景 + 场景预览格子数） |
| 厨具摆放位置 | **要**（Scene 预览位置） |
| UI 布局与配色 | **要** |

---

## 3. 调 Prefab（美术资源）

### 3.1 每个 prefab 是什么

| Prefab | 作用 |
|---|---|
| `Prefabs/Cookers/Cooker_chopping_station.prefab` | 切菜台外观 |
| `Prefabs/Cookers/Cooker_frying_pan.prefab` | 煎锅 |
| `Prefabs/Cookers/Cooker_assembly_table.prefab` | 组装台 |
| `Prefabs/Cookers/Cooker_dish_washer.prefab` | 洗碗台 |
| `Prefabs/Staff/Staff_Chef.prefab` | 员工 |
| `Prefabs/Grid/Grid_Floor.prefab` | 地板 |
| `Prefabs/Grid/Grid_Cell.prefab` | 单个网格格（会被复制 25 份） |
| `Prefabs/Ingredients/Item_*.prefab` | 食材/成品/盘子的世界模型 |

### 3.2 两种改法

**A. 直接改现有 prefab（最省事）**

1. 在 Project 里双击 `Cooker_frying_pan.prefab`，进入 Prefab 编辑模式。
2. 把子物体换成你的模型，或改材质。
3. 退出。**Scene 视图和 Play 立即同步**（prefab 连接还在）。

**B. 换成你自己的模型（推荐用于正式美术）**

1. 把你的模型做成 prefab，放进 `Prefabs/Cookers/`。
2. 打开 `Data/Cookers/Cooker_frying_pan.asset`，把 `View Prefab` 指向你的 prefab。
3. 完成。运行时和 Scene 预览都会用你的模型。

> 两种方式可以混用。字段里有值就用那个 prefab，没值就用现在这个生成的白模。

### 3.3 每个 view 上可调的字段

选中 prefab 后，在 Inspector 上能看到对应组件：

**`CookerView`（厨具）**

| 字段 | 作用 |
|---|---|
| `Model Prefab` | 再嵌一层模型 prefab（想保持外层结构时用） |
| `Body Material` / `Accent Material` | 主体 / 强调色材质 |
| `Body Color` / `Accent Color` / `Inactive Accent Color` | 无材质时的纯色；强调色同时表示"是否激活" |
| `Body Size` | 白模尺寸。**不影响格子占用**（永远 1 格） |

**`StaffView`（员工）**

| 字段 | 作用 |
|---|---|
| `Model Prefab` | 你的角色模型 |
| `Body Material` / `Head Material` / `Selection Material` | 材质 |
| `Selection Color` | 脚下选中圈颜色 |
| `Body Height` / `Body Radius` | 白模尺寸与碰撞体 |
| — | 会自动找子物体的 `Animator`，并在选中时设 `SetBool("Selected", …)` |

**`GridView`（地板与网格，挂在场景的 `World/GridRoot/Preview/GridView` 上）**

| 字段 | 作用 |
|---|---|
| `Floor Prefab` | 地板 prefab |
| `Cell Prefab` | 单格 decal prefab |
| `Floor Material` / `Cell Material` | 材质 |
| `Valid/Invalid Highlight Material` 与颜色 | 建造时的高亮 |

**`PlacementPreview`（建造虚影）**

| 字段 | 作用 |
|---|---|
| `Ghost Prefab` | 虚影模型 |
| `Valid Material` / `Invalid Material` | 合法 / 非法材质 |

### 3.4 三条不能碰的边界

1. **模型缩放不决定格子占用。** 厨具碰撞盒和 `GridData` 占用固定 1 格
   （`CookerView.MaxFootprint`）。模型做大只会视觉上压到邻格。
2. **动画不驱动加工规则。** 进度存在 `CookerRuntime`，由 `WorkManager` 统一推进；动画只是表现。
3. **美术 prefab 不管理订单与库存。** 库存唯一入口是 `InventoryManager`。
   prefab 上不要挂任何游戏逻辑脚本。

### 3.5 换成有骨骼动画的角色

1. 角色 prefab 根节点挂 `StaffView`，模型作为子物体，子物体有 `Animator`。
2. 在 `Assets/_Game/Scripts/View/StaffView.cs` 里加：

```csharp
public void SetMoving(bool moving)
{
    if (animator != null) animator.SetBool("Moving", moving);
}
```

3. 在 `StaffManager.Tick()` 里移动块中调用 `runtime.View?.SetMoving(true)`，
   到达格子后调用 `SetMoving(false)`。
   **改动只发生在视图层，游戏逻辑不受影响。**

### 3.6 换成你自己的 UI 皮肤

UI 目前是代码生成。如果想用美术做的 UI：

1. 做一个 Canvas prefab，按现有层级命名子物体
   （`MoneyText` `ModeText` `TimeText` `OrderPanel` `SubmitOrderButton` `CookerButton0..3` …）。
2. 在 `KitchenUIBuilder.BuildCanvas()` 里改成 `Instantiate(yourCanvasPrefab)`。
3. `WireUI()` 靠**名字**查找子物体，所以只要命名一致就能自动接上，不用改绑定代码。

---

## 4. 面板速查：我该去哪里改？

```
想改的东西
├── 界面（颜色/字号/位置/文案）
│     └── Assets/_Game/Editor/KitchenUIBuilder.cs   （+ Scripts/UI/UIManager.cs 的文案）
├── 数值（钱/时间/价格/配方/订单/库存/地图）
│     └── Assets/_Game/Data/**.asset                （Inspector 里直接改）
└── 模型（厨具/员工/地板/食材）
      ├── Assets/_Game/Prefabs/**.prefab            （直接改 prefab）
      └── Assets/_Game/Data/**/**.asset 的 View Prefab（换成自己的 prefab）
```

## 5. 改完怎么验证

| 菜单项 | 作用 |
|---|---|
| `Kitchen/1. Generate Data Assets` | 只重新生成 ScriptableObject 配置 |
| `Kitchen/2. Generate Kitchen Scene` | 只重新生成场景 |
| `Kitchen/3. Generate Everything` | 配置 + prefab + 场景 全量重建（**推荐**） |
| `Kitchen/4. Open Kitchen Scene` | 打开 `Kitchen.unity` |
| `Kitchen/5. Verify Acceptance Criteria` | 跑 78 项自动验收，写 `Logs/acceptance_report.json` |
| `Kitchen/6. Import TMP Essential Resources` | 缺 TextMeshPro 资源时用 |

验收报告里和美术相关的检查：

| 检查项 | 含义 |
|---|---|
| `art_prefab_assets_exist` | 所有 prefab 文件都在 |
| `art_prefabs_are_assigned` | 配置里的 `View Prefab` 都指到了 prefab |
| `runtime_views_come_from_prefabs` | 运行时确实是 prefab 实例（不是白模回退） |
| `scene_has_visible_kitchen` | Scene 里有网格/厨具/员工/灯光/相机 |
| `models_fit_their_cell` | 厨具没超出自己那一格 |
| `ui_no_panel_overlap` / `ui_text_fits` | UI 不重叠、文字不溢出 |

如果你把模型刻意做成跨格，`models_fit_their_cell` 会失败 —— 那说明需要同时改
`CookerView.MaxFootprint` 与网格占用逻辑，属于玩法改动，不在美术替换范围内。
