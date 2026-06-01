using System.Collections.Generic;
using UnityEngine;

/// <summary>ScriptableObject holding an ordered list of <see cref="DialogueLine"/>s, played by the DialogueManager.</summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Vellum/Dialogue Asset")]
public class DialogueAsset : ScriptableObject
{
    public string dialogueId;
    public List<DialogueLine> lines;
}
