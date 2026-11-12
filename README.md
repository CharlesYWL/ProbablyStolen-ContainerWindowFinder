# Container Window Finder / 箱子窗口定位

A small mod for **Probably Stolen – Cyberpunk Shopkeeper Sim** that helps you find which container window is open.
《深空当铺：可能是偷的》小 mod：帮你找到某个箱子对应的已打开窗口。

![demo](docs/demo.gif)

**Steam Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3838289931

## Features / 功能

- **Hover** a container whose window is open → that window briefly highlights. Layering is unchanged; a window behind others stays behind.
- **Click** the container once → the window highlights and jumps to the front (uses the game's own `PixelWindow.ToFront()`).
- **X-ray outline** → a thin dashed border shows the full window outline, even where it is covered by other windows or the item tooltip. Covered content is not changed.
- Highlights fade after ~2 s and never accumulate. Double-click, drag and box selection work as before.

- **悬停**在已打开窗口的箱子上 → 对应窗口短暂高亮，前后顺序不变，被挡住的仍在后面。
- **单击**箱子 → 窗口高亮并跳到最前面（调用游戏自带的 `PixelWindow.ToFront()`）。
- **透视边框** → 细虚线显示窗口完整轮廓，被其他窗口或物品信息面板挡住的部分也能看到，被挡内容不变。
- 高亮约 2 秒后淡出，不会越积越多。双击、拖拽、框选照旧。

No capabilities required (empty `<Capabilities />`), no save changes. / 不需要特殊权限，不修改存档。

## Install / 安装

- Workshop: subscribe → in game open **Mods** → select → **Activate** → restart.
- Manual: copy `manifest.xml` + `ContainerWindowFinder.dll` into `Probably Stolen\Mods\ContainerWindowFinder\`, then activate and restart.

## Build / 编译

Requires the .NET SDK (6+) and your own copy of the game. Game assemblies are **not** included in this repo.

```
dotnet build src -c Release -p:GameManaged="C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed"
```

Output: `src/bin/Release/netstandard2.1/ContainerWindowFinder.dll` (+ `manifest.xml`).

## Tuning / 调整

Colors, durations and line widths are constants at the top of `src/WindowFinderBehaviour.cs`.

## License

MIT. Not affiliated with the game's developers.
