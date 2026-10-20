# Kitchen Final

俯视角 3D 厨房自动化 Demo（Unity 2022.3.62f3c1，Built-in Render Pipeline）。

参考《王之凝视》的"厨具即建筑、员工激活厨具"玩法：你扮演厨师长，在只有三名员工的厨房里实时安排员工站位与厨具，完成订单。

A top-down 3D kitchen automation demo. Chefs operate one assigned cooker from its front work cell.
Heating runs unattended after preparation. A chef facing the pan stops the heat and collects the food when idle.

---

## 快速开始 / Quick start

1. 用 **Unity 2022.3.62f3c1** 打开本工程。
2. 打开场景 `Assets/_Game/Scenes/MainMenu.unity`（开始界面）。
3. 按 **Play**，选择 **教程**（三关入门）或 **经典模式**（每日营业）。主菜单采用左右分区的 KITCHEN CLUB 标题与白色餐牌；设置可调整全局音量、厨房 HUD 缩放和界面弹性动效，并自动保存。

也可以直接打开 `Assets/_Game/Scenes/Kitchen.unity` 按 Play，此时会从当前关卡（默认第一关）开始。

更新界面样式或修复 HUD 文字时，先停止 Play 并保存场景，再执行
**`Kitchen/UI/Apply Paper UI (UI Only)`**。它只更新厨房与主菜单的 Canvas 和 UI 绑定，保留玩法、美术及关卡资产。
首次缺少 TMP 基础资源时，先执行 **`Kitchen/6. Import TMP Essential Resources`**。

只更新主菜单时执行 **`Kitchen/UI/Apply Main Menu (Menu Only)`**，不会更新厨房场景。菜单使用项目内的 Lilita One 与 Noto Sans SC Bold 字体，均附 OFL 许可，见 `Assets/ThirdParty/GoogleFonts/README.md`。菜单窄屏自动上下排列；面包回形针可点击互动。

只更新局内界面时执行 **`Kitchen/UI/Apply Kitchen HUD (HUD Only)`**。局内按钮采用纯色圆角：蓝色操作、绿色确认/上菜、灰色次级、珊瑚色出售/退出、淡紫色技能。所有可见操作按钮（包括运行时轮盘、并行订单、每日简报和结算）统一提供悬停提亮与抬升、按压变深与压缩、松手回弹和移出复位；不可用时置灰。关闭弹性动效后保留颜色反馈。白色面板与黄色标题块继续保留。

鼠标悬停按钮时轻微抬升、放大并变色，悬停面包回形针时轻转；移出自动复位。点击教程 / 经典模式先显示简短说明和三条操作提示，再点击 **开始教程 / 开始营业** 才进入游戏。返回箭头或 Esc 回到菜单，不重置当前进度。关闭弹性动效后仍保留按钮颜色反馈。

> Open `Assets/_Game/Scenes/MainMenu.unity` and press Play, then choose Tutorials or Classic mode.
> Opening `Kitchen.unity` directly also works and starts at the current campaign level.
> To refresh the HUD, stop Play, save scene edits, and run `Kitchen/UI/Apply Paper UI (UI Only)`.
> Import `Kitchen/6. Import TMP Essential Resources` first if the TMP essential resources are missing.

---

## 经典模式 / Classic

- 经典模式使用独立配置 `Assets/_Game/Resources/Kitchen/Level_Classic.asset`，可以从主菜单直接开始，也可以完成第三个教程后进入。
- 每天分为 **营业简报与备战 → 限时营业 → 结算与技能三选一**。默认每天 180 秒，只有点击开门才开始计时和生产；简报可收起，免费调整已有设备朝向/位置，也可花钱购买设备。
- 两张订单持续补入，超时顾客离开并换新单；按订单奖励累计当日分数，营业结束后立即停止生产和交付，列出上菜、超时、未上菜、焦糊、当日/累计/最高分。
- 第一天两名员工、两只盘子、空厨房和 400 金币；全部设备由玩家购买摆放，四类基础设备各买一台共 310 金币。基础原料无限，半成品/成品需要加工，盘子仍要回收清洗。
- 经典模式同类设备每成功购买一台，后续价格增加原价的 25%，总价向上取整：切菜台依次 50、63、75、88 金币。不同类型分别计数，取消或放置失败不涨价，拆除不降价；购买次数跨天保留，新一轮重置。教程仍使用固定价格。
- 每日三选一技能：人工加工加速、移动加速、奖励加成、额外盘子、过火宽限、招聘。技能从下一天生效，可叠加；招聘最多两次。
- 设备布局、朝向、配方偏好、金币和技能跨天保留。每天开始时清空食品与未完成批次，盘子归还并洗净；不会继承未完成订单。
- 分数与建造资金分开记录，购物不扣得分；参考目标不设失败门槛，后续天数逐步缩短订单耐心时间。当前进度只在本次游戏运行中保留，返回菜单结束本轮，退出游戏不存档。
- 具体规则、技能数值与调整入口见 `经典模式与教程关卡设计.md`。无需生成场景；每日内容运行时加载，保留已有厨房与美术。

## 教程流程 / Tutorials

