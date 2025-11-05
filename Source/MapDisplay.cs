using UnityEngine;
using KSP.UI.Screens;

namespace MapDisplay
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class MapDisplayMod : MonoBehaviour
    {
        private ApplicationLauncherButton toolbarButton;
        private bool showWindow = false;
        private Rect windowRect = new Rect(100, 100, 600, 400);
        private Texture2D mapTexture;
        private float zoomLevel = 1.0f;
        private Vector2 scrollPosition = Vector2.zero;
        
        void Start()
        {
            // Load your PNG map (place it in GameData/MicroMap/Textures/)
            mapTexture = GameDatabase.Instance.GetTexture("MicroMap/Textures/map", false);
            
            // Create toolbar button
            Texture2D buttonTexture = GameDatabase.Instance.GetTexture("MicroMap/Textures/icon", false);
            toolbarButton = ApplicationLauncher.Instance.AddModApplication(
                OnToolbarButtonToggle,  // onTrue
                OnToolbarButtonToggle,  // onFalse
                null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT,
                buttonTexture
            );
        }
        
        void OnToolbarButtonToggle()
        {
            showWindow = !showWindow;
        }
        
        void OnGUI()
        {
            if (showWindow && mapTexture != null)
            {
                windowRect = GUI.Window(12345, windowRect, DrawMapWindow, "Map");
            }
        }
        
        void DrawMapWindow(int windowID)
        {
            // Close button (X) in top-right corner
            if (GUI.Button(new Rect(windowRect.width - 25, 5, 20, 20), "X"))
            {
                showWindow = false;
            }
            
            // Zoom controls
            GUI.Label(new Rect(10, 30, 50, 20), "Zoom:");
            if (GUI.Button(new Rect(60, 30, 30, 20), "+"))
            {
                zoomLevel = Mathf.Min(zoomLevel + 0.25f, 5.0f);
            }
            if (GUI.Button(new Rect(95, 30, 30, 20), "-"))
            {
                zoomLevel = Mathf.Max(zoomLevel - 0.25f, 0.5f);
            }
            if (GUI.Button(new Rect(130, 30, 50, 20), "Reset"))
            {
                zoomLevel = 1.0f;
                scrollPosition = Vector2.zero;
            }
            
            // Scrollable area for zoomed map
            Rect scrollViewRect = new Rect(10, 60, windowRect.width - 20, windowRect.height - 70);
            Rect contentRect = new Rect(0, 0, (windowRect.width - 40) * zoomLevel, (windowRect.height - 90) * zoomLevel);
            
            scrollPosition = GUI.BeginScrollView(scrollViewRect, scrollPosition, contentRect);
            
            // Display the map image with zoom
            GUI.DrawTexture(new Rect(0, 0, contentRect.width, contentRect.height), mapTexture, ScaleMode.ScaleToFit);
            
            GUI.EndScrollView();
            
            // Make window draggable
            GUI.DragWindow(new Rect(0, 0, windowRect.width, 30));
        }
        
        void OnDestroy()
        {
            if (toolbarButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
            }
        }
    }
}