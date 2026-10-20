# 主菜单字体

2026-10-20 下载，用于主菜单；原始字体未修改。两个字体均采用 SIL Open Font License 1.1，许可文本分别保存在各字体目录的 OFL.txt，可离线随游戏分发。

| 字体 | 作者 | 下载来源 | 用途 |
| --- | --- | --- | --- |
| Lilita One Regular | Juan Montoreano | https://github.com/google/fonts/tree/main/ofl/lilitaone | 英文大标题、英文标签 |
| Noto Sans SC Bold | The Noto Project / Adobe | https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/SC | 中文标题、按钮、设置与提示 |

Noto 原始文件：`Sans/SubsetOTF/SC/NotoSansSC-Bold.otf`，许可：`Sans/LICENSE`。

生成的 TMP SDF 字体在 `Assets/_Game/Art/Fonts`。`KitchenMenuFonts` 只烘焙菜单所需字符，不依赖操作系统字体。扩充菜单文案时，增加该生成器的字符集并重新生成对应 SDF 资产；厨房 HUD 继续使用其现有字体。
