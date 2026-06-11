using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class SpecialAbilityVolumeManager : MonoBehaviour
{
    [Header("Ability References")]
    [SerializeField] private AR_SpecialAbility arSpecialAbility;
    [SerializeField] private SG_SpecialAbility sgSpecialAbility;
    [SerializeField] private SRSpecialAbility  srSpecialAbility;
    [SerializeField] private BulletTimeAbility bulletTimeAbility;

    [Header("Volume")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private VolumeProfile normalProfile;
    [SerializeField] private VolumeProfile specialAbilityProfile;
    [SerializeField] private VolumeProfile bulletTimeProfile;

    // True when AR or SG special ability is active (highest volume priority).
    public bool IsSpecialAbilityActive =>
        (arSpecialAbility != null && arSpecialAbility.IsActive) ||
        (sgSpecialAbility != null && sgSpecialAbility.IsActive);

    private VolumeProfile lastAppliedProfile;

    private void Awake()
    {
        if (arSpecialAbility  == null) arSpecialAbility  = FindFirstSceneObject<AR_SpecialAbility>();
        if (sgSpecialAbility  == null) sgSpecialAbility  = FindFirstSceneObject<SG_SpecialAbility>();
        if (srSpecialAbility  == null) srSpecialAbility  = FindFirstSceneObject<SRSpecialAbility>();
        if (bulletTimeAbility == null) bulletTimeAbility = FindFirstSceneObject<BulletTimeAbility>();
    }

    private void OnEnable()  => RefreshVolumeProfile();
    private void OnDisable() => ApplyProfile(normalProfile);

    private void Update() => RefreshVolumeProfile();

    private void RefreshVolumeProfile()
    {
        VolumeProfile target = ResolveActiveProfile();
        if (target == lastAppliedProfile) return;
        ApplyProfile(target);
    }

    // Priority: AR/SG SA > SR SA / BT > normal
    private VolumeProfile ResolveActiveProfile()
    {
        // Highest: AR or SG special ability
        if (IsSpecialAbilityActive)
            return specialAbilityProfile ?? normalProfile;

        // Second: SR special ability or regular bullet time
        if ((srSpecialAbility  != null && srSpecialAbility.IsActive) ||
            (bulletTimeAbility != null && bulletTimeAbility.IsActive))
            return bulletTimeProfile ?? normalProfile;

        return normalProfile;
    }

    private void ApplyProfile(VolumeProfile profile)
    {
        if (postProcessVolume == null || profile == null) return;
        postProcessVolume.profile = profile;
        lastAppliedProfile = profile;
    }

    private static T FindFirstSceneObject<T>() where T : Object
    {
        T[] objects = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return objects.Length > 0 ? objects[0] : null;
    }
}
