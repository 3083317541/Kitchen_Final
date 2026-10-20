# 项目协作指南

本文件适用于整个仓库。开始工作先阅读本文件及目标目录下的补充指引。默认用中文与用户交流。

## 项目目标

- 非盈利、非商业 Game Jam；用户主要负责美术，喜欢《胡闹厨房》的可爱、圆润、清晰易读的风格，目前不熟悉 Blender。
- 优先使用免费且授权清楚的现成素材，其次在 Unity 内组合或简单修改，确实缺少时再考虑生成与建模。
- 非商业用途不等于获得其他游戏资产的使用许可；借鉴比例、配色与灯光，保留素材来源和许可。
- 美术替换保留现有玩法、物品 ID、配方、经济及关卡配置，避免改动无关系统。

## 环境与入口

- Unity 版本以 ProjectSettings/ProjectVersion.txt 为准，目前为 **2022.3.62f3c1**。
- 使用 **Built-in Render Pipeline**、旧 Input Manager、uGUI、TextMeshPro。不要为素材导入自行升级 Unity、迁移渲染管线或输入系统。
- 开始场景：Assets/_Game/Scenes/MainMenu.unity。
- 游戏场景：Assets/_Game/Scenes/Kitchen.unity。
- 教程配置：Assets/_Game/Resources/Kitchen/Level_01.asset 至 Level_03.asset；经典模式配置：Level_Classic.asset。
- 先读 README.md；详细说明在 Assets/_Game/Art/工程说明.md、说明书_UI_数值_Prefab.md。
- 美术资源替换指南.md 有部分说明可能落后于代码，字段、层级和实际行为以当前脚本为准。

## 目录与架构

- Assets/_Game/Scripts/Data：ScriptableObject 定义。
- Assets/_Game/Scripts/Runtime：GameRuntime 入口与系统调度。
- Assets/_Game/Scripts/View：模型、选择反馈、摆放预览、白盒回退。
- Assets/_Game/Scripts/UI：界面展示。
- Assets/_Game/Editor：生成器、预览与自动验收。
- Assets/_Game/Data：物品、厨具、配方等配置资产。
- Assets/_Game/Prefabs：游戏预制体。
- Assets/_Game/Resources：运行时加载的关卡与策略。
- Assets/_Game/Art：美术文档和资源。

架构约定：

- 输入只由 InputManager 采集并转换为意图，Manager 不自行读取输入。
- 依赖由 GameRuntime 显式注入，避免 Manager 互相查找场景对象。
- 加工由 WorkManager 统一推进，具体规则使用 CookerWorkStrategy，不新增每台厨具的独立计时器。
- UI 调用 GameRuntime 接口，不直接修改库存、订单或厨具数据。
- 厨具有四向朝向，员工只在正面相邻的操作格工作；员工和厨具不共格。多台设备可以共用操作格，但一次只能站一人，员工到位后明确指定一台设备。网格规则负责通行，美术碰撞体不能替代占用判断。
- 建造时按 R 旋转预览，选中或移动厨具时按 R 旋转实物；旋转释放任务并保留人工加工进度。操作格不额外登记占用，避免阻止共享；加热批次未处理完时禁止旋转。
- 切菜、组装、清洗需要持续操作，离开保留进度；加热类由 WorkManager 统一推进装料、自动加热、过火、关火待取、焦糊与清理。员工到达正面操作格且逻辑面前一格覆盖锅时自动关火；空闲时自动取出，不抢其他主动任务，也不自动开下一批。
- 员工朝向由 StaffRuntime.Facing/FrontCell 表示，不从美术 Transform 推断；移动和指派同步逻辑朝向。加热黄环完成后红环从零累积，满圈烤糊；关火后的成品不会继续烤糊。
- OrderManager 管理并行订单；教程第一关一张、后两关两张，有限清单全部完成才通关，超时原位重发。经典模式循环菜单持续补单，超时换新单，打烊后停止发单和交付。UI 经 GameRuntime.TrySubmitOrder(order) 操作，禁止直接扣库存。
- ClassicDayController 由 GameRuntime 创建并推进，管理简报/营业/结算，技能和跨天数据由 ClassicProgress 保存于本次运行内。每日 180 秒默认值来自 LevelConfig.serviceDuration，计时与生产只在营业期推进。基础原料无限仅用于经典模式，盘子与半成品保持有限；技能通过运行时倍率生效，禁止修改共享配方或设备资产。详见经典模式与教程关卡设计.md。
- 经典模式首日空场，所有设备由玩家购买；报价统一经 GameRuntime.GetCookerPrice，按设备定义 ID 累计成功购买次数，每台增加原价的 25%（关卡可配），总价向上取整。取消/失败不计数，拆除不降价，跨天免费恢复不重复计数；教程保持原价，禁止修改共享 CookerDefinition.Price。

