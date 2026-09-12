using UnityEngine;
using PrimeTween;

public static class EVVTween
{
    public static Tween TweenTo(this Transform transform, Vector3 position, float duration = 0.5f, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart)
    {
        return Tween.Position(transform, new TweenSettings<Vector3>(position, duration, cycles: cycles, cycleMode: cycleMode));
    }
}
