using UnityEngine;
using UnityEditor;

public class ScreenshotTool
{
    // Questo comando aggiunge un nuovo bottone al menu in alto di Unity!
    [MenuItem("Tools/📸 Scatta Screenshot 1080p")]
    public static void TakeScreenshot()
    {
        // Genera un nome unico basato sulla data e l'ora esatta
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = "Screenshot_" + timestamp + ".png";
        
        // Scatta la foto! (Prende la risoluzione attuale della finestra Game)
        ScreenCapture.CaptureScreenshot(fileName);
        
        // Ti avvisa nella Console
        Debug.Log("✅ Screenshot salvato con successo: " + fileName);
    }
}