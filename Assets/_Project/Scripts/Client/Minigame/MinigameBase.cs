using System;
using UnityEngine;
 
public enum MinigameType
{
    Mash = 0,
    Driving = 1,
    Refuel = 2,
}
 
// Base for every minigame. Minigames are LOCAL: only the player doing it runs
// one, and only the result is sent to the server. That keeps them cheap and
// means new minigames need no networking code at all.
public abstract class MinigameBase : MonoBehaviour
{
    [SerializeField] private MinigameType _type;
    public MinigameType Type => _type;
 
    private Action _onSuccess;
    private Action _onCancel;
 
    protected int Difficulty { get; private set; }
 
    //called by MinigameHost. Difficulty means whatever the minigame wants
    //(for Mash it is the number of presses required).
    public void Begin(int difficulty, Action onSuccess, Action onCancel)
    {
        Difficulty = Mathf.Max(1, difficulty);
        _onSuccess = onSuccess;
        _onCancel = onCancel;
 
        gameObject.SetActive(true);
        OnBegin();
    }
 
    protected abstract void OnBegin();
 
    protected void Succeed()
    {
        Action cb = _onSuccess;
        Close();
        cb?.Invoke();
    }
 
    protected void Cancel()
    {
        Action cb = _onCancel;
        Close();
        cb?.Invoke();
    }
 
    private void Close()
    {
        _onSuccess = null;
        _onCancel = null;
        gameObject.SetActive(false);
    }
}
