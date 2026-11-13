using UnityEngine;

namespace ContainerWindowFinder
{
    /// <summary>
    /// Player settings, shown behind the gear button of this mod in the Mods menu.
    /// Values are read live every frame, so Apply takes effect immediately.
    /// </summary>
    internal static class Settings
    {
        private static readonly string[] ColorNames =
            { "Cyan", "Green", "Yellow", "Amber", "White", "Purple", "Red" };

        private static ConfigEntry<float> fadeOut;
        private static ConfigEntry<float> opacity;
        private static ConfigEntry<string> hoverColor;
        private static ConfigEntry<string> clickColor;
        private static ConfigEntry<bool> xray;
        private static ConfigEntry<bool> fill;
        private static ConfigEntry<bool> dragHighlight;

        public static void Define(string modId)
        {
            ModConfig c = ModHelper.GetConfig(modId);

            fadeOut = c.Define("fade_out_seconds", 0f, "Highlight / 高亮",
                () => "Fade-out after the mouse leaves (0–3 s) / 鼠标移开后淡出时间（0–3 秒）",
                min: 0f, max: 3f,
                tooltip: () => "Range 0–3 seconds. 0 = the glow disappears as soon as the mouse leaves the container.\n范围 0–3 秒。0 = 鼠标离开箱子立即消失。");
            opacity = c.Define("opacity", 0.75f, "Highlight / 高亮",
                () => "Brightness / 亮度", min: 0.2f, max: 1f);
            hoverColor = c.Define("hover_color", "Cyan", "Highlight / 高亮",
                () => "Hover color / 悬停颜色", choices: ColorNames);
            clickColor = c.Define("click_color", "Amber", "Highlight / 高亮",
                () => "Click color / 单击颜色", choices: ColorNames);
            fill = c.Define("tint_fill", true, "Highlight / 高亮",
                () => "Tint the window / 窗口淡色填充");
            xray = c.Define("xray_outline", true, "Highlight / 高亮",
                () => "Dashed outline through covering windows / 透视虚线框",
                tooltip: () => "Shows the window outline even where other windows or the tooltip cover it.\n被其他窗口或信息面板挡住的部分也显示轮廓。");
            dragHighlight = c.Define("drag_highlight", true, "Highlight / 高亮",
                () => "Highlight while dragging an item over a container / 拖着物品经过箱子时高亮");
        }

        public static float FadeOut => fadeOut != null ? Mathf.Max(0f, fadeOut.Value) : 0f;
        public static float Opacity => opacity != null ? Mathf.Clamp01(opacity.Value) : 0.75f;
        public static Color HoverColor => ToColor(hoverColor?.Value, "Cyan");
        public static Color ClickColor => ToColor(clickColor?.Value, "Amber");
        public static bool Fill => fill == null || fill.Value;
        public static bool Xray => xray == null || xray.Value;
        public static bool DragHighlight => dragHighlight == null || dragHighlight.Value;

        private static Color ToColor(string name, string fallback)
        {
            switch (name ?? fallback)
            {
                case "Green": return new Color(0.35f, 1f, 0.45f);
                case "Yellow": return new Color(1f, 0.95f, 0.3f);
                case "Amber": return new Color(1f, 0.75f, 0.2f);
                case "White": return new Color(0.95f, 0.95f, 1f);
                case "Purple": return new Color(0.75f, 0.5f, 1f);
                case "Red": return new Color(1f, 0.4f, 0.4f);
                case "Cyan":
                default: return new Color(0.35f, 0.85f, 1f);
            }
        }
    }
}
