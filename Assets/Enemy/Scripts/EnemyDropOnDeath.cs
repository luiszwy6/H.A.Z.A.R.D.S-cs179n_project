using UnityEngine;
using Pickups;

[RequireComponent(typeof(EnemyHealth), typeof(PickupDropper))]
public class EnemyDropOnDeath : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<EnemyHealth>().onDeath.AddListener(GetComponent<PickupDropper>().TryDrop);
    }
}
