using UnityEngine;
using KSP.UI.Screens;
using System.Collections.Generic;

namespace MapDisplay
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class MapDisplayMod : MonoBehaviour
    {
        private ApplicationLauncherButton toolbarButton;
        private bool showWindow;
        private Rect windowRect;
        private Texture2D mapTexture;
        private Texture2D drawableTexture;
        private Texture2D colorTex;
        private float zoomLevel;
        private Vector2 scrollPosition;
        private bool isDrawing;
        private Color drawColor;
        private int uiBrushSize;
        private List<Color[]> undoStack;
        private int maxUndoSteps;

        void Awake()
        {
            showWindow = false;
            windowRect = new Rect(100, 100, 900, 640);
            zoomLevel = 1.0f;
            scrollPosition = Vector2.zero;
            isDrawing = false;
            drawColor = Color.red;
            uiBrushSize = 1;
            undoStack = new List<Color[]>();
            maxUndoSteps = 20;
        }

        void Start()
        {
            mapTexture = GameDatabase.Instance.GetTexture("MicroMap/Textures/map", false);

            if (mapTexture != null)
            {
                drawableTexture = new Texture2D(mapTexture.width, mapTexture.height, TextureFormat.RGBA32, false);
                drawableTexture.SetPixels(mapTexture.GetPixels());
                drawableTexture.Apply();
                SaveUndoState();
            }

            colorTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            colorTex.wrapMode = TextureWrapMode.Repeat;
            colorTex.filterMode = FilterMode.Point;

            Texture2D buttonTexture = GameDatabase.Instance.GetTexture("MicroMap/Textures/icon", false);
            toolbarButton = ApplicationLauncher.Instance.AddModApplication(
                OnToolbarButtonToggle,
                OnToolbarButtonToggle,
                null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT,
                buttonTexture
            );
        }

        void OnDestroy()
        {
            if (toolbarButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
            }
        }

        void OnToolbarButtonToggle()
        {
            showWindow = !showWindow;
        }

        void OnGUI()
        {
            if (showWindow && drawableTexture != null)
            {
                windowRect = GUI.Window(12345, windowRect, DrawMapWindow, "Map");
            }
        }

        void SaveUndoState()
        {
            Color[] pixels = drawableTexture.GetPixels();
            Color[] copy = new Color[pixels.Length];
            System.Array.Copy(pixels, copy, pixels.Length);
            undoStack.Add(copy);
            if (undoStack.Count > maxUndoSteps)
            {
                undoStack.RemoveAt(0);
            }
        }

        void Undo()
        {
            if (undoStack.Count > 1)
            {
                undoStack.RemoveAt(undoStack.Count - 1);
                drawableTexture.SetPixels(undoStack[undoStack.Count - 1]);
                drawableTexture.Apply();
            }
        }

        void DrawMapWindow(int id)
        {
            if (GUI.Button(new Rect(windowRect.width - 25, 5, 20, 20), "X"))
            {
                showWindow = false;
            }

            float x = 10f;
            float y = 30f;
            float h = 22f;
            float gap = 6f;

            GUI.Label(new Rect(x, y, 50, h), "Zoom:");
            x += 45;
            if (GUI.Button(new Rect(x, y, 26, h), "+"))
            {
                zoomLevel = Mathf.Min(zoomLevel + 0.25f, 5f);
            }
            x += 30;
            if (GUI.Button(new Rect(x, y, 26, h), "-"))
            {
                zoomLevel = Mathf.Max(zoomLevel - 0.25f, 0.5f);
            }
            x += 30;
            if (GUI.Button(new Rect(x, y, 48, h), "Reset"))
            {
                zoomLevel = 1f;
                scrollPosition = Vector2.zero;
            }
            x += 54;

            GUI.Label(new Rect(x, y, 40, h), "Draw:");
            x += 38;
            isDrawing = GUI.Toggle(new Rect(x, y, 70, h), isDrawing, "Enable");
            x += 76;

            if (DrawColorButton(new Rect(x, y, 34, h), new Color32(255, 32, 32, 255), "R", Color.white))
            {
                drawColor = new Color32(255, 32, 32, 255);
            }
            x += 38;

            if (DrawColorButton(new Rect(x, y, 34, h), new Color32(32, 96, 255, 255), "B", Color.white))
            {
                drawColor = new Color32(32, 96, 255, 255);
            }
            x += 38;

            if (DrawColorButton(new Rect(x, y, 34, h), new Color32(40, 200, 60, 255), "G", Color.white))
            {
                drawColor = new Color32(40, 200, 60, 255);
            }
            x += 38;

            if (DrawColorButton(new Rect(x, y, 34, h), new Color32(255, 235, 20, 255), "Y", Color.black))
            {
                drawColor = new Color32(255, 235, 20, 255);
            }
            x += 38;

            if (DrawColorButton(new Rect(x, y, 34, h), new Color32(255, 255, 255, 255), "W", Color.black))
            {
                drawColor = new Color32(255, 255, 255, 255);
            }
            x += 38;

            if (DrawColorButton(new Rect(x, y, 40, h), new Color32(0, 0, 0, 255), "Bl", Color.white))
            {
                drawColor = new Color32(0, 0, 0, 255);
            }
            x += 46;

            float rightX = windowRect.width - 320f;
            if (rightX < x + 10f)
            {
                rightX = x + 10f;
            }

            if (GUI.Button(new Rect(rightX, y, 58, h), "Undo"))
            {
                Undo();
            }
            rightX += 62;

            if (GUI.Button(new Rect(rightX, y, 58, h), "Clear"))
            {
                drawableTexture.SetPixels(mapTexture.GetPixels());
                drawableTexture.Apply();
                undoStack.Clear();
                SaveUndoState();
            }
            rightX += 68;

            GUI.Label(new Rect(rightX, y, 36, h), "Size:");
            rightX += 36;
            uiBrushSize = (int)GUI.HorizontalSlider(new Rect(rightX, y + 4, 120, 16), uiBrushSize, 1, 15);
            rightX += 130;
            GUI.Label(new Rect(rightX, y, 30, h), uiBrushSize.ToString());

            float topMargin = 60f;
            Rect scrollViewRect = new Rect(10, topMargin, windowRect.width - 20, windowRect.height - topMargin - 10);
            Rect contentRect = new Rect(0, 0, (windowRect.width - 40) * zoomLevel, (windowRect.height - topMargin - 30) * zoomLevel);

            scrollPosition = GUI.BeginScrollView(scrollViewRect, scrollPosition, contentRect);

            Rect imageRect = GetAspectFitRect(new Rect(0, 0, contentRect.width, contentRect.height), drawableTexture.width, drawableTexture.height);
            GUI.DrawTexture(imageRect, drawableTexture, ScaleMode.StretchToFill, true);

            if (isDrawing && Input.GetMouseButton(0) && imageRect.Contains(Event.current.mousePosition))
            {
                Vector2 mp = Event.current.mousePosition;
                float nx = (mp.x - imageRect.x) / imageRect.width;
                float ny = (mp.y - imageRect.y) / imageRect.height;

                int tx = Mathf.RoundToInt(nx * (drawableTexture.width - 1));
                int ty = Mathf.RoundToInt((1f - ny) * (drawableTexture.height - 1));

                int actualBrush = uiBrushSize + 3;
                DrawOnTexture(tx, ty, actualBrush);
            }

            if (isDrawing && Event.current.type == EventType.MouseUp)
            {
                SaveUndoState();
            }

            GUI.EndScrollView();

            GUI.DragWindow(new Rect(0, 0, windowRect.width, 30));
        }

        bool DrawColorButton(Rect r, Color32 swatch, string label, Color labelColor)
        {
            colorTex.SetPixel(0, 0, swatch);
            colorTex.Apply(false);
            GUI.DrawTexture(r, colorTex);

            bool clicked = GUI.Button(r, GUIContent.none);

            Color oldColor = GUI.color;
            GUI.color = labelColor;
            GUIStyle centerStyle = new GUIStyle(GUI.skin.label);
            centerStyle.alignment = TextAnchor.MiddleCenter;
            centerStyle.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(r.x, r.y, r.width, r.height), label, centerStyle);
            GUI.color = oldColor;

            return clicked;
        }

        Rect GetAspectFitRect(Rect area, float texW, float texH)
        {
            float areaAspect = area.width / area.height;
            float texAspect = texW / texH;

            if (texAspect > areaAspect)
            {
                float h = area.width / texAspect;
                float yPos = area.y + (area.height - h) * 0.5f;
                return new Rect(area.x, yPos, area.width, h);
            }
            else
            {
                float w = area.height * texAspect;
                float xPos = area.x + (area.width - w) * 0.5f;
                return new Rect(xPos, area.y, w, area.height);
            }
        }

        void DrawOnTexture(int x, int y, int brushRadius)
        {
            int r = brushRadius;
            int r2 = r * r;

            for (int dy = -r; dy <= r; dy++)
            {
                int py = y + dy;
                if (py < 0 || py >= drawableTexture.height)
                {
                    continue;
                }

                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r2)
                    {
                    continue;
                    }
                    int px = x + dx;
                    if (px < 0 || px >= drawableTexture.width)
                    {
                        continue;
                    }

                    drawableTexture.SetPixel(px, py, drawColor);
                }
            }
            drawableTexture.Apply();
        }
    }
}