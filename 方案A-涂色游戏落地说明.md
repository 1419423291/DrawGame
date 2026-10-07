# 方案 A：涂色游戏落地说明

给负责 `E:\UnityDemo\Draw` 的 Agent。本文只改涂色游戏。Web 编辑器（`E:\UnityDemo\Asset_Editor`）由另一份同名契约的说明负责，不要改那个工程。

**目标：** 读取 schema 3 关卡。调色盘每一格是一种观感颜色。点对颜色时，从 `GCImg_color.png` 拷贝该区域的原图像素；点错颜色时，铺这一格的代表色纯色。

**现行代码：** `Assets/Scripts/Workspace/ColoringLevelRuntime.cs` 把 `paletteItems[].styleId` 当成填充配方，`linearGradient` 在区域 bbox 里插值。`ToolBucketStrategyImpl` 已经是「一次点击、一个区域、整区写入、未改动不记撤销」。这个调用方式保留，填充结果改到运行时内部。

Unity 版本是 2021.3.16f1c1，颜色空间是 Gamma。

## 与 Web 编辑器的并行

可以并行。两边都以实现下面这份契约为准，不要在实现过程中改字段名、编号规则或文件名。游戏用本文的 `level99` 夹具自测，不必等编辑器重导 level02。联调等两边都落地后再做。

当前 `Assets/Textures/Pictures/level01` 与 `level02` 仍是 schema 2，没有可作上色来源的彩色原图。目录扫描遇到 schema 不是 3、或缺少 `GCImg_color.png` 的关卡时跳过并打警告。在编辑器重导之前，这两关不会出现在选图列表里。不要为 schema 2 保留渐变填充。

## 数据契约 level-data/3

游戏与编辑器共用这一节。`schemaVersion` 为整数 **3**。游戏拒绝任何其他版本。

### 关卡目录

游戏读取的关卡资产是这四个文件，位于 `Assets/Textures/Pictures/level*/`：

| 文件 | 作用 |
| --- | --- |
| `level_data.json` | 契约正文 |
| `regionId_map.png` | 区域编号 |
| `lineart.png` | 线稿 |
| `GCImg_color.png` | 彩色原图。上色时按区域从这里拷像素 |

同目录里若有 `edit_state.json` 或 `label_map.png`，游戏忽略。

### level_data.json

```json
{
  "schemaVersion": 3,
  "levelId": "level_02",
  "canvasWidth": 1920,
  "canvasHeight": 1080,
  "regions": [
    {
      "regionId": 2,
      "paletteItemId": 2,
      "bbox": { "x": 742, "y": 229, "w": 591, "h": 687 }
    }
  ],
  "paletteItems": [
    { "paletteItemId": 2, "color": "#6BB086" }
  ],
  "artAssets": {
    "regionIdMap": "regionId_map.png",
    "lineArt": "lineart.png",
    "colorImage": "GCImg_color.png",
    "recommendedImportSettings": {
      "regionIdMap": { "compression": "None", "filterMode": "Point", "mipmap": false, "readable": true, "sRGB": false, "npotScale": "None" },
      "lineArt": { "compression": "Default", "filterMode": "Bilinear", "mipmap": true, "readable": true, "npotScale": "None" },
      "colorImage": { "compression": "None", "filterMode": "Point", "mipmap": false, "readable": true, "sRGB": true, "npotScale": "None" }
    }
  }
}
```

字段规则：

- 根上有 `schemaVersion`、`levelId`、`canvasWidth`、`canvasHeight`、`regions`、`paletteItems`、`artAssets`。
- 没有 `styles`、`targetStyleId`、`styleId`、`type`、`colorStart`、`colorEnd`、`angleDeg`、`createdAt`。`JsonUtility` 会忽略多余字段，但加载逻辑不得再读取它们。
- `regions` 按 `regionId` 升序。`regionId` 为 1 到 255 的整数，且每个编号恰好出现一次。超出该范围或出现重复时整关加载失败。
- `bbox` 原点在图像左上，x 向右，y 向下，单位像素，宽高至少为 1。上色不读取 bbox。
- `paletteItems` 的数组顺序就是调色盘从左到右的顺序。`paletteItemId` 从 1 起，按数组顺序连续递增。
- `color` 是大写 `#RRGGBB`。它是该格的代表色，也是玩家涂错区域时铺上的纯色。
- 每个 `regions[].paletteItemId` 必须能在 `paletteItems` 里找到。找不到时整关加载失败。允许某一格没有任何区域引用，游戏夹具用它验证涂错色。
- 三张图的宽高都等于区域图的 `width` × `height`。与 `canvasWidth` / `canvasHeight` 不一致时打警告，并以贴图像素尺寸为准。

### 区域图与彩色原图

区域编号只看 R 通道，且 A 为 0 或 R 为 0 的像素不可涂。`GetPixels32` 的下标与画布一致：`x + y * width`，y 从贴图底部起算。彩色原图使用同一下标，拷贝时不要再做 Y 翻转。

`GCImg_color.png` 必须可读、无 mipmap、Point、`npotScale = None`、无压缩（RGBA32）。工程是 Gamma，`GetPixels32` 得到的通道直接写入画布。

### 调色盘如何生成

聚类只在 Web 编辑器完成。游戏按 JSON 显示 `paletteItems`，不要用色差再并一次。

代表色与 OKLab 的算法记在编辑器说明里。游戏只消费结果。缺省合并距离 0.02 时，身体、手臂、脚会是同一格，草地是另一格。