| 关卡 | 菜品 | 可用厨具 | 员工 | 初始金钱 | 通关奖励 |
|---|---|---|---|---|---|
| 第一关 · 沙拉 | 沙拉 | 切菜台、组装台 | 1 | 170（正好买一台切菜台 + 一台组装台） | 员工 +1、煎锅 |
| 第二关 · 沙拉与汉堡 | 沙拉、生菜番茄肉面包 | 切菜台、煎锅、组装台 | 2 | 250 | 员工 +1、洗碗台 |
| 第三关 · 盘子循环 | 沙拉、生菜番茄肉面包 | 全部四台 | 3 | 300 | —（最后一关） |

- 三关分别教学站位与沙拉、煎锅与并行订单、盘子循环与员工调度；开场展示操作简报，点击开始练习后计时。
- 每关有**固定订单清单**；第一关同时一张，后两关同时两张，独立倒计时，可以选择任意一张先上菜；完成一张后补入下一张，全部完成才通关。第三关初始净盘为两只，需要清洗才能完成三次交付。
- LevelConfig 的 Max Concurrent Orders 可设为 1 或 2；订单 UI 在运行时替换面板内部内容，无需重建场景。
- **订单超时只重发对应一张**，另一张的计时、库存和加工进度保留；食物烤糊仍会损失原料，资源耗尽时可重新开始关卡。
- 关卡的初始金钱、库存、员工数、厨具目录、订单清单都是 `Assets/_Game/Resources/Kitchen/Level_0N.asset` 的数据，可以直接在 Inspector 改。
- 每关的通关奖励记录在 `LevelConfig` 的 `Clear Reward` 里，用于奖励面板显示；下一关的实际内容由它自己的配置决定，避免两处重复计算。

---

## 操作 / Controls

| 操作 | 说明 |
|---|---|
| 左键点击厨师 | 选中，用 WASD 走到设备正面的操作格；行走中不能操作 |
| 左键点击厨具 | 查看面板与操作格；已有选中员工且站位正确时，为其指定任务 |
| 右键点击厨具 | 打开加工轮盘，选择具体配方或自动；当前批次完成后应用新选择 |
| 商店按钮 / 数字键 1-4 | 选择要购买的厨具，再左键点空地放置 |
| R | 建造时顺时针旋转预览；普通或移动模式下旋转当前选中的厨具 |
| Esc / 右键 | 取消建造或移动 |
| 移动厨具按钮 | 进入移动模式，WASD 或点击相邻格完成移动 |
| 每张订单的上菜按钮 | 交付对应订单，可先完成第二张（需要成品 + 干净盘子） |

加工进度显示在机器上方的黄色圆环中。煎锅正常圆环绕满一圈后开始累积红色过火环；红色满圈即烤糊。关火后显示绿色完成环。轮盘打开时阻止厨房操作；点击空白、右键或 Esc 关闭。今日订单上的面包曲别针采用透明 PNG，点击可以晃动、眨眼和显示短句。

---

## 玩法规则 / Rules

- 一名员工占一格，一台厨具占一格；切菜台、煎锅、组装台和洗碗台都有四向朝向，只接受正面相邻操作格的员工，侧面、背面和斜角均无效。
- 多台设备可以共用一个操作格；一名员工可不移动地切换任务，但无法让多人在同一格并行工作。操作格允许正常通行，不是设备额外占用的格子。
- 建造预览和选中设备时显示正面箭头与操作格：绿色可用，黄色有人，红色被设备挡住或越界。选中面板显示方向、操作格坐标及共用设备数量。
- 建造拒绝正面越界或被厨具挡住的方案，不扣钱。移动、旋转允许暂时形成受阻布局，以便继续调整；这期间无法从受阻操作格工作。提示只检查操作格本身，不判断整条行走路线是否连通。
- 旋转不收费，释放该设备的员工任务，保留人工加工进度。预置设备先采用关卡里的 Facing，正面受阻时启动阶段顺时针寻找可用方向，以兼容旧关卡。
- 到位后点击设备明确指定任务，同一员工只能操作一台设备，同一设备只接受一名员工。切菜、组装、洗碗在离岗或换任务时暂停，保留进度。
- 切菜台每 0.3 秒切一次、共 10 次；煎锅自动加热 5 秒；组装台 10 秒；洗碗台 5 秒一只盘子。
- 煎锅默认需要 0.75 秒装料，随后释放员工并自动加热。完成时或完成后，只要已到位员工站在操作格、面前一格覆盖锅，就自动关火，无需再次点击；WASD 可转向被设备阻挡的相邻格，不会走进设备。
- 关火后成品安全留在锅里，离人也不再增加过火时间。面朝锅的空闲员工自动花 0.5 秒取出入库；不会抢走员工的其他主动任务，也不会自动启动下一批。未被照看的锅默认 10 秒红环满后烤糊，需明确指派员工花 1.5 秒清理。
- 加热设备有进行中的批次、待取成品或焦糊时，不能移动、旋转或拆除；完成处理后可以重新布置。
- 这些加热操作时间可在 CookerDefinition 的 Inspector 调整；没有新增物品 ID 或配方。教程保留有限库存，经典模式基础原料无限；物流尚未加入。
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
├── WorkManager        统一检查邻格任务并推进主动操作、自动加热与焦糊风险
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
| [工程说明.md](Assets/_Game/Art/工程说明.md) | **工程说明**：全部运行逻辑与实现思路、架构决策、不变量、扩展指南 |
| [说明书_UI_数值_Prefab.md](Assets/_Game/Art/说明书_UI_数值_Prefab.md) | **操作手册**：怎么改 UI、数值、Prefab |
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
