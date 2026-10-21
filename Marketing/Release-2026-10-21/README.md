# KITCHEN CLUB · 宣传资源定稿归档

归档日期：2026-10-21。此目录收录当前定稿游戏的商店素材与简介，不作为 Unity 运行依赖。

## 上传文件

| 用途 | 文件 | 尺寸 | 内容 |
| --- | --- | --- | --- |
| 游戏图标 | Icons/chef-hat-logo-1024.png | 1024×1024 | 暖黄实底、完整方图、无透明背景 |
| 小尺寸游戏图标 | Icons/chef-hat-logo-512.png | 512×512 | 同上 |
| 16:9 宣传图 / 视频封面 | Promotion/kitchen-club-store-cover-2560x1440.png | 2560×1440 | 仅游戏标题 KITCHEN CLUB，无游戏图标和其他文字 |
| 无 Logo 横版宣传图 | Promotion/kitchen-club-no-logo-landscape-2560x1440.png | 2560×1440 | 无文字、无 Logo 的厨房插画 |
| 无 Logo 竖版宣传图 | Promotion/kitchen-club-no-logo-portrait-1440x2560.png | 1440×2560 | 无文字、无 Logo 的厨房插画 |
| PC 游戏库壁纸 | Wallpaper/kitchen-club-library-wallpaper-3840x1240.png | 3840×1240 | 无文字、无 Logo；右侧主体，左侧留给平台 UI |
| TapTap 横竖封面 | TapTapCovers/ | 详见子目录 README | 含标题和图标；不要用于要求无 Logo 的栏位 |

上传使用 PNG 文件。`Sources/chef-hat-logo.svg` 仅为可编辑源文件。所有图片尺寸、字节数与 SHA-256 见 `manifest.json`。

## 商店简介

从一间空厨房开始，用有限的金币和几名员工，经营你的餐馆！购买、摆放并旋转厨具，安排员工切菜、煎肉、组装与洗盘，在源源不断的订单中挑战更高分。切菜离不开人，煎锅却能自行加热——抓住这段空隙调动员工，同时留心锅里的食物！每天打烊后，选择新技能，带着积累的金币与设备迎接下一天。简单的规则，交织出意想不到的厨房节奏：你的巧妙布局，能否让忙乱变成默契？

## 当前游戏范围

- 三关教程；经典模式每天包含备战简报、180 秒营业、结算与技能三选一。
- 并行订单、四向设备和共享操作格、人工加工与无人加热、自动关火及取出、焦糊清理、回盘与清洗。
- 经典模式空场与 400 金币开局，同类设备逐台增加原价的 25%，购买次数及布局跨天保留。
- 卡通菜单、局内中文字体、弹性反馈、暂停与设置、返回菜单及退出确认。
- 本次运行内跨天保留进度；退出本轮不存档。原地值守、自动岗位调度及物流属于后续设计，尚未实现。

## 来源与重制

图标、煎锅、砧板及食材插画由项目内 Python/Pillow 绘制脚本制作，没有使用图像生成 API。宣传文字使用项目已收录的 Lilita One 字体，许可见 `Assets/ThirdParty/GoogleFonts/LilitaOne/OFL.txt`。TapTap 封面的额外 UI 素材来源见其 README。

在项目根目录运行以下命令，可分别重新导出素材；会覆盖对应导出文件：

```powershell
python Tools/Branding/create-chef-logo.py
python Tools/Branding/create-store-cover.py
python Tools/Branding/create-store-cover.py --wallpaper
python Tools/Branding/create-store-cover.py --clean-cover
python Tools/Branding/create-store-cover.py --portrait
python Tools/Branding/create-taptap-covers.py
python Tools/Branding/archive-release.py
```

归档脚本从 `output/branding` 收集文件，校验图片后更新本目录和完整 ZIP 包。原始导出目录继续保留，便于按此前路径找到文件。当前归档不表示已上传平台或通过平台审核。
