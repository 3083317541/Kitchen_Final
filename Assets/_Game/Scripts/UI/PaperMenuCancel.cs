using UnityEngine;
using UnityEngine.EventSystems;

namespace Kitchen.UI
{
    public sealed class PaperMenuCancel : MonoBehaviour, ICancelHandler
    {
        public void OnCancel(BaseEventData data)
        {
            if (transform.parent != null)
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, data, ExecuteEvents.cancelHandler);
        }
    }
}
