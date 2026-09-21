using UnityEngine;

public interface IEVVEnemyLaneWalker
{
    int LaneIndex { get; }
    EVVHealth Health { get; }
    GameObject gameObject { get; }
    // Mid-jump: melee swings miss him, projectiles still hit.
    bool IsDodgingMelee { get; }

    void BeginLaneWalk(int laneIndex, Vector3 startPosition, Vector3 endPosition, float speed, int maxHealth);
    // Holds him still (no walking, no fighting) for a while, e.g. while something else moves him.
    void PauseWalk(float seconds);
    // Taken out of the fight alive (fallen into a hole): lets go of the lure, the way his death does.
    void LetGo();
}
