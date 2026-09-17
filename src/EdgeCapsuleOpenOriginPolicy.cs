namespace PaperTodo;

internal static class EdgeCapsuleOpenOriginPolicy
{
    internal static bool ShouldMarkOpenedFromEdge(
        EdgeCapsuleSlotState currentSlot,
        EdgeCapsulePaperForm targetForm)
    {
        if (targetForm != EdgeCapsulePaperForm.Expanded)
        {
            return false;
        }

        return currentSlot is
            EdgeCapsuleSlotState.CollapsedDocked or
            EdgeCapsuleSlotState.RetractedCollapsed or
            EdgeCapsuleSlotState.RetractingCollapsed;
    }
}
