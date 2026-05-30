using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Update Enemy Sensor",
    story: "[Self] updates enemy sensor data for [Target]",
    category: "Enemy/Sensor",
    id: "b6fc1d5ce2c84de59d7af651e0bb51a7"
)]
public partial class UpdateEnemySensorAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    [SerializeReference] public BlackboardVariable<bool> CanSeeTarget;
    [SerializeReference] public BlackboardVariable<float> DistanceToTarget;
    [SerializeReference] public BlackboardVariable<bool> HasLastKnownPosition;
    [SerializeReference] public BlackboardVariable<Vector3> LastKnownPosition;

    [SerializeField] public bool RefreshSensor = true;
    [SerializeField] public bool WriteLastKnownOnlyWhenValid = true;
    [SerializeField] public bool DebugLog = false;

    private EnemySensor sensor;
    private EnemyStatus enemyStatus;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        sensor = Self.Value.GetComponent<EnemySensor>();

        if (sensor == null)
            return Status.Failure;

        enemyStatus = Self.Value.GetComponent<EnemyStatus>();

        if (Target != null && Target.Value != null)
            sensor.SetTarget(Target.Value.transform);

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (sensor == null)
            return Status.Failure;

        if (Target != null && Target.Value != null)
            sensor.SetTarget(Target.Value.transform);

        if (RefreshSensor)
            sensor.RefreshSensor();

        if (CanSeeTarget != null)
            CanSeeTarget.Value = sensor.CanSeeTarget;

        if (enemyStatus != null)
            enemyStatus.SetCanSeeTarget(sensor.CanSeeTarget);

        if (DistanceToTarget != null)
            DistanceToTarget.Value = sensor.DistanceToTarget;

        if (HasLastKnownPosition != null)
            HasLastKnownPosition.Value = sensor.HasLastKnownPosition;

        if (LastKnownPosition != null)
        {
            if (!WriteLastKnownOnlyWhenValid || sensor.HasLastKnownPosition)
                LastKnownPosition.Value = sensor.LastKnownPosition;
        }

        if (DebugLog && Self != null && Self.Value != null)
        {
            Debug.Log(
                $"[UpdateEnemySensor] CanSee={sensor.CanSeeTarget}, " +
                $"HasLKP={sensor.HasLastKnownPosition}, " +
                $"LKP={sensor.LastKnownPosition}",
                Self.Value
            );
        }

        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}