### 游戏上色规则

玩家选中 `paletteItems` 数组里的一格（数组顺序即调色盘从左到右）。点击区域 R 时，用这一格的 `paletteItemId` 比较，不用数组下标比较：

- 等于 R 的 `paletteItemId`：把 R 在 `GCImg_color.png` 上的 RGB 拷进画布，alpha 写成 255。源图 alpha 不参与，不跳过半透明像素。
- 不相等：R 的每个像素铺成这一格的 `color`，alpha 写成 255。

区域 0、点在画布外、或该区域没有像素：不写入，并走现有的 `onWrongAction("persistentborder click")`。写入后与当前画布 RGB 完全相同：不调用 `fetchColors`，不产生撤销步。一次点击只涂中的那一个区域。没有次数限制。

撤销和重做仍走现有的整帧像素缓冲。UI 参考分辨率保持 1024×768。画布像素尺寸跟随关卡贴图。

## 游戏调整

| 文件 | 改动 |
| --- | --- |
| `Assets/Scripts/Workspace/ColoringLevel.cs` | 增加 `public Texture2D colorImage`。`preview` 继续给选图图标，可以和彩色原图是同一张贴图。 |
| `Assets/Scripts/Workspace/ColoringLevelRuntime.cs` | 只接受 `schemaVersion == 3`。加载 `colorImage` 的 `GetPixels32`，并记下每个 `regionId` 的 `paletteItemId`。`Palette` 与 `PreviewAt` 使用代表色纯色，不再生成渐变预览。`BeginFill` / `ColorAtPixel` 按上一节选择拷贝或铺纯色。`Load` 在缺图、不可读、尺寸不一致、版本不对、调色盘为空、区域引用了不存在的 `paletteItemId` 时返回 false 并 `Debug.LogError`。 |
| `Assets/Scripts/Workspace/Logic/ToolBucketStrategyImpl.cs` | 保持现有调用。不要在这里分支渐变。 |
| `Assets/Scripts/Editor/ColoringLevelCatalogBuilder.cs` | 只收录同时有 `level_data.json`、`lineart.png`、`regionId_map.png`、`GCImg_color.png` 且 `schemaVersion` 为 3 的关卡，其余跳过并警告。用这次扫描结果覆盖选图列表，schema 2 的旧关卡不要留在列表里。一张合格关卡都没有时，列表改为空并警告。扫描过程本身失败（目录不存在或抛错）时保留原列表。把彩色原图赋给 `colorImage`，选图预览也用它。刷新时把 `GCImg_color.png` 的导入设置写成契约里的 `colorImage` 要求：`isReadable = true`，`mipmapEnabled = false`，`filterMode = Point`，`npotScale = None`，`textureCompression = Uncompressed`，Standalone 覆盖为 `RGBA32`。区域图和线稿的现有导入要求保持不变。 |
| `Assets/Scripts/OnGUI/PredefinedColorsAndPalette.cs` | 继续用 `PreviewAt` 画色块，不着色。色块数量随 `PaletteCount`。 |
| `Assets/Scripts/Controllers/MainColorSelectController.cs` | 继续用调色盘下标调用 `SelectStyle`。下标对应 `paletteItems` 数组顺序。 |

`Assets/Scripts/Workspace/CanvasCamera.cs`、画布分层、撤销缓冲、开局进入列表第一关，这些不要改。

序列化类型要能被 `JsonUtility` 读到：`schemaVersion`、`paletteItems[].paletteItemId`、`paletteItems[].color`、`regions[].regionId`、`regions[].paletteItemId`。选中项与区域是否同一格，用 `paletteItemId` 比较。调色盘点击下标只用来取数组里的那一格。

## 验收标准

在 `Assets/Textures/Pictures/level99/` 放一套 2×2 的最小关卡，不改 level01 / level02 的文件。下列「顶行」指 PNG 里先出现的那一行（图像 y 向下的 y=0）：

- 区域图顶行两个像素 R=1，底行两个像素 R=2，G=0、B=0、A=255。
- 彩色原图顶行是 `#112233`、`#445566`，底行两个像素都是 `#778899`。
- `paletteItems` 两格：`paletteItemId` 1 的 `color` 为 `#112233`，`paletteItemId` 2 的 `color` 为 `#AABBCC`。区域 1 和区域 2 的 `paletteItemId` 都是 1。第 2 格没有区域引用。
- `schemaVersion` 为 3，`canvasWidth` 为 2，`canvasHeight` 为 2。线稿两行都透明。

进入 Play 后确认：

1. 选图列表里是 level99，没有 level01 和 level02。控制台有跳过 schema 2 的警告。
2. 调色盘是两格纯色：`#112233` 与 `#AABBCC`。没有渐变预览。
3. 选第 1 格，点区域 1：两个像素变成 `#112233` 与 `#445566`。点区域 2：两个像素都变成 `#778899`。
4. 选第 2 格，点区域 1：两个像素都变成 `#AABBCC`。再选第 1 格点回去，又恢复成原图的两个不同像素。
5. 对已经是目标颜色的区域再点一次，撤销步数不增加。点区域外不写入。
6. 把 level99 的 `schemaVersion` 改成 2，或拿掉 `GCImg_color.png`，刷新后列表里没有 level99，也不会把 level01、level02 加回去。测完改回 schema 3 并放回彩色原图，列表恢复为 level99。
7. `GCImg_color.png` 的导入是可读、无 mipmap、Point、npot 为 None、无压缩。区域图没有被重新缩放。
