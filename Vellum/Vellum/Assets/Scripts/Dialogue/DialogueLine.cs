using UnityEngine;

[System.Serializable]
public struct DialogueLine
{
    public string speaker;
    [TextArea(2, 5)] public string text;
}