## UI 视觉与重构约定

- 用户于 2026-10-19 确认 UI 方向：纯白大圆角底板、叠放在左上边缘的柔黄色圆角标题、深色清晰文字、平面按钮和短促弹性反馈。详细规范与重构清单见根目录 UI系统设计与动效方案.md 的“已确认规范”部分。
- 金币采用独立面板：白色圆角数字底框 + 独立的“金币”标题块，复用相同视觉语言；数值读取 GameRuntime.Money。
- 保留少量可交互的面包回形针装饰，点击可晃动、眨眼并弹出短句；不要给每个面板重复添加装饰或持续循环动画。
- 黑色点阵背景用于主菜单、独立展示等适用界面；厨房 HUD 要露出实际厨房，不能用全屏黑底遮住玩法。
- 主题、基础控件、动效与业务绑定分开；控件布局根节点稳定，动画作用于 Visual 子层级。保留 uGUI、TextMeshPro、HudScaler 和既有玩法输入通道。
- UI 重构准备阶段只记录规范与试样，不代表已经替换 Unity 场景。正式接入时逐步修改基础控件、金币/订单、其余 HUD 和主菜单，提供只更新 UI 的入口，禁止顺带重建玩法资产。

## Unity 资源编辑

- 保留已有 GUID、.meta 文件和预制体引用；移动资产时同时保留对应 .meta。
- 外部原始模型与游戏包装预制体分开。建议第三方资源放在 Assets/ThirdParty/<作者>/<包名>，游戏预制体放在 Assets/_Game/Prefabs。
- 只导入需要的格式和模型，不同时导入同一物件的 FBX、OBJ、GLB、Blend。
- 保留许可和共享贴图，记录名称、作者、来源 URL、许可证。
- 不提交 Library、Temp、obj、编辑器日志或临时审计工程。
- C# 沿用现有风格，运行时代码不直接依赖 UnityEditor。

## 美术接入

| 内容 | 配置字段 |
| --- | --- |
| 食材、成品、盘子 | ItemDefinition.viewPrefab，可选 viewMaterial、icon |
| 工作台 | CookerDefinition.viewPrefab |
| 员工 | LevelConfig.staffPrefab |
| 地面 | LevelConfig.floorPrefab |

- 工作台和员工预制体保留 CookerView/StaffView，模型放在 Visual 子层级，或通过 View 的 modelPrefab 引用独立模型。
- modelPrefab 不得指向包含该 View 的预制体自身，避免递归实例化。
- 不要直接把原始 FBX 当作完整工作台；缺少约定层级可能触发白盒补建，造成模型叠加。
- 保留交互层、选择反馈和必要碰撞体。美术偏移、旋转、缩放在 Visual 内处理，逻辑根物体保持格子对齐。
- 鼠标网格落点由 InputManager 将相机射线投到 Grid.Math.GroundY 平面求得，不依赖地板或装饰碰撞体；真实越界仍应拒绝。美术替换后验收需覆盖相机射线到建造、选中后移动的输入链路。
- 默认格子宽 2 单位，CookerView.MaxFootprint 为 1.8；检查占地、脚底、台面和视角遮挡。
- 各包比例和原点不同，依据实际导入尺寸调整，不统一套用 100 倍缩放。
- 当前 StaffView 只驱动 Animator 的 Selected 参数；走路、切菜、煎炒需补 Animator Controller 与状态连接。
- 游戏代码负责移动；动画优先保留 Generic，通常关闭 Root Motion，检查循环、朝向及道具显隐。

