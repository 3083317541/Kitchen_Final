# KITCHEN CLUB · TapTap 封面

建议上传以下大图：

| 用途 | 文件 | 尺寸 | 比例 |
| --- | --- | --- | --- |
| 横版封面 | kitchen-club-taptap-landscape-1840x860.png | 1840 × 860 | 92:43 |
| 竖版封面 | kitchen-club-taptap-portrait-1200x1800.png | 1200 × 1800 | 2:3 |

另附 460 × 215 横版、600 × 900 竖版，方便直接使用原要求尺寸或检查缩略图。
全部为 RGB PNG，背景不透明。图中文字仅为游戏标题 **KITCHEN CLUB**，包含厨师帽 LOGO，没有菜单按钮、标语、操作提示或其他宣传文字。

构图沿用此前网页的黑色点阵背景、白黄卡通标题、圆角餐牌和面包回形针；横版左右分区，竖版上下排列。
尺寸依据本次提供的横版最低 460 × 215、竖版最低 600 × 900 制作；未执行平台上传或平台审核。

## 素材与重新导出

- 字体：项目内 Lilita One，OFL 许可见 `Assets/ThirdParty/GoogleFonts/LilitaOne/`。
- 厨师帽：既有项目品牌源文件 `output/branding/chef-hat-logo.svg`。
- 回形针：项目 UI 资源 `Assets/_Game/Art/UI/BreadPaperclip.png` 和 `BreadPaperclipEyes.png`。
- 风格参考：此前 `kitchen-main-menu.html` 网页试样。
- 生成脚本：`Tools/Branding/create-taptap-covers.py`；需要 Python 与 Pillow。

在仓库根目录运行 `python Tools/Branding/create-taptap-covers.py` 可重新导出。
输出校验信息见 `export-manifest.json`。所有文件均放在美术交付目录，不需要导入 Unity 即可上传。
