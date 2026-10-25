# UI 皮肤包使用说明

> 适用：韩Mir2 客户端（2026-10-17 版起内置）
> 作用：**不改任何代码、不动任何布局**，把登录界面、人物选择、载入画面、游戏内各个面板的图换成你自己的。

---

## 一、原理（30 秒看懂）

客户端所有界面贴图都存在 `Data\*.Lib` 里，程序按「**库名 + 索引号**」取图。
皮肤包做的事很简单：**画某个索引前，先看看你的皮肤目录里有没有同名 PNG，有就用你的**，并自动缩放到原图尺寸——所以控件位置、按钮点击区、面板大小全部不变，只换像素。

找不到的索引自动回落原图 → **你只想换 1 张图，就只放 1 张图**。

---

## 二、三步上手

### 第 1 步：导出原图，找到索引号

```
UI\LibExport\LibExport.exe "E:\micromir2\Data" "E:\UI导出" Prguse Title
```

- 第 1 个参数 = 客户端 Data 目录
- 第 2 个参数 = 导出目录（自动创建）
- 后面的库名可写多个；**不写 = 导出全部库**

导出后每个库一个文件夹，文件名就是索引号：`E:\UI导出\Prguse\939.png`。
同目录的 `_index.csv` 记录了每张图的**宽高和偏移**，作图时按这个尺寸来。

### 第 2 步：改图，放进皮肤目录

假设皮肤名叫 `我的皮肤`：

```
E:\micromir2\UI\Skin\我的皮肤\Prguse\939.png     <- 换载入画面
E:\micromir2\UI\Skin\我的皮肤\Title\197.png      <- 换某个按钮
```

- 用 Photoshop / Aseprite 等任意工具改，**保持 PNG 格式、建议保持原图尺寸**
- PNG 的透明通道有效（半透明、镂空都支持）
- 尺寸不一致也会被自动缩放对齐，但**建议按原尺寸画**，文字类小图缩放会糊

### 第 3 步：启用

编辑 `E:\micromir2\UI\skin.ini`：

```ini
[Skin]
Name=我的皮肤
```

重启客户端即可。想恢复原版，把 `Name=` 留空。

---

## 三、常用界面在哪张图上

| 界面 | 库名 | 说明 |
|---|---|---|
| 载入画面 | `Prguse` 930~939 | 随机取一张，800×600 |
| 游戏内地图背景 | `Background` | 10=山脉 21=村口 22=沙漠 23=长城 |
| 选人界面 | `ChrSel` | 角色框大图 |
| 登录界面 | `Prguse` + `Title` | 背景 + 按钮文字图 |
| 各面板底图 | `Prguse` / `Prguse2` / `Prguse3` | 背包、技能、聊天等 |
| 主界面按钮/页签 | `Title` | |
| 底部整宽栏 | `PrguseEx` | 自适应分辨率那条 |
| BUFF 图标 | `BuffIcon` | |
| 技能图标 | `MagIcon`(小) / `MagIcon2`(大) | |

> 全部库都能换，上表只是最常用的。想全量导一遍：`LibExport.exe "E:\micromir2\Data" "E:\UI导出"`（会连怪物/装备图一起导，耗时较长）。

---

## 四、注意事项

1. **皮肤是纯客户端行为**，不用动服务端、不用重启服务端。
2. 微端玩家：`skin.ini` 和 `UI\Skin\` 放在**玩家本地客户端目录**（微端只同步 Data 资源，不覆盖 UI 目录）。
3. `Name` 填错或目录不存在 = 自动用原图，**不会报错、不会白屏**。
4. 单张图画坏了也只是那一张回落原图，不影响其他。
5. 想恢复原版：`Name=` 留空。
6. 多个皮肤 = 多个目录，切 `Name` 即可切换。

---

## 五、实现位置（给维护者）

| 文件 | 内容 |
|---|---|
| `Client/MirGraphics/UISkin.cs` | 皮肤包核心：ini 解析、索引扫描、PNG→纹理 |
| `Client/MirGraphics/MLibrary.cs` | `_libName` 字段；`CheckImage` 末尾调 `UISkin.TryApply`；`MImage.SkinApplied` 标记 |
| `Client/Program.cs` | `Settings.Load()` 后调用 `UISkin.Load()` |
| `Tools/LibExport/` | .Lib → PNG 导出工具（含 `_index.csv` 尺寸表） |
| 备份 | `Back Up\manual-20261017_2020-UI皮肤包\` |

版本：Client.dll `557e927b59a987835eb4aeda4bc01642`（已同步 `E:\micromir2\`、`Mir2Server\ClientResources\`）
