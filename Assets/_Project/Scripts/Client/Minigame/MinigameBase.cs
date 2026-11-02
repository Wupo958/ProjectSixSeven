using System;
using UnityEngine;
 
public enum MinigameType
{
    Mash = 0,
    Driving = 1,
    Refuel = 2,
}
 
public abstract class MinigameBase : MonoBehaviour
{
    [SerializeField] private MinigameType _type;
    public MinigameType Type => _type;
 
    private Action _onSuccess;
    private Action _onCancel;
    private Action _onFail;
 
    protected int Difficulty { get; private set; }
 
    public void Begin(int difficulty, Action onSuccess, Action onCancel, Action onFail = null)
    {
        Difficulty = Mathf.Max(1, difficulty);
        _onSuccess = onSuccess;
        _onCancel = onCancel;
        _onFail = onFail;
 
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

    protected void Fail()
    {
        Action cb = _onFail;
        Close();
        cb?.Invoke();
    }
 
    private void Close()
    {
        _onSuccess = null;
        _onCancel = null;
        _onFail = null;
        gameObject.SetActive(false);
    }
}
