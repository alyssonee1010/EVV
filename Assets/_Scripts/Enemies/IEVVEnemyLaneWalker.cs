using UnityEngine;

public interface IEVVEnemyLaneWalker
{
    int LaneIndex { get; }
    EVVHealth Health { get; }
    GameObject gameObject { get; }

    void BeginLaneWalk(int laneIndex, Vector3 startPosition, Vector3 endPosition, float speed, int maxHealth);
}
