#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Editor screenshot utility.
///   Tools &gt; Capture Scene View   (Ctrl/Cmd+Shift+K) → EXACTLY what you see in the
///       Scene panel: same framing/zoom (WYSIWYG), high-resolution render
///       (height 1080, width proportional to the panel → no "shrunk" look).
///   Tools &gt; Capture Game Camera 1920x1080  (Ctrl/Cmd+Shift+J) → the Main Camera at 1080p
///       (as seen in game).
///
/// The project is URP: it uses the SRP render request (camera.Render() would give a
/// black screen). Saves to &lt;Project&gt;/Screenshots/.
/// </summary>
public static class SceneViewCapture
{
    // Base height of the Scene view PNG; the width follows the panel's aspect ratio.
    private const int SCENE_HEIGHT = 1080;

    [MenuItem("Tools/Capture Scene View %#k")]
    public static void CaptureSceneView()
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null || sv.camera == null)
        {
            Debug.LogWarning("[SceneViewCapture] No active Scene view: click the Scene panel and try again.");
            return;
        }

        Camera cam = sv.camera;
        // The REAL aspect of the Scene panel: so the image keeps the same framing
        // you see (we don't force the aspect → no zoom-out).
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
            Debug.LogWarning("[SceneViewCapture] No Camera tagged 'MainCamera' in the scene.");
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
            // overrideAspect=true (Game cam): force 16:9. false (Scene): keep the
            // panel's aspect, already consistent with the width/height computed above.
            if (overrideAspect) cam.aspect = (float)width / height;

            // URP/HDRP: render through the pipeline (camera.Render() would give black).
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
            {
                cam.targetTexture = rt; // Built-in fallback
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

        Debug.Log($"[SceneViewCapture] {width}x{height} saved: {path}");
        EditorUtility.RevealInFinder(path);
    }
}
#endif
