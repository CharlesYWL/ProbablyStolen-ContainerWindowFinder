using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ContainerWindowFinder
{
    /// <summary>
    /// Polls the item under the cursor with the game's own RenderHandler.RaycastElement&lt;GameItem&gt;
    /// (the same helper ItemMouseHandler / ItemMouseDoubleClickHandler / DebugOwnershipToggleMode use).
    /// Hover  -> temporary highlight on the item's visible content window, z-order untouched.
    /// Click  -> highlight + PixelWindow.ToFront() (the game's own focus call).
    /// It never consumes input, so the game's click / double-click / drag handlers see everything as before.
    /// </summary>
    public class WindowFinderBehaviour : MonoBehaviour
    {
        // ---- tuning ----
        private const float HoverDelay = 0.12f;      // cursor must rest on the item this long before the hover flash
        private const float FadeIn = 0.08f;          // quick ease-in so the glow does not pop
        private const float BorderPx = 3f;
        private const float XrayThickness = 1f;   // x-ray dashed border: thin so it stays subtle
        private const float XrayDash = 6f;
        private const float XrayGap = 4f;
        private const float XrayAlpha = 0.7f;      // relative to the main highlight
        private const float FillAlpha = 0.22f;       // multiplied by the current fade alpha
        private const float DefaultClickSlop = 6f;   // px, used when ItemMouseDragHandler.dragThreshold is unknown

        private enum Kind { Hover, Click }

        private class Highlight
        {
            public PixelWindow window;
            public Kind kind;
            public GameObject root;
            public RectTransform rect;
            public Image fill;
            public Image[] edges;
            public float start;
            public Color color;
            public GameItem source;      // container whose hover keeps this glow alive
            public bool held;
            public float releasedAt;
            public bool triedBackground;
            public RectTransform background;
            // X-ray dashed border, drawn on our own top-most overlay canvas so it shows through
            // any window or tooltip covering this one. Border only: covered content is never touched.
            public RectTransform xray;
            public readonly List<Image> dashes = new List<Image>();
        }

        private const int OverlaySortingOrder = 32760;
        private readonly Vector3[] corners = new Vector3[4];

        private readonly List<Highlight> active = new List<Highlight>();

        private GameItem hoverCandidate;
        private float hoverSince;
        private GameItem hoverFired;          // item whose hover flash already played (no re-flash while resting on it)

        private GameItem pressItem;
        private Vector2 pressPos;
        private bool pressValid;
        private bool pendingClick;
        private GameItem pendingClickItem;

        private bool loggedError;

        // drag over an open container -> flash its window
        private GameItem dragWindowFired;

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    ModEntry.Log?.Warning("Update error (logged once): " + e);
                }
            }
        }

        private void LateUpdate()
        {
            try
            {
                // Clicks are resolved after every game Update has run this frame, so a single-click-interact
                // close / a double-click toggle is already applied and we only act on windows still visible.
                if (pendingClick)
                {
                    pendingClick = false;
                    GameItem item = pendingClickItem;
                    pendingClickItem = null;
                    PixelWindow w = VisibleWindowOf(item);
                    if (w != null)
                    {
                        w.ToFront();
                        Flash(w, Kind.Click, item);
                    }
                }
                Animate();
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    ModEntry.Log?.Warning("LateUpdate error (logged once): " + e);
                }
            }
        }

        private void OnDisable()
        {
            ClearAll();
        }

        private void OnDestroy()
        {
            ClearAll();
        }

        // ------------------------------------------------------------------ input

        private void Tick()
        {
            if (RenderHandler.current == null || UnityEngine.EventSystems.EventSystem.current == null)
            {
                ResetInput();
                return;
            }

            bool dragging = ItemMouseDragHandler.current != null && ItemMouseDragHandler.current.IsDraggingItem;
            Vector2 mouse = Input.mousePosition;
            TickDrag(dragging, mouse);
            GameItem hovered = dragging ? null : RenderHandler.RaycastElement<GameItem>(mouse);
            if (hovered != null && hovered.isDebugMenu) hovered = null;

            // ---- hover ----
            if (hovered != hoverCandidate)
            {
                hoverCandidate = hovered;
                hoverSince = Time.unscaledTime;
                if (hovered != hoverFired) hoverFired = null;
            }
            if (hoverCandidate != null && hoverFired != hoverCandidate && Time.unscaledTime - hoverSince >= HoverDelay)
            {
                hoverFired = hoverCandidate;
                PixelWindow w = VisibleWindowOf(hoverCandidate);
                if (w != null) Flash(w, Kind.Hover, hoverCandidate);
            }
            if (hovered == null) hoverFired = null;

            // ---- single click (left button), observed only ----
            if (Input.GetMouseButtonDown(0))
            {
                pressValid = !dragging && hovered != null && hovered.contentWindow != null && !ClickBlocked();
                pressItem = pressValid ? hovered : null;
                pressPos = mouse;
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (pressValid && pressItem != null && !dragging && !ClickBlocked())
                {
                    float slop = ItemMouseDragHandler.current != null && ItemMouseDragHandler.current.dragThreshold > 0f
                        ? ItemMouseDragHandler.current.dragThreshold
                        : DefaultClickSlop;
                    if ((mouse - pressPos).magnitude < slop && hovered == pressItem)
                    {
                        pendingClick = true;
                        pendingClickItem = pressItem;
                    }
                }
                pressValid = false;
                pressItem = null;
            }
        }

        private static bool ClickBlocked()
        {
            if (ItemPinHelper.BlocksClicks) return true;
            if (ItemSelectHandler.current != null && ItemSelectHandler.current.IsEquipped) return true;
            if (KeyBindHelper.GetKey("quick_move_inventory") || KeyBindHelper.GetKey("quick_move_showcase") || KeyBindHelper.GetKey("quick_move_storage")) return true;
            return false;
        }

        private void ResetInput()
        {
            hoverCandidate = null;
            hoverFired = null;
            pressValid = false;
            pressItem = null;
            pendingClick = false;
            pendingClickItem = null;
        }

        private static PixelWindow VisibleWindowOf(GameItem item)
        {
            if (item == null) return null;
            PixelWindow w = item.contentWindow;
            if (w == null || w.handler == null) return null;
            if (!w.IsVisible()) return null;
            if (!((Component)w.handler).gameObject.activeInHierarchy) return null;
            return w;
        }

        // ------------------------------------------------------------------ highlights

        private void Flash(PixelWindow window, Kind kind, GameItem source)
        {
            Highlight existing = null;
            foreach (Highlight h in active)
            {
                if (h.window == window) { existing = h; break; }
            }

            // A hover never downgrades a click highlight that is still showing on the same window.
            if (existing != null && existing.kind == Kind.Click && kind == Kind.Hover)
            {
                existing.source = source;
                existing.held = true;
                return;
            }

            if (existing == null)
            {
                existing = Create(window);
                if (existing == null) return;
                active.Add(existing);
                existing.start = Time.unscaledTime;
            }
            else if (!existing.held)
            {
                existing.start = Time.unscaledTime - FadeIn;   // re-hovered while fading: no dip
            }

            existing.kind = kind;
            existing.source = source;
            existing.held = true;
            existing.color = kind == Kind.Click ? Settings.ClickColor : Settings.HoverColor;
        }

        private Highlight Create(PixelWindow window)
        {
            RectTransform parent = window.rectTransform;
            if (parent == null) return null;

            GameObject root = new GameObject("CWF_Highlight"); root.AddComponent<RectTransform>();
            root.layer = parent.gameObject.layer;
            RectTransform rt = (RectTransform)root.transform;
            rt.SetParent(parent, false);
            rt.SetAsLastSibling();

            LayoutElement le = root.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.ignoreParentGroups = false;

            Highlight h = new Highlight
            {
                window = window,
                root = root,
                rect = rt,
                fill = MakeImage(rt, "Fill"),
                edges = new Image[4]
            };
            Stretch(h.fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            for (int i = 0; i < 4; i++) h.edges[i] = MakeImage(rt, "Edge" + i);
            // top, bottom, left, right
            Stretch(h.edges[0].rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -BorderPx), Vector2.zero);
            Stretch(h.edges[1].rectTransform, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, BorderPx));
            Stretch(h.edges[2].rectTransform, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(BorderPx, 0));
            Stretch(h.edges[3].rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-BorderPx, 0), Vector2.zero);

            FitToWindow(h);
            return h;
        }

        private static Image MakeImage(RectTransform parent, string name)
        {
            GameObject go = new GameObject(name); go.AddComponent<RectTransform>();
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f);
            return img;
        }

        private static void Stretch(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        /// <summary>
        /// The window's border/background children are laid out from the handler's top-left with
        /// sizeDelta = (widthPixels, heightPixels) (see PixelWindow.Validate). We mirror that.
        /// Uses only public members: PixelWindow.widthPixels / heightPixels.
        /// </summary>
        private static void FitToWindow(Highlight h)
        {
            RectTransform rt = h.rect;
            // Preferred: mirror the window's own private 'background' node rect (game type -> ModReflect is allowed).
            if (!h.triedBackground)
            {
                h.triedBackground = true;
                try
                {
                    if (ModReflect.Has(h.window, "background"))
                    {
                        TreeNodeRender bg = ModReflect.Get<TreeNodeRender>(h.window, "background");
                        if (bg != null) h.background = bg.rectTransform;
                    }
                }
                catch (System.Exception) { h.background = null; }
            }
            if (h.background != null)
            {
                RectTransform b = h.background;
                rt.anchorMin = b.anchorMin;
                rt.anchorMax = b.anchorMax;
                rt.pivot = b.pivot;
                rt.anchoredPosition = b.anchoredPosition;
                rt.sizeDelta = b.sizeDelta;
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
                return;
            }
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(Mathf.Max(8, h.window.widthPixels), Mathf.Max(8, h.window.heightPixels));
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        private void Animate()
        {
            float now = Time.unscaledTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Highlight h = active[i];
                if (h.root == null || h.window == null || h.window.handler == null || !h.window.IsVisible())
                {
                    Remove(i);
                    continue;
                }
                // Glow stays while the mouse (or a dragged item) is on its container,
                // then fades over the configured time (0 = disappears immediately).
                if (h.held && h.source != hoverCandidate && h.source != dragWindowFired)
                {
                    h.held = false;
                    h.releasedAt = now;
                }
                float a;
                if (h.held)
                {
                    a = Mathf.Clamp01((now - h.start) / FadeIn);
                }
                else
                {
                    float fade = Settings.FadeOut;
                    float t = fade <= 0.001f ? 1f : (now - h.releasedAt) / fade;
                    if (t >= 1f)
                    {
                        Remove(i);
                        continue;
                    }
                    a = 1f - Mathf.SmoothStep(0f, 1f, t);
                }
                a *= Settings.Opacity;

                // Keep on top *inside* the window (window's own sibling index is never touched)
                // and follow resizes.
                if (h.rect.GetSiblingIndex() != h.rect.parent.childCount - 1) h.rect.SetAsLastSibling();
                FitToWindow(h);

                Color edge = h.color; edge.a = a;
                Color fill = h.color; fill.a = Settings.Fill ? a * FillAlpha : 0f;
                h.fill.color = fill;
                for (int e = 0; e < h.edges.Length; e++) h.edges[e].color = edge;
                if (Settings.Xray)
                {
                    if (h.xray != null && !h.xray.gameObject.activeSelf) h.xray.gameObject.SetActive(true);
                    Color xr = edge; xr.a *= XrayAlpha;
                    UpdateXray(h, xr);
                }
                else if (h.xray != null && h.xray.gameObject.activeSelf)
                {
                    h.xray.gameObject.SetActive(false);
                }
            }
        }

        private void Remove(int index)
        {
            Highlight h = active[index];
            active.RemoveAt(index);
            if (h.root != null) Destroy(h.root);   // x-ray canvas is a child of root
        }

        // ------------------------------------------------------------------ x-ray dashed border

        private void UpdateXray(Highlight h, Color color)
        {
            // The x-ray border lives INSIDE the window's own transform (same coordinates, so it can never
            // drift), but on a nested Canvas with overrideSorting so it is drawn above other windows and
            // the item tooltip. It is only an outline: whatever covers the window is left untouched.
            if (h.xray == null)
            {
                GameObject go = new GameObject("CWF_Xray"); go.AddComponent<RectTransform>();
                go.layer = h.root.layer;
                h.xray = (RectTransform)go.transform;
                h.xray.SetParent(h.rect, false);
                h.xray.anchorMin = Vector2.zero;
                h.xray.anchorMax = Vector2.one;
                h.xray.pivot = Vector2.zero;
                h.xray.offsetMin = Vector2.zero;
                h.xray.offsetMax = Vector2.zero;
                Canvas c = go.AddComponent<Canvas>();
                c.overrideSorting = true;
                c.sortingOrder = OverlaySortingOrder;
                // no GraphicRaycaster: never takes a click
            }
            if (h.xray.GetComponent<Canvas>() is Canvas cv && !cv.overrideSorting)
            {
                cv.overrideSorting = true;
                cv.sortingOrder = OverlaySortingOrder;
            }

            Vector2 size = h.rect.rect.size;
            float thick = XrayThickness, dash = XrayDash, gap = XrayGap;
            int used = 0;
            used = DashEdge(h, used, new Vector2(0f, size.y - thick), size.x, true, thick, dash, gap, color);  // top
            used = DashEdge(h, used, new Vector2(0f, 0f), size.x, true, thick, dash, gap, color);              // bottom
            used = DashEdge(h, used, new Vector2(0f, 0f), size.y, false, thick, dash, gap, color);             // left
            used = DashEdge(h, used, new Vector2(size.x - thick, 0f), size.y, false, thick, dash, gap, color); // right
            for (int i = used; i < h.dashes.Count; i++) h.dashes[i].enabled = false;
        }

        private int DashEdge(Highlight h, int used, Vector2 start, float length, bool horizontal,
            float thick, float dash, float gap, Color color)
        {
            for (float p = 0f; p < length; p += dash + gap)
            {
                float seg = Mathf.Min(dash, length - p);
                Image img;
                if (used < h.dashes.Count) img = h.dashes[used];
                else
                {
                    img = MakeImage(h.xray, "Dash");
                    RectTransform r = img.rectTransform;
                    r.anchorMin = r.anchorMax = Vector2.zero;
                    r.pivot = Vector2.zero;
                    h.dashes.Add(img);
                }
                used++;
                img.enabled = true;
                img.color = color;
                RectTransform rt = img.rectTransform;
                Vector2 pos = horizontal ? new Vector2(start.x + p, start.y) : new Vector2(start.x, start.y + p);
                Vector2 size = horizontal ? new Vector2(seg, thick) : new Vector2(thick, seg);
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }
            return used;
        }

        // ------------------------------------------------------------------ drag target

        private void TickDrag(bool dragging, Vector2 mouse)
        {
            // Carrying an item over a container whose window is open: flash that window.
            // The game already outlines drop targets itself, so nothing is drawn on the item.
            GameItem target = null;
            if (dragging)
            {
                GameItemElement carried = ModReflect.Get<GameItemElement>(ItemMouseDragHandler.current, "currentItem");
                if (carried != null)
                {
                    target = RenderHandler.RaycastElement<GameItem>(mouse, carried.fakeBackground);
                    if (target == carried || (target != null && target.isDebugMenu)) target = null;
                }
            }
            if (!Settings.DragHighlight) target = null;
            if (target != null && target != dragWindowFired)
            {
                dragWindowFired = target;
                PixelWindow w = VisibleWindowOf(target);
                if (w != null) Flash(w, Kind.Hover, target);
            }
            if (target == null) dragWindowFired = null;
        }

        private void ClearAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) Remove(i);
            dragWindowFired = null;
            ResetInput();
        }
    }
}
