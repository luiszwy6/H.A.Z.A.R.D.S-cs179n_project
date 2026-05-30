using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Dead",
    story: "[Self] stays dead",
    category: "Enemy/State",
    id: "b7f2dfde0b2741f08f5e72533c86c441"
)]
public partial class EnemyDeadAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    [Header("Stop Movement")]
    [SerializeField] public bool StopNavMeshAgent = true;

    [Header("Stop Shooting")]
    [SerializeField] public bool AddShootLock = true;
    [SerializeField] public bool ClearWeaponRuntimeState = true;
    [SerializeField] public bool ClearAnimatorShooting = true;

    [Header("Disable Behavior Agent")]
    [SerializeField] public bool DisableBehaviorGraphAgent = false;

    [Header("Debug")]
    [SerializeField] public bool DebugLog = false;

    private NavMeshAgent navMeshAgent;
    private EnemyShootLockController shootLockController;
    private EnemyWeaponShooter weaponShooter;
    private EnemyAnimatorParameterDriver animatorDriver;
    private SquadMember squadMember;
    private Behaviour behaviorGraphAgent;

    private bool shootLockAdded;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        GameObject self = Self.Value;

        navMeshAgent = self.GetComponent<NavMeshAgent>();
        shootLockController = self.GetComponent<EnemyShootLockController>();
        weaponShooter = self.GetComponentInChildren<EnemyWeaponShooter>(true);
        animatorDriver = self.GetComponent<EnemyAnimatorParameterDriver>();
        squadMember = self.GetComponent<SquadMember>();
        behaviorGraphAgent = FindBehaviorGraphAgent(self);

        ApplyDeadState();

        if (DebugLog)
            Debug.Log("[EnemyDeadAction] Enter dead state.", self);

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        ApplyDeadState();

        return Status.Running;
    }

    protected override void OnEnd()
    {

    }

    private void ApplyDeadState()
    {
        if (StopNavMeshAgent)
            StopAgent();

        if (AddShootLock)
            ApplyShootLock();

        if (ClearWeaponRuntimeState && weaponShooter != null)
            weaponShooter.ForceClearRuntimeState();

        if (ClearAnimatorShooting && animatorDriver != null)
        {
            animatorDriver.SetShooting(false);
            animatorDriver.SetKeepShooting(false);
        }

        if (DisableBehaviorGraphAgent && behaviorGraphAgent != null)
            behaviorGraphAgent.enabled = false;

        if (squadMember != null)
            squadMember.MarkDead();
    }

    private void StopAgent()
    {
        if (navMeshAgent == null)
            return;

        if (!navMeshAgent.enabled)
            return;

        if (!navMeshAgent.isOnNavMesh)
            return;

        navMeshAgent.isStopped = true;
        navMeshAgent.ResetPath();
    }

    private void ApplyShootLock()
    {
        if (shootLockController != null && !shootLockAdded)
        {
            shootLockController.AddShootLock();
            shootLockAdded = true;
        }

        if (weaponShooter != null)
            weaponShooter.externalShootLock = true;
    }

    private Behaviour FindBehaviorGraphAgent(GameObject self)
    {
        if (self == null)
            return null;

        Behaviour[] behaviours = self.GetComponents<Behaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];

            if (behaviour == null)
                continue;

            Type type = behaviour.GetType();

            while (type != null)
            {
                if (type.Name == "BehaviorGraphAgent")
                    return behaviour;

                if (type.FullName == "Unity.Behavior.BehaviorGraphAgent")
                    return behaviour;

                type = type.BaseType;
            }
        }

        return null;
    }
}
