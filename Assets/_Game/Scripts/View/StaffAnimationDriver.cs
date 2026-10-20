using Kitchen.Core;
using UnityEngine;

namespace Kitchen.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StaffView))]
    public sealed class StaffAnimationDriver : MonoBehaviour
    {
        private static readonly int ActionParameter = Animator.StringToHash("Action");
        private StaffView staffView;
        private Animator boundAnimator;
        private bool supportsAction;

        private void LateUpdate()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (staffView == null)
            {
                staffView = GetComponent<StaffView>();
            }

            Animator animator = staffView.Animator;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            if (animator != boundAnimator)
            {
                boundAnimator = animator;
                supportsAction = false;
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                {
                    if (parameter.nameHash == ActionParameter && parameter.type == AnimatorControllerParameterType.Int)
                    {
                        supportsAction = true;
                        break;
                    }
                }
            }

            if (!supportsAction)
            {
                return;
            }

            var employee = staffView.Runtime;
            int action = employee != null && employee.IsMoving ? 1 : 0;
            var cooker = employee != null ? employee.AssignedCooker : null;
            if (action == 0 && cooker != null && cooker.IsActive &&
                cooker.ActiveStaffId == employee.Id && cooker.State == CookerState.Working &&
                cooker.Definition.Kind == CookerKind.ChoppingStation)
            {
                action = 2;
            }

            animator.SetInteger(ActionParameter, action);
        }
    }
}
