using UnityEngine;

public struct GrenadeEffectContext
{
    public GrenadeWorldController Grenade;
    public GameObject Owner;
    public Transform OwnerRoot;
    public Vector3 Position;
    public Vector3 Velocity;
}

public abstract class GrenadeWorldEffect : MonoBehaviour
{
    public virtual void OnGrenadeLaunched(GrenadeEffectContext context)
    {
    }

    public abstract void ActivateEffect(GrenadeEffectContext context);
}