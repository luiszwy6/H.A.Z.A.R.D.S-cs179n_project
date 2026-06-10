public static class NightVisionEvents
{
    public static bool IsActive { get; private set; }

    public static event System.Action<bool> OnNightVisionChanged;

    public static void SetActive(bool active)
    {
        if (IsActive == active) return;
        IsActive = active;
        OnNightVisionChanged?.Invoke(active);
    }
}
