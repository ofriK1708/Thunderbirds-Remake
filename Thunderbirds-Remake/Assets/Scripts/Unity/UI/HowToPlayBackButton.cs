using UnityEngine;
using UnityEngine.EventSystems;

namespace Thunderbirds.Unity
{
    /// <summary>Uses the EventSystem Cancel action, including keyboard and gamepad.</summary>
    public sealed class HowToPlayBackButton : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private HowToPlayView screen;
        public void OnCancel(BaseEventData eventData)
        {
            screen.Close();
            eventData.Use();
        }
    }
}
