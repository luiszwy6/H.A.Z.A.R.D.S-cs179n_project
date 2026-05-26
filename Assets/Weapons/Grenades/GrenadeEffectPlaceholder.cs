using UnityEngine;

public class GrenadeDamageRange : GrenadeWorldEffect
{
    [SerializeField] private bool logActivate = true;

    public override void ActivateEffect(GrenadeEffectContext context)
    {
        if (logActivate)
            Debug.Log($"[GrenadeDamageRange] Activate at {context.Position}", this);
    }
}

public class GrenadeImpact : GrenadeWorldEffect
{
    [SerializeField] private bool logActivate = true;

    public override void ActivateEffect(GrenadeEffectContext context)
    {
        if (logActivate)
            Debug.Log($"[GrenadeImpact] Activate at {context.Position}", this);
    }
}

public class FlashBangBlind : GrenadeWorldEffect
{
    [SerializeField] private bool logActivate = true;

    public override void ActivateEffect(GrenadeEffectContext context)
    {
        if (logActivate)
            Debug.Log($"[FlashBangBlind] Activate at {context.Position}", this);
    }
}

public class SmokeBlind : GrenadeWorldEffect
{
    [SerializeField] private bool logActivate = true;

    public override void ActivateEffect(GrenadeEffectContext context)
    {
        if (logActivate)
            Debug.Log($"[SmokeBlind] Activate at {context.Position}", this);
    }
}