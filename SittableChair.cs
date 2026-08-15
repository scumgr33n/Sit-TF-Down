using SteamShelf.Placeables;
using UnityEngine;

namespace SitTFDown
{
    public class SittableChair : MonoBehaviour, IInteractable
    {
        public void OnInteract() { }

        public void OnAlternateInteract()
        {
            Core.Instance.SitDown(transform);
        }
    }
}