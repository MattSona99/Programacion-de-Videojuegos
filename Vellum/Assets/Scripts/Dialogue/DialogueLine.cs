using UnityEngine;

/// <summary>A single dialogue line: who speaks and the text to display.</summary>
[System.Serializable]
public struct DialogueLine
{
    public string speaker;
    [TextArea(2, 5)] public string text;
}
