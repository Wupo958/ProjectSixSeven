using System;
using System.Collections.Generic;
using UnityEngine;
 
[CreateAssetMenu(menuName = "PCCMT/Handbook Page", fileName = "Handbook_")]
public sealed class HandbookPage : ScriptableObject
{
    [Tooltip("Shown across the top of the double page.")]
    public string Title = "New Page";
 
    [Tooltip("Left-hand page, top to bottom.")]
    public List<HandbookSection> Left = new List<HandbookSection>();
 
    [Tooltip("Right-hand page, top to bottom.")]
    public List<HandbookSection> Right = new List<HandbookSection>();
}
 
[Serializable]
public sealed class HandbookSection
{
    [Tooltip("Bold line above the text. Optional.")]
    public string Heading;
 
    [Tooltip("Optional picture, scaled to the page width.")]
    public Sprite Image;
 
    [Tooltip("Body text. Keys in square brackets like [E], [Space] or [LMB] are highlighted automatically. Rich text such as <b>bold</b> and <i>italic</i> works too.")]
    [TextArea(3, 12)]
    public string Body;
 
    [Tooltip("Optional key list, shown as aligned 'Key   Action' rows.")]
    public List<HandbookControl> Controls = new List<HandbookControl>();
}
 
[Serializable]
public sealed class HandbookControl
{
    [Tooltip("Key name without brackets, e.g. E, Space, LMB, W / S.")]
    public string Key = "E";
 
    [Tooltip("What the key does.")]
    public string Action = "Interact";
}