## 已检查的免费素材

以下为 2026-10-19 的检查记录；不表示素材已经导入原游戏，也不代表用户选定最终混搭方案。

| 素材包 | 下载文件 | 内容与许可 |
| --- | --- | --- |
| [Kenney Food Kit](https://kenney.nl/assets/food-kit) | kenney_food-kit.zip | 200 款模型；食材、成品、盘子、刀、砧板、锅；包内 CC0 |
| [Kenney Furniture Kit](https://kenney.nl/assets/furniture-kit) | kenney_furniture-kit.zip | 140 款模型；柜台、水槽、炉灶、冰箱、墙地；包内 CC0 |
| [Quaternius Sushi Restaurant Kit](https://quaternius.com/packs/sushirestaurantkit.html) | drive-download-20261019T111308Z-1-001.zip | 117 个模型变体，其中 16 个角色文件；官网 CC0，本包未附许可文本 |

建议尝试 Quaternius 角色/厨房环境 + Kenney Food；若优先简洁搭建，则用 Kenney Furniture + Food，角色仍取 Quaternius。先统一比例、灯光、配色，再决定补充资源。

### 验证结论与边界

- 三个 ZIP 均通过 CRC 检查，未发现零字节文件，所检查图片可解码。
- 独立工程用 Unity **2022.3.62f3** 导入了全部 **457 个 FBX**，均可加载并包含网格，材质使用 Standard。
- Food Kit 色板和 Quaternius 图集在测试布局下自动匹配；Furniture 使用纯色材质，无贴图属正常情况。
- 16 个角色 FBX 均导入 30 个动画片段，含 Idle、Walk、Run、Chop、Pan、Assembly。
- 这是导入与资源检查，不能表述为已在原游戏替换、逐段验证动画或完成画面验收；测试编辑器与项目中国版后缀不同。
- 不把不同格式、缩略图、角色装备变体误计为独立设计数量。

### 已知注意事项

- 当前项目首选已验证的 FBX，无需为这些包先安装 Blender。
- Food Kit 保留 Textures/colormap.png；Quaternius 保留 Sushi_Atlas.png。
- Quaternius FBX 留有作者绝对贴图路径；测试中能找到包内图集，改变目录后如丢色需手动重映射。
- Quaternius OBJ/MTL 有 117 处相对贴图引用未解析，图集本身存在，优先使用 FBX。
- 持刀锅版本 Rabbit_Pink.gltf、Rabbit_Green.gltf 仅含 Idle，对应 FBX 有完整 30 段。
- Unity 动画名带 CharacterArmature| 前缀，以实际导入名称配置。
- Kenney 柜体默认约 4.3 × 4.5 × 4.5 单位，原点靠底角，需要缩放和居中。
- Quaternius Environment_CuttingTable 是薄砧板类物件，不是完整柜台。
- Kenney washer、dryer 是洗衣机和烘干机，不是厨房洗盘台。
- 包内展示图的光照不是 FBX 自带效果，最终需要在游戏中配置。

## 当前素材映射与待补内容

四类工作台都能组合现成素材：

- chopping_station：柜台 + 砧板 + 刀。
- frying_pan：炉灶 + 煎锅。
- assembly_table：空柜台 + 盘子/配料展示。
- dish_washer：水槽 + 盘堆。

物品优先使用 Kenney Food：

| 游戏物品 ID | 建议 |
| --- | --- |
| bread | loaf，整条面包 |
| bread_slice | bread，实际是吐司片 |
| cabbage | cabbage |
| cabbage_slice | 待组合绿色碎叶/碎块 |
| tomato、tomato_slice | tomato、tomato-slice |
| raw_meat、cooked_meat | meat-raw、meat-cooked |
| raw_meat_slice、cooked_meat_slice | 待组合切块和生/熟材质，避免缩小带骨肉排造成每块都有骨头 |
| salad | salad，检查自带容器是否与游戏盘子重复 |
| meat_bread | 待组合面包与配料，sub/sandwich 可临时代替 |
| clean_plate | plate |
| dirty_plate | 待给盘子添加残渣/污渍 |

plate-dinner 已盛有食物，plate-broken 是破盘子，均不应直接替代空盘/脏盘。

优先补五种状态预制体，再处理工作台识别、角色动作、图标和蒸汽/水泡。Food Kit 单个预览为 64×64，可暂作小图标，大尺寸 UI 应统一角度重新渲染。没有明确缺口时无需继续下载大型综合包。

## 生成器与验证

- 修改前检查 Git 状态，保留用户已有改动；完成后检查差异，避免 Unity 导入造成无关变更。
- **不要把 Kitchen/3. Generate Everything 或 GenerateAndVerify 当作普通测试命令**：它们会重建资源、预制体或场景，可能覆盖手工调整。确需生成时先检查范围并保留已有工作。
- 只验收现有工程优先用 Kitchen/5. Verify Acceptance Criteria，报告为 Logs/acceptance_report.json。
- 只验收公开入口为 Kitchen.EditorTools.KitchenSceneGenerator.Verify；批处理还需查看报告失败项，不能只凭退出码宣称通过。
- 首次模型兼容性验证优先用独立临时工程。
- 美术替换后在 Play 中检查材质、比例、选择、移动、建造和工作台功能；动画修改需实际播放对应动作。
- 规则代码修改运行相关验收；纯文档修改无需启动 Unity。
- 区分静态检查、导入成功、游戏内验收，只声明实际完成的验证。

## 本机历史资料

- 2026-10-20 第二批：四台设备包装、对应建造预览和棋盘地砖已接入，切菜台柜体正面修正为与操作格 +Z 对齐。地板由 KitchenFloorView 按关卡尺寸自动铺设；GridView 已读取 LevelConfig.floorPrefab。说明见根目录 美术第二批替换与布景说明.md。
- ArtShowcase.unity 是独立美术展示，不是正式关卡。正式静态布景建议放 World/ArtDressing，不放会被运行时移除的 Preview。厨房编辑态地面可用 Kitchen/Art/Refresh Floor Preview (Art Only) 刷新，支持撤销且不自动保存。
- Kitchen/Art/Apply Workstations And Floor (Art Only) 会重建本批包装和地板材质，不作日常测试；手工打磨后避免覆盖。StageTwo 备份保留本批前预制体；四台设备与 Grid_Floor 原 GUID 不变。

- 2026-10-20 已完成美术首批接入：Panda、Kenney 切菜台、番茄/番茄片；详见根目录 美术首批替换记录.md 与 Assets/ThirdParty/Sources.md。其余工作台、物品、幽灵、环境仍待后续阶段。此前“未导入原项目”的记录仅描述历史检查当时的状态。
- 首批自动检查在独立完整工程副本中完成：规则 134/134、专项 16/16、两轮 Play 各 17/17；尚未宣称三关人工画面验收。原项目四个预制体 GUID 保留，白盒备份在 Assets/_Game/Art/Backups/StageOne。
- Kitchen/Art/Apply Stage One (Art Only) 会重建首批包装内部结构及 Panda Controller，手工打磨后不要把它当普通测试。专项 KitchenArtStageOneVerifier.Run 会切换测试场景，仅用于独立验证副本。

- 下载目录当时为 D:/Users/MangLF/Downloads，是重定向目录，找不到时需重新定位。
- 审计目录：C:/Users/MangLF/.codex/tmp/kitchen_asset_audit_20261019/。
- 内含 素材检查与选用建议.md、food_contact_sheet.jpg、audit.json、unity_import_report.json 和独立 UnityImportCheck 工程。
- 临时资料可能被清理，不得成为项目构建或运行依赖。需要重新检查时以当前代码、下载包和官方来源为准。

