using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Update Enemy Health",
    story: "[Self] updates health data",
    category: "Enemy/Health",
    id: "a21f5e7d9b8a4e5e9c8c3a6b2f4d1190"
)]
public partial class UpdateEnemyHealthAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    [SerializeReference] public BlackboardVariable<bool> IsDead;
    [SerializeReference] public BlackboardVariable<float> CurrentHealth;
    [SerializeReference] public BlackboardVariable<float> BaseHealth;
    [SerializeReference] public BlackboardVariable<float> HealthPercent;
    [SerializeReference] public BlackboardVariable<int> ArmorLevel;

    [SerializeField] public bool DebugLog = false;

    private EnemyHealth enemyHealth;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        enemyHealth = Self.Value.GetComponent<EnemyHealth>();

        if (enemyHealth == null)
            enemyHealth = Self.Value.GetComponentInChildren<EnemyHealth>(true);

        if (enemyHealth == null)
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (enemyHealth == null)
            return Status.Failure;

        float baseHealth = Mathf.Max(0.0001f, enemyHealth.BaseHealth);
        float currentHealth = Mathf.Max(0f, enemyHealth.CurrentHealth);
        float healthPercent = Mathf.Clamp01(currentHealth / baseHealth);

        if (IsDead != null)
            IsDead.Value = enemyHealth.IsDead;

        if (CurrentHealth != null)
            CurrentHealth.Value = currentHealth;

        if (BaseHealth != null)
            BaseHealth.Value = enemyHealth.BaseHealth;

        if (HealthPercent != null)
            HealthPercent.Value = healthPercent;

        if (ArmorLevel != null)
            ArmorLevel.Value = enemyHealth.CurrentArmorLevel;

        if (DebugLog && Self != null && Self.Value != null)
        {
            Debug.Log(
                $"[UpdateEnemyHealth] HP={currentHealth}/{enemyHealth.BaseHealth}, " +
                $"Percent={healthPercent}, " +
                $"Armor={enemyHealth.CurrentArmorLevel}, " +
                $"IsDead={enemyHealth.IsDead}",
                Self.Value
            );
        }

        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}