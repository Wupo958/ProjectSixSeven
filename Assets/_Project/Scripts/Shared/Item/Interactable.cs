using UnityEngine;
 
//base for anything the player can look at and press E on.
//needs a collider on the same object (or a parent) to be hit by the raycast.
public abstract class Interactable : MonoBehaviour
{
    //shown on screen while looked at. Return null/empty to hide the prompt.
    public abstract string GetPrompt(PlayerInteractor player);
 
    //can the player act on this right now? Keeps the prompt honest.
    public virtual bool CanInteract(PlayerInteractor player) => true;
 
    public abstract void Interact(PlayerInteractor player);
}
