using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

public enum EnemyDistanceCompareMode
{
    Lower,
    LowerOrEqual,
    Greater,
    GreaterOrEqual,
    Equal,
    NotEqual
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "If Live Target Distance",
    story: "If[DistanceToTarget] from [Self] to [Target] is [CompareMode] than [Range]",
    category: "Enemy/Sensor",
    id: "a71f9b0dc8a24d7cbcb8f5c9e8b5192e"
)]
public partial class CheckTargetDistanceAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    [SerializeReference] public BlackboardVariable<float> DistanceToTarget;
    [SerializeReference] public BlackboardVariable<EnemyDistanceCompareMode> CompareMode;
    [SerializeReference] public BlackboardVariable<float> Range;
    [SerializeReference] public BlackboardVariable<float> EqualTolerance;

    [SerializeReference] public BlackboardVariable<bool> RefreshSensorBeforeCheck;
    [SerializeReference] public BlackboardVariable<bool> UseSensorDistanceIfAvailable;

    private EnemySensor sensor;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (Target == null || Target.Value == null)
            return Status.Failure;

        sensor = Self.Value.GetComponent<EnemySensor>();

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (Target == null || Target.Value == null)
            return Status.Failure;

        float liveDistance = GetLiveDistance();

        if (DistanceToTarget != null)
            DistanceToTarget.Value = liveDistance;

        float range = ResolveRange();
        float equalTolerance = ResolveEqualTolerance();

        switch (ResolveCompareMode())
        {
            case EnemyDistanceCompareMode.Lower:
                return liveDistance < range ? Status.Success : Status.Failure;

            case EnemyDistanceCompareMode.LowerOrEqual:
                return liveDistance <= range ? Status.Success : Status.Failure;

            case EnemyDistanceCompareMode.Greater:
                return liveDistance > range ? Status.Success : Status.Failure;

            case EnemyDistanceCompareMode.GreaterOrEqual:
                return liveDistance >= range ? Status.Success : Status.Failure;

            case EnemyDistanceCompareMode.Equal:
                return Mathf.Abs(liveDistance - range) <= equalTolerance
                    ? Status.Success
                    : Status.Failure;

            case EnemyDistanceCompareMode.NotEqual:
                return Mathf.Abs(liveDistance - range) > equalTolerance
                    ? Status.Success
                    : Status.Failure;

            default:
                return Status.Failure;
        }
    }

    protected override void OnEnd()
    {
    }

    private float GetLiveDistance()
    {
        if (sensor != null && ResolveRefreshSensorBeforeCheck())
            sensor.RefreshSensor();

        if (sensor != null && ResolveUseSensorDistanceIfAvailable())
            return sensor.DistanceToTarget;

        return Vector3.Distance(
            Self.Value.transform.position,
            Target.Value.transform.position
        );
    }

    private EnemyDistanceCompareMode ResolveCompareMode()
    {
        if (CompareMode == null)
            return EnemyDistanceCompareMode.Lower;

        return CompareMode.Value;
    }

    private float ResolveRange()
    {
        if (Range == null)
            return 5f;

        return Range.Value;
    }

    private float ResolveEqualTolerance()
    {
        if (EqualTolerance == null)
            return 0.05f;

        return Mathf.Max(0f, EqualTolerance.Value);
    }

    private bool ResolveRefreshSensorBeforeCheck()
    {
        if (RefreshSensorBeforeCheck == null)
            return true;

        return RefreshSensorBeforeCheck.Value;
    }

    private bool ResolveUseSensorDistanceIfAvailable()
    {
        if (UseSensorDistanceIfAvailable == null)
            return true;

        return UseSensorDistanceIfAvailable.Value;
    }
}