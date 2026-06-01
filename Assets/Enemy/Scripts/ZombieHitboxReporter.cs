using UnityEngine;

public class ZombieHitboxReporter : MonoBehaviour
{
    private ZombieAttackSetting owner;

    public void Setup(ZombieAttackSetting attackSetting)
    {
        owner = attackSetting;
    }

    private void OnTriggerEnter(Collider other)
    {
        owner?.OnHitboxContact(other);
    }
}
