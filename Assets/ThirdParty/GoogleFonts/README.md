# 界面字体

2026-10-20 下载，用于主菜单；原始字体未修改。两个字体均采用 SIL Open Font License 1.1，许可文本分别保存在各字体目录的 OFL.txt，可离线随游戏分发。

| 字体 | 作者 | 下载来源 | 用途 |
| --- | --- | --- | --- |
| Lilita One Regular | Juan Montoreano | https://github.com/google/fonts/tree/main/ofl/lilitaone | 英文大标题、英文标签 |
| Noto Sans SC Bold | The Noto Project / Adobe | https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/SC | 中文标题、按钮、设置与提示 |
| ZCOOL KuaiLe（站酷快乐体） | Liu Bingke、Yang Kang、Wu Shaojie | https://github.com/google/fonts/tree/main/ofl/zcoolkuaile | 局内中文、按钮、标题和动态面板 |

Noto 原始文件：`Sans/SubsetOTF/SC/NotoSansSC-Bold.otf`，许可：`Sans/LICENSE`。

生成的 TMP SDF 字体在 `Assets/_Game/Art/Fonts`。`KitchenMenuFonts` 只烘焙菜单所需字符，不依赖操作系统字体。扩充菜单文案时，增加该生成器的字符集并重新生成对应 SDF 资产。

2026-10-21 为局内 UI 导入站酷快乐体，源字体未修改，随附该字体目录的 OFL.txt。`KitchenHudFonts` 从项目脚本/数据收集汉字并预烘焙常用符号，使用动态多页 SDF；站酷缺少的字形由单独的 Noto Sans SC Bold SDF 回退补齐。两者均引用仓库内可分发的源字体，不依赖系统微软雅黑。菜单字体保持原样；新增文案可动态补字，也可运行 Fonts Only 入口预烘焙。
