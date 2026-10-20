# 第三方素材来源与首批导入记录

主菜单字体另见 `Assets/ThirdParty/GoogleFonts/README.md`：Lilita One 与 Noto Sans SC Bold，均附 SIL OFL 1.1 许可。

首批接入：2026-10-20。仅选用以下 FBX，不包含整包所有格式。

| 作者 / 包 | 来源 | 许可依据 | 本次使用 |
| --- | --- | --- | --- |
| Kenney / Food Kit 2.0 | https://kenney.nl/assets/food-kit | 包内 License.txt，CC0 | tomato、tomato-slice、cutting-board、cooking-knife，及共享 colormap.png |
| Kenney / Furniture Kit 2.0 | https://kenney.nl/assets/furniture-kit | 包内 License.txt，CC0 | kitchenCabinet，纯色材质 |
| Quaternius / Sushi Restaurant Kit | https://quaternius.com/packs/sushirestaurantkit.html | 官网标注 CC0，所下载 ZIP 未附许可文本 | 普通版 Panda、Environment_Counter_Straight、共享 Sushi_Atlas.png |

CC0 说明：https://creativecommons.org/publicdomain/zero/1.0/

原始网格与贴图没有修改。项目包装单独调整尺寸、偏移和朝向。Panda 的 FBX 导入设置将 Idle、Walk、Chop_Loop 设为循环，并使用项目内 Generic Animator Controller。动画不驱动位移、计时、扣料或产出。

现用切菜台选 Kenney 柜体，采用 0.38 / 0.23 / 0.38 的 XYZ 包装缩放，形成约 1.63 × 1.04 × 1.71 的单格柜体；砧板最大水平尺寸约 0.82，番茄约 0.36、番茄片约 0.40。使用 Quaternius 柜体的对照预制体保存在 Assets/_Game/Prefabs/Art/Chopping_Quaternius_Comparison.prefab，柜体统一缩放 0.45，约 1.80 × 0.87 × 0.79。

Panda 依据 Idle 姿势的实际蒙皮网格统一缩至高约 1.55，并将脚底归零。最终以试装截图与游戏镜头验收为准。

首批范围为熊猫、切菜台、番茄及番茄片。其余设备和食物仍为原有白盒，后续按主方案分批接入。

## 第二批接入（2026-10-20）

沿用以上来源与 CC0 许可，额外导入 Kenney Food 的 frying-pan、plate，以及 Furniture 的 kitchenStove、kitchenCabinetDrawer、kitchenSink、floorFull，共 6 个 FBX；累计 13 个 FBX。

新增炉灶、抽屉柜、水槽统一沿用柜体包装缩放 0.38 / 0.23 / 0.38，并在局部 Y 轴旋转 180°，将原 -Z 门面 / 操作面朝向项目 +Z。切菜台同样修正。设备实际水平占地约 1.63 × 1.71，小于 1.8。

frying-pan 最大水平长度约 1.15，plate 约 0.43。floorFull 包装为宽深各 1、高 0.09、顶面 Y=0 的单块砖，再由 KitchenFloorView 按格子宽铺设；使用项目 Standard 材质替换地砖颜色。原 FBX 网格未修改。

当前四类设备和地面均已接入；未补齐的其余物品与特效见根目录《美术第二批替换与布景说明》。
