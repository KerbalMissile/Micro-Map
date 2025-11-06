using UnityEngine;
using KSP.UI.Screens;
using System.Collections.Generic;
using System.IO;
using System;

namespace MapDisplay
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class MapDisplayMod : MonoBehaviour
    {
    private ApplicationLauncherButton toolbarButton;
    private bool showWindow;
    private Rect windowRect;
    private Texture2D[] mapTextures;
    private Texture2D[] drawableTextures;
    private int currentMapIndex;
    private int maxMapIndex;
    private Texture2D colorTex;
    private Texture2D arrowTexture;
    private float zoomLevel;
    private Vector2 scrollPosition;
    private bool isDrawing;
    private bool isTextMode;
    private Color drawColor;
    private int uiBrushSize;
    private string textInput;
    private List<TextLabel> textLabels;
    private List<MapState> undoStack;
    private int maxUndoSteps;
    private string saveFolder;
    private string[] drawingPaths;
    private string[] textPaths;
    private string undoPath;
    private bool needsSave;
    private float autoSaveTimer;
    private float autoSaveInterval;
    private bool showPlayerPosition;
    private bool showOrbitLines;
    private bool textureNeedsApply;
    private float applyTimer;
    private Vector2 lastDrawPos;
    private bool wasDrawing;

    [System.Serializable]
    public class TextLabel
    {
    public string text;
    public Vector2 position;
    public Color color;

    public TextLabel(string t, Vector2 p, Color c)
    {
    text = t;
    position = p;
    color = c;
    }
    }

    public class MapState
    {
    public Color[] pixels;
    public List<TextLabel> labels;

    public MapState(Color[] p, List<TextLabel> l)
    {
    pixels = new Color[p.Length];
    System.Array.Copy(p, pixels, p.Length);

    labels = new List<TextLabel>();
    foreach (TextLabel label in l)
    {
    labels.Add(new TextLabel(label.text, label.position, label.color));
    }
    }
    }

    void Awake()
    {
    showWindow = false;
    windowRect = new Rect(100, 100, 900, 640);
    zoomLevel = 1.0f;
    scrollPosition = Vector2.zero;
    isDrawing = false;
    isTextMode = false;
    drawColor = Color.red;
    uiBrushSize = 1;
    textInput = "";
    textLabels = new List<TextLabel>();
    undoStack = new List<MapState>();
    maxUndoSteps = 20;
    needsSave = false;
    autoSaveTimer = 0f;
    autoSaveInterval = 5f;
    showPlayerPosition = true;
    showOrbitLines = true;
    textureNeedsApply = false;
    applyTimer = 0f;
    lastDrawPos = Vector2.zero;
    wasDrawing = false;
    currentMapIndex = 0;
    maxMapIndex = 0;

    mapTextures = new Texture2D[4];
    drawableTextures = new Texture2D[4];
    drawingPaths = new string[4];
    textPaths = new string[4];

    saveFolder = KSPUtil.ApplicationRootPath + "GameData/MicroMap/SavedMaps/";

    for (int i = 0; i < 4; i++)
    {
    string suffix = (i == 0) ? "" : (i + 1).ToString();
    drawingPaths[i] = saveFolder + "current_drawing" + suffix + ".cache";
    textPaths[i] = saveFolder + "text_labels" + suffix + ".txt";
    }

    undoPath = saveFolder + "undo_history.cache";

    if (!Directory.Exists(saveFolder))
    {
    Directory.CreateDirectory(saveFolder);
    }
    }

    void Start()
    {
    // Load maps in sequence until one is missing
    for (int i = 0; i < 4; i++)
    {
    string mapName = (i == 0) ? "map" : "map" + (i + 1).ToString();
    Texture2D loadedMap = GameDatabase.Instance.GetTexture("MicroMap/Textures/" + mapName, false);

    if (loadedMap != null)
    {
    mapTextures[i] = loadedMap;
    drawableTextures[i] = new Texture2D(loadedMap.width, loadedMap.height, TextureFormat.RGBA32, false);
    maxMapIndex = i;
    }
    else
    {
    break;
    }
    }

    // Load saved drawings and text for each map
    for (int i = 0; i <= maxMapIndex; i++)
    {
    if (File.Exists(drawingPaths[i]))
    {
    LoadDrawing(i);
    }
    else
    {
    drawableTextures[i].SetPixels(mapTextures[i].GetPixels());
    drawableTextures[i].Apply();
    }
    }

    LoadTextLabels();
    LoadUndoHistory();

    if (undoStack.Count == 0)
    {
    SaveUndoStateImmediate();
    }

    colorTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
    colorTex.wrapMode = TextureWrapMode.Repeat;
    colorTex.filterMode = FilterMode.Point;

    arrowTexture = GameDatabase.Instance.GetTexture("MicroMap/Textures/arrow", false);
    if (arrowTexture == null)
    {
    arrowTexture = GenerateArrowTexture();
    }

    // Wait for ApplicationLauncher to be ready
    GameEvents.onGUIApplicationLauncherReady.Add(OnAppLauncherReady);
    }

    void OnAppLauncherReady()
    {
    if (toolbarButton == null)
    {
    Texture2D buttonTexture = GameDatabase.Instance.GetTexture("MicroMap/Textures/icon", false);
    if (buttonTexture == null)
    {
    buttonTexture = GenerateIconTexture();
    }

    toolbarButton = ApplicationLauncher.Instance.AddModApplication(
    OnToolbarButtonToggle,
    OnToolbarButtonToggle,
    null, null, null, null,
    ApplicationLauncher.AppScenes.FLIGHT,
    buttonTexture
    );
    }
    }

    Texture2D GenerateIconTexture()
    {
    Texture2D tex = new Texture2D(38, 38, TextureFormat.RGBA32, false);
    Color[] pixels = new Color[38 * 38];

    for (int i = 0; i < pixels.Length; i++)
    {
    pixels[i] = new Color(0.2f, 0.4f, 0.8f, 1f);
    }

    tex.SetPixels(pixels);
    tex.Apply();
    return tex;
    }

    Texture2D GenerateArrowTexture()
    {
    int size = 32;
    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    Color[] pixels = new Color[size * size];

    for (int i = 0; i < pixels.Length; i++)
    {
    pixels[i] = new Color(0, 0, 0, 0);
    }

    Color arrowColor = new Color(1f, 0.2f, 0.2f, 1f);
    Color outlineColor = new Color(1f, 1f, 1f, 1f);

    int centerX = size / 2;

    for (int y = 0; y < size / 2; y++)
    {
    int width = (size / 2 - y) / 2;
    for (int x = centerX - width; x <= centerX + width; x++)
    {
    if (x >= 0 && x < size && y >= 0 && y < size)
    {
    pixels[y * size + x] = arrowColor;

    if (x == centerX - width || x == centerX + width || y == 0)
    {
    pixels[y * size + x] = outlineColor;
    }
    }
    }
    }

    int shaftWidth = 3;
    for (int y = size / 2; y < size - 2; y++)
    {
    for (int x = centerX - shaftWidth; x <= centerX + shaftWidth; x++)
    {
    if (x >= 0 && x < size && y >= 0 && y < size)
    {
    pixels[y * size + x] = arrowColor;

    if (x == centerX - shaftWidth || x == centerX + shaftWidth)
    {
    pixels[y * size + x] = outlineColor;
    }
    }
    }
    }

    tex.SetPixels(pixels);
    tex.Apply();
    tex.filterMode = FilterMode.Bilinear;
    return tex;
    }

    bool ShouldShowPlayerIcon()
    {
    if (FlightGlobals.ActiveVessel == null)
    {
    return false;
    }

    string bodyName = FlightGlobals.ActiveVessel.mainBody.name;

    // Map index 0 (map) = Kerbin
    // Map index 1 (map2) = Mun
    // Map index 2 (map3) = can be configured
    // Map index 3 (map4) = can be configured

    switch (currentMapIndex)
    {
    case 0:
    return bodyName == "Kerbin";
    case 1:
    return bodyName == "Mun";
    case 2:
    // Add your planet name here for map3
    return true; // Shows on all planets for now
    case 3:
    // Add your planet name here for map4
    return true; // Shows on all planets for now
    default:
    return false;
    }
    }

    Vector2 GetPlayerMapPosition()
    {
    if (FlightGlobals.ActiveVessel == null)
    {
    return new Vector2(0.5f, 0.5f);
    }

    Vessel vessel = FlightGlobals.ActiveVessel;
    double lat = vessel.latitude;
    double lon = vessel.longitude;

    float y = (float)((90.0 - lat) / 180.0);
    float x = (float)((lon + 180.0) / 360.0);

    return new Vector2(x, y);
    }

    float GetPlayerHeading()
    {
    if (FlightGlobals.ActiveVessel == null)
    {
    return 0f;
    }

    Vessel vessel = FlightGlobals.ActiveVessel;

    Vector3d up = vessel.mainBody.GetSurfaceNVector(vessel.latitude, vessel.longitude);
    Vector3d north = vessel.mainBody.GetSurfaceNVector(vessel.latitude + 0.01, vessel.longitude) - up;
    north = north.normalized;

    Vector3d vesselForward = vessel.GetTransform().up;
    Vector3d surfaceForward = Vector3d.Exclude(up, vesselForward).normalized;

    double angle = Vector3d.Angle(north, surfaceForward);

    Vector3d cross = Vector3d.Cross(north, surfaceForward);
    if (Vector3d.Dot(cross, up) < 0)
    {
    angle = 360.0 - angle;
    }

    return (float)angle;
    }

    void Update()
    {
    if (textureNeedsApply)
    {
    applyTimer += Time.deltaTime;
    if (applyTimer >= 0.05f)
    {
    drawableTextures[currentMapIndex].Apply(false);
    textureNeedsApply = false;
    applyTimer = 0f;
    }
    }

    if (needsSave)
    {
    autoSaveTimer += Time.deltaTime;
    if (autoSaveTimer >= autoSaveInterval)
    {
    SaveDrawing(currentMapIndex);
    SaveTextLabels();
    SaveUndoHistory();
    needsSave = false;
    autoSaveTimer = 0f;
    }
    }
    }

    void OnDestroy()
    {
    GameEvents.onGUIApplicationLauncherReady.Remove(OnAppLauncherReady);

    if (toolbarButton != null)
    {
    ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
    }

    if (textureNeedsApply)
    {
    drawableTextures[currentMapIndex].Apply(false);
    }

    if (needsSave)
    {
    SaveDrawing(currentMapIndex);
    SaveTextLabels();
    SaveUndoHistory();
    }
    }

    void OnToolbarButtonToggle()
    {
    if (showWindow)
    {
    if (textureNeedsApply)
    {
    drawableTextures[currentMapIndex].Apply(false);
    textureNeedsApply = false;
    }

    if (needsSave)
    {
    SaveDrawing(currentMapIndex);
    SaveTextLabels();
    SaveUndoHistory();
    needsSave = false;
    autoSaveTimer = 0f;
    }
    }
    showWindow = !showWindow;
    }

    void OnGUI()
    {
    if (showWindow && drawableTextures[currentMapIndex] != null)
    {
    windowRect = GUI.Window(12345, windowRect, DrawMapWindow, "Map");
    }
    }

    void SaveDrawing(int mapIndex)
    {
    if (drawableTextures[mapIndex] == null)
    {
    return;
    }

    try
    {
    Color32[] pixels = drawableTextures[mapIndex].GetPixels32();

    using (FileStream fs = new FileStream(drawingPaths[mapIndex], FileMode.Create, FileAccess.Write, FileShare.None, 65536))
    using (BinaryWriter writer = new BinaryWriter(fs))
    {
    writer.Write(drawableTextures[mapIndex].width);
    writer.Write(drawableTextures[mapIndex].height);
    writer.Write(pixels.Length);

    byte[] buffer = new byte[pixels.Length * 4];
    for (int i = 0; i < pixels.Length; i++)
    {
    int idx = i * 4;
    buffer[idx] = pixels[i].r;
    buffer[idx + 1] = pixels[i].g;
    buffer[idx + 2] = pixels[i].b;
    buffer[idx + 3] = pixels[i].a;
    }
    writer.Write(buffer);
    }

    Debug.Log("[MapDisplay] Drawing saved for map " + mapIndex);
    }
    catch (Exception e)
    {
    Debug.LogError("[MapDisplay] Failed to save drawing: " + e.Message);
    }
    }

    void LoadDrawing(int mapIndex)
    {
    if (!File.Exists(drawingPaths[mapIndex]))
    {
    return;
    }

    try
    {
    using (FileStream fs = new FileStream(drawingPaths[mapIndex], FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
    using (BinaryReader reader = new BinaryReader(fs))
    {
    int width = reader.ReadInt32();
    int height = reader.ReadInt32();
    int pixelCount = reader.ReadInt32();

    if (width != drawableTextures[mapIndex].width || height != drawableTextures[mapIndex].height)
    {
    Debug.LogWarning("[MapDisplay] Cached drawing dimensions don't match. Skipping load.");
    return;
    }

    byte[] buffer = reader.ReadBytes(pixelCount * 4);
    Color32[] pixels = new Color32[pixelCount];

    for (int i = 0; i < pixelCount; i++)
    {
    int idx = i * 4;
    pixels[i] = new Color32(buffer[idx], buffer[idx + 1], buffer[idx + 2], buffer[idx + 3]);
    }

    drawableTextures[mapIndex].SetPixels32(pixels);
    drawableTextures[mapIndex].Apply(false);
    }

    Debug.Log("[MapDisplay] Drawing loaded for map " + mapIndex);
    }
    catch (Exception e)
    {
    Debug.LogError("[MapDisplay] Failed to load drawing: " + e.Message);
    }
    }

    void SaveTextLabels()
    {
    try
    {
    using (StreamWriter writer = new StreamWriter(textPaths[currentMapIndex]))
    {
    foreach (TextLabel label in textLabels)
    {
    writer.WriteLine(label.text);
    writer.WriteLine(label.position.x + "," + label.position.y);
    writer.WriteLine(label.color.r + "," + label.color.g + "," + label.color.b + "," + label.color.a);
    }
    }
    Debug.Log("[MapDisplay] Text labels saved: " + textLabels.Count);
    }
    catch (Exception e)
    {
    Debug.LogError("[MapDisplay] Failed to save text labels: " + e.Message);
    }
    }

    void LoadTextLabels()
    {
    textLabels.Clear();

    if (!File.Exists(textPaths[currentMapIndex]))
    {
    return;
    }

    try
    {
    using (StreamReader reader = new StreamReader(textPaths[currentMapIndex]))
    {
    while (!reader.EndOfStream)
    {
    string text = reader.ReadLine();
    if (string.IsNullOrEmpty(text)) break;

    string posLine = reader.ReadLine();
    string[] posData = posLine.Split(',');
    Vector2 pos = new Vector2(float.Parse(posData[0]), float.Parse(posData[1]));

    string colorLine = reader.ReadLine();
    string[] colorData = colorLine.Split(',');
    Color color = new Color(float.Parse(colorData[0]), float.Parse(colorData[1]), float.Parse(colorData[2]), float.Parse(colorData[3]));

    textLabels.Add(new TextLabel(text, pos, color));
    }
    }
    Debug.Log("[MapDisplay] Text labels loaded: " + textLabels.Count);
    }
    catch (Exception e)
    {
    Debug.LogError("[MapDisplay] Failed to load text labels: " + e.Message);
    textLabels.Clear();
    }
    }

    void SaveUndoHistory()
    {
    try
    {
    using (FileStream fs = new FileStream(undoPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
    using (BinaryWriter writer = new BinaryWriter(fs))
    {
    writer.Write(undoStack.Count);

    foreach (MapState state in undoStack)
    {
    writer.Write(state.pixels.Length);

    byte[] buffer = new byte[state.pixels.Length * 4];
    for (int i = 0; i < state.pixels.Length; i++)
    {
    int idx = i * 4;
    buffer[idx] = (byte)(state.pixels[i].r * 255);
    buffer[idx + 1] = (byte)(state.pixels[i].g * 255);
    buffer[idx + 2] = (byte)(state.pixels[i].b * 255);
    buffer[idx + 3] = (byte)(state.pixels[i].a * 255);
    }
    writer.Write(buffer);

    writer.Write(state.labels.Count);
    foreach (TextLabel label in state.labels)
    {
    writer.Write(label.text);
    writer.Write(label.position.x);
    writer.Write(label.position.y);
    writer.Write(label.color.r);
    writer.Write(label.color.g);
    writer.Write(label.color.b);
    writer.Write(label.color.a);
    }
    }
    }
    Debug.Log("[MapDisplay] Undo history saved: " + undoStack.Count + " states");
    }
    catch (Exception e)
    {
    Debug.LogError("[MapDisplay] Failed to save undo history: " + e.Message);
    }
    }

    void LoadUndoHistory()
    {
    if (!File.Exists(undoPath))
    {
    return;
    }

    try
    {
    using (FileStream fs = new FileStream(undoPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
    using (BinaryReader reader = new BinaryReader(fs))
    {
    int stateCount = reader.ReadInt32();
    undoStack.Clear();

    for (int i = 0; i < stateCount; i++)
    {
    int pixelCount = reader.ReadInt32();
    byte[] buffer = reader.ReadBytes(pixelCount * 4);
    Color[] pixels = new Color[pixelCount];

    for (int j = 0; j < pixelCount; j++)
    {
    int idx = j * 4;
    pixels[j] = new Color(buffer[idx] / 255f, buffer[idx + 1] / 255f, buffer[idx + 2] / 255f, buffer[idx + 3] / 255f);
    }

    int labelCount = reader.ReadInt32();
    List<TextLabel> labels = new List<TextLabel>();
    for (int j = 0; j < labelCount; j++)
    {
    string text = reader.ReadString();
    float px = reader.ReadSingle();
    float py = reader.ReadSingle();
    float cr = reader.ReadSingle();
    float cg = reader.ReadSingle();
    float cb = reader.ReadSingle();
    float ca = reader.ReadSingle();
    labels.Add(new TextLabel(text, new Vector2(px, py), new Color(cr, cg, cb, ca)));
    }

    undoStack.Add(new MapState(pixels, labels));
    }
    }
    Debug.Log("[MapDisplay] Undo history loaded: " + undoStack.Count + " states");
    }
    catch (Exception e)
    {
    Debug.LogError("[MapDisplay] Failed to load undo history: " + e.Message);
    undoStack.Clear();
    }
    }

    void SaveUndoStateImmediate()
    {
    MapState state = new MapState(drawableTextures[currentMapIndex].GetPixels(), textLabels);
    undoStack.Add(state);
    if (undoStack.Count > maxUndoSteps)
    {
    undoStack.RemoveAt(0);
    }

    SaveDrawing(currentMapIndex);
    SaveTextLabels();
    SaveUndoHistory();
    }

    void SaveUndoStateDeferred()
    {
    MapState state = new MapState(drawableTextures[currentMapIndex].GetPixels(), textLabels);
    undoStack.Add(state);
    if (undoStack.Count > maxUndoSteps)
    {
    undoStack.RemoveAt(0);
    }

    needsSave = true;
    autoSaveTimer = 0f;
    }

    void Undo()
    {
    if (undoStack.Count > 1)
    {
    undoStack.RemoveAt(undoStack.Count - 1);
    MapState state = undoStack[undoStack.Count - 1];
    drawableTextures[currentMapIndex].SetPixels(state.pixels);
    drawableTextures[currentMapIndex].Apply(false);

    textLabels.Clear();
    foreach (TextLabel label in state.labels)
    {
    textLabels.Add(new TextLabel(label.text, label.position, label.color));
    }

    needsSave = true;
    autoSaveTimer = 0f;
    }
    }

    void SwitchMap(int newIndex)
    {
    if (textureNeedsApply)
    {
    drawableTextures[currentMapIndex].Apply(false);
    textureNeedsApply = false;
    }

    if (needsSave)
    {
    SaveDrawing(currentMapIndex);
    SaveTextLabels();
    SaveUndoHistory();
    needsSave = false;
    autoSaveTimer = 0f;
    }

    currentMapIndex = newIndex;
    LoadTextLabels();
    undoStack.Clear();
    SaveUndoStateImmediate();
    }

    void DrawMapWindow(int id)
    {
    if (GUI.Button(new Rect(windowRect.width - 25, 5, 20, 20), "X"))
    {
    if (textureNeedsApply)
    {
    drawableTextures[currentMapIndex].Apply(false);
    textureNeedsApply = false;
    }

    if (needsSave)
    {
    SaveDrawing(currentMapIndex);
    SaveTextLabels();
    SaveUndoHistory();
    needsSave = false;
    autoSaveTimer = 0f;
    }
    showWindow = false;
    }

    float x = 10f;
    float y = 30f;
    float h = 22f;

    // Row 1: Map switching, Zoom, Draw, Text, Player, Colors
    if (maxMapIndex > 0)
    {
    if (GUI.Button(new Rect(x, y, 26, h), "<"))
    {
    if (currentMapIndex > 0)
    {
    SwitchMap(currentMapIndex - 1);
    }
    }
    x += 30;

    GUI.Label(new Rect(x, y, 50, h), "Map " + (currentMapIndex + 1));
    x += 54;

    if (GUI.Button(new Rect(x, y, 26, h), ">"))
    {
    if (currentMapIndex < maxMapIndex)
    {
    SwitchMap(currentMapIndex + 1);
    }
    }
    x += 34;
    }

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
    bool newDrawing = GUI.Toggle(new Rect(x, y, 70, h), isDrawing, "Enable");
    if (newDrawing != isDrawing)
    {
    isDrawing = newDrawing;
    if (isDrawing) isTextMode = false;
    }
    x += 76;

    GUI.Label(new Rect(x, y, 40, h), "Text:");
    x += 38;
    bool newTextMode = GUI.Toggle(new Rect(x, y, 70, h), isTextMode, "Enable");
    if (newTextMode != isTextMode)
    {
    isTextMode = newTextMode;
    if (isTextMode) isDrawing = false;
    }
    x += 76;

    showPlayerPosition = GUI.Toggle(new Rect(x, y, 60, h), showPlayerPosition, "Player");
    x += 66;

    showOrbitLines = GUI.Toggle(new Rect(x, y, 60, h), showOrbitLines, "Orbit");
    x += 66;

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

    // Row 2: Undo, Clear, Save, Size/Text Input
    x = 10f;
    y += 26f;

    if (GUI.Button(new Rect(x, y, 58, h), "Undo"))
    {
    Undo();
    }
    x += 62;

    if (GUI.Button(new Rect(x, y, 58, h), "Clear"))
    {
    drawableTextures[currentMapIndex].SetPixels(mapTextures[currentMapIndex].GetPixels());
    drawableTextures[currentMapIndex].Apply(false);
    textLabels.Clear();
    undoStack.Clear();
    SaveUndoStateImmediate();
    }
    x += 62;

    if (GUI.Button(new Rect(x, y, 58, h), "Save"))
    {
    if (textureNeedsApply)
    {
    drawableTextures[currentMapIndex].Apply(false);
    textureNeedsApply = false;
    }
    SaveDrawing(currentMapIndex);
    SaveTextLabels();
    SaveUndoHistory();
    needsSave = false;
    autoSaveTimer = 0f;
    }
    x += 68;

    if (isTextMode)
    {
    GUI.Label(new Rect(x, y, 40, h), "Text:");
    x += 40;
    textInput = GUI.TextField(new Rect(x, y, 250, h), textInput, 50);
    }
    else
    {
    GUI.Label(new Rect(x, y, 36, h), "Size:");
    x += 36;
    uiBrushSize = (int)GUI.HorizontalSlider(new Rect(x, y + 4, 200, 16), uiBrushSize, 1, 15);
    x += 210;
    GUI.Label(new Rect(x, y, 40, h), uiBrushSize.ToString());
    }

    float topMargin = 82f;
    Rect scrollViewRect = new Rect(10, topMargin, windowRect.width - 20, windowRect.height - topMargin - 10);
    Rect contentRect = new Rect(0, 0, (windowRect.width - 40) * zoomLevel, (windowRect.height - topMargin - 30) * zoomLevel);

    scrollPosition = GUI.BeginScrollView(scrollViewRect, scrollPosition, contentRect);

    Rect imageRect = GetAspectFitRect(new Rect(0, 0, contentRect.width, contentRect.height), drawableTextures[currentMapIndex].width, drawableTextures[currentMapIndex].height);
    GUI.DrawTexture(imageRect, drawableTextures[currentMapIndex], ScaleMode.StretchToFill, true);

    // Draw text labels
    foreach (TextLabel label in textLabels)
    {
    float labelX = imageRect.x + label.position.x * imageRect.width;
    float labelY = imageRect.y + label.position.y * imageRect.height;

    GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
    labelStyle.normal.textColor = label.color;
    labelStyle.fontStyle = FontStyle.Bold;

    GUI.Label(new Rect(labelX, labelY, 200, 25), label.text, labelStyle);
    }

    // Draw orbit lines
    if (showOrbitLines && IsInStableOrbit() && ShouldShowPlayerIcon())
    {
    List<Vector2> orbitPoints = CalculateOrbitPoints(100);

    if (orbitPoints.Count > 1)
    {
    for (int i = 0; i < orbitPoints.Count - 1; i++)
    {
    Vector2 p1 = orbitPoints[i];
    Vector2 p2 = orbitPoints[i + 1];

    float x1 = imageRect.x + p1.x * imageRect.width;
    float y1 = imageRect.y + p1.y * imageRect.height;
    float x2 = imageRect.x + p2.x * imageRect.width;
    float y2 = imageRect.y + p2.y * imageRect.height;

    // Handle wrapping around the map edges
    float dx = Mathf.Abs(x2 - x1);
    if (dx > imageRect.width * 0.5f)
    {
    continue; // Skip drawing line segments that wrap around
    }

    DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), new Color(0f, 1f, 1f, 0.8f), 2f);
    }
    }
    }

    if (showPlayerPosition && ShouldShowPlayerIcon() && arrowTexture != null)
    {
    Vector2 playerPos = GetPlayerMapPosition();
    float heading = GetPlayerHeading();

    float arrowX = imageRect.x + playerPos.x * imageRect.width;
    float arrowY = imageRect.y + playerPos.y * imageRect.height;

    float arrowSize = 24f;
    Rect arrowRect = new Rect(arrowX - arrowSize / 2, arrowY - arrowSize / 2, arrowSize, arrowSize);

    Matrix4x4 matrixBackup = GUI.matrix;
    GUIUtility.RotateAroundPivot(heading, new Vector2(arrowX, arrowY));
    GUI.DrawTexture(arrowRect, arrowTexture);
    GUI.matrix = matrixBackup;
    }

    if (isDrawing && Input.GetMouseButton(0) && imageRect.Contains(Event.current.mousePosition))
    {
    Vector2 mp = Event.current.mousePosition;
    float nx = (mp.x - imageRect.x) / imageRect.width;
    float ny = (mp.y - imageRect.y) / imageRect.height;

    int tx = Mathf.RoundToInt(nx * (drawableTextures[currentMapIndex].width - 1));
    int ty = Mathf.RoundToInt((1f - ny) * (drawableTextures[currentMapIndex].height - 1));

    int actualBrush = uiBrushSize + 3;
    DrawOnTexture(tx, ty, actualBrush);
    wasDrawing = true;
    }

    if (isTextMode && Event.current.type == EventType.MouseDown && Event.current.button == 0 && imageRect.Contains(Event.current.mousePosition) && !string.IsNullOrEmpty(textInput))
    {
    Vector2 mp = Event.current.mousePosition;
    float nx = (mp.x - imageRect.x) / imageRect.width;
    float ny = (mp.y - imageRect.y) / imageRect.height;

    textLabels.Add(new TextLabel(textInput, new Vector2(nx, ny), drawColor));
    SaveUndoStateDeferred();
    }

    if (wasDrawing && Event.current.type == EventType.MouseUp)
    {
    if (textureNeedsApply)
    {
    drawableTextures[currentMapIndex].Apply(false);
    textureNeedsApply = false;
    }
    SaveUndoStateDeferred();
    wasDrawing = false;
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
    // Scale brush size for Mun map (map index 1)
    float scaleFactor = 1.0f;
    if (currentMapIndex == 1)
    {
    // Mun map is 600x300, Kerbin map is 3840x1920
    // Scale factor = 600/3840 = 0.15625 (divide to make smaller)
    scaleFactor = 0.15625f;
    }

    int scaledRadius = Mathf.RoundToInt(brushRadius * scaleFactor);
    int r = scaledRadius;
    int r2 = r * r;

    for (int dy = -r; dy <= r; dy++)
    {
    int py = y + dy;
    if (py < 0 || py >= drawableTextures[currentMapIndex].height)
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
    if (px < 0 || px >= drawableTextures[currentMapIndex].width)
    {
    continue;
    }

    drawableTextures[currentMapIndex].SetPixel(px, py, drawColor);
    }
    }

    textureNeedsApply = true;
    }

    bool IsInStableOrbit()
    {
    if (FlightGlobals.ActiveVessel == null)
    {
    return false;
    }

    Vessel vessel = FlightGlobals.ActiveVessel;

    // Check if vessel has an orbit
    if (vessel.orbit == null)
    {
    return false;
    }

    // Check if periapsis is above the body's surface (not suborbital)
    double periapsis = vessel.orbit.PeA;
    if (periapsis < 0)
    {
    return false; // Suborbital - periapsis is below surface
    }

    // Check if orbit is closed (not escape trajectory)
    if (vessel.orbit.eccentricity >= 1.0)
    {
    return false; // Hyperbolic or parabolic trajectory
    }

    return true;
    }

    List<Vector2> CalculateOrbitPoints(int numPoints)
    {
    List<Vector2> points = new List<Vector2>();

    if (FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.orbit == null)
    {
    return points;
    }

    Orbit orbit = FlightGlobals.ActiveVessel.orbit;
    CelestialBody body = orbit.referenceBody;

    for (int i = 0; i <= numPoints; i++)
    {
    double meanAnomaly = (double)i / numPoints * 2.0 * Math.PI;

    // Get position at this point in the orbit
    Vector3d pos = orbit.getPositionFromMeanAnomaly(meanAnomaly);

    // Convert to lat/lon
    double lat = body.GetLatitude(pos);
    double lon = body.GetLongitude(pos);

    // Convert to map coordinates (0-1 range)
    float y = (float)((90.0 - lat) / 180.0);
    float x = (float)((lon + 180.0) / 360.0);

    points.Add(new Vector2(x, y));
    }

    return points;
    }

    void DrawLine(Vector2 pointA, Vector2 pointB, Color color, float width)
    {
    Matrix4x4 matrixBackup = GUI.matrix;

    float angle = Mathf.Atan2(pointB.y - pointA.y, pointB.x - pointA.x) * 180f / Mathf.PI;
    float length = Vector2.Distance(pointA, pointB);

    GUIUtility.RotateAroundPivot(angle, pointA);

    Color oldColor = GUI.color;
    GUI.color = color;
    GUI.DrawTexture(new Rect(pointA.x, pointA.y - width / 2, length, width), Texture2D.whiteTexture);
    GUI.color = oldColor;

    GUI.matrix = matrixBackup;
    }
    }
}