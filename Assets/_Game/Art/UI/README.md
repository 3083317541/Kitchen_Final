# 纸片 UI 图片

`BreadPaperclip.png` 与 `BreadPaperclipEyes.png` 是本项目原创透明图片，源稿分别为同名 SVG。参考了已确认网页试样的面包、金色金属线与表情比例，没有从教程截图提取角色或图标。

Unity 使用 PNG 作为 Sprite (2D and UI)，关闭 Mip Maps，保留透明通道，不压缩。身体与眼睛分层，点击时由 `PaperclipReaction` 晃动整体、压缩眼睛实现眨眼，并显示短句。可以用自己的等比例透明 PNG 覆盖，保留 `.meta` 即可维持引用。

源稿尺寸：身体 96 × 154、眼睛 48 × 18。PNG 以四倍尺寸导出，避免 HUD 放大时锯齿。眼睛位于身体的 `(48, 41)` 附近，独立层中心与此位置对齐。

重新导出需 Node.js 与 sharp，在项目根目录执行 `node Tools/UI/render-paperclip.cjs`。该工具只生成两张 PNG；Unity 运行和打包不依赖 Node.js 或 SVG 插件。

`PaperRounded.png` 继续作为圆角面板的九宫格 Sprite；机器进度圈由 `PaperRingGraphic` 绘制，读取实际加工值。
