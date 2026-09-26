using UnityEngine;
using UnityEngine.EventSystems;

namespace Thunderbirds.Unity
{
    public sealed class OptionsCancelHandler : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private OptionsView screen;
        public void OnCancel(BaseEventData eventData)
        {
            screen.Close();
            eventData.Use();
        }
    }
}
