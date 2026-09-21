using UnityEngine;

public interface IEVVEnemyLaneWalker
{
    int LaneIndex { get; }
    EVVHealth Health { get; }
    GameObject gameObject { get; }
    // Mid-jump: melee swings miss him, projectiles still hit.
    bool IsDodgingMelee { get; }

    void BeginLaneWalk(int laneIndex, Vector3 startPosition, Vector3 endPosition, float speed, int maxHealth);
}
