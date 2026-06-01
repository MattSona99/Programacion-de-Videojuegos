#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Utility Editor per gli screenshot.
//   Tools > Capture Scene View   (Ctrl/Cmd+Shift+K) → ESATTAMENTE ciò che vedi nel
//       pannello Scene: stessa inquadratura/zoom (WYSIWYG), reso ad alta risoluzione
//       (altezza 1080, larghezza proporzionale al pannello → niente "rimpicciolito").
//   Tools > Capture Game Camera 1920x1080  (Ctrl/Cmd+Shift+J) → la Main Camera a 1080p
//       (come si vede in game).
//
// Il progetto è URP: si usa la render request della SRP (camera.Render() darebbe
// schermo nero). Salva in <Progetto>/Screenshots/.
public static class SceneViewCapture
{
    // Altezza base del PNG della Scene view; la larghezza segue il rapporto del pannello.
    private const int SCENE_HEIGHT = 1080;

    [MenuItem("Tools/Capture Scene View %#k")]
    public static void CaptureSceneView()
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null || sv.camera == null)
        {
            Debug.LogWarning("[SceneViewCapture] Nessuna Scene view attiva: clicca il pannello Scene e riprova.");
            return;
        }

        Camera cam = sv.camera;
        // Rapporto REALE del pannello Scene: così l'immagine ha la stessa
        // inquadratura che vedi (non forziamo l'aspect → niente zoom-out).
        float aspect = cam.pixelHeight > 0 ? (float)cam.pixelWidth / cam.pixelHeight : 16f / 9f;
        int width = Mathf.Max(1, Mathf.RoundToInt(SCENE_HEIGHT * aspect));

        Capture(cam, width, SCENE_HEIGHT, overrideAspect: false, label: "SceneView");
    }

    [MenuItem("Tools/Capture Game Camera 1920x1080 %#j")]
    public static void CaptureGameCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[SceneViewCapture] Nessuna Camera con tag 'MainCamera' in scena.");
            return;
        }
        Capture(cam, 1920, 1080, overrideAspect: true, label: "GameCamera");
    }

    private static void Capture(Camera cam, int width, int height, bool overrideAspect, string label)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);

        float prevAspect = cam.aspect;
        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);

        try
        {
            // overrideAspect=true (Game cam): imponiamo 16:9. false (Scene): lasciamo
            // l'aspect del pannello, già coerente con width/height calcolati sopra.
            if (overrideAspect) cam.aspect = (float)width / height;

            // URP/HDRP: render attraverso la pipeline (camera.Render() darebbe nero).
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
            {
                cam.targetTexture = rt; // fallback Built-in
                cam.Render();
                cam.targetTexture = prevTarget;
            }

            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
        }
        finally
        {
            cam.aspect = prevAspect;
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{label}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);

        Debug.Log($"[SceneViewCapture] {width}x{height} salvato: {path}");
        EditorUtility.RevealInFinder(path);
    }
}
#endif
