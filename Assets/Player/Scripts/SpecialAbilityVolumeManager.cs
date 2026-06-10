using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class SpecialAbilityVolumeManager : MonoBehaviour
{
    [Header("Ability References")]
    [SerializeField] private AR_SpecialAbility arSpecialAbility;
    [SerializeField] private SG_SpecialAbility sgSpecialAbility;

    [Header("Volume")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private VolumeProfile normalProfile;
    [SerializeField] private VolumeProfile specialAbilityProfile;

    private bool wasSpecialAbilityActive;

    private void Awake()
    {
        if (arSpecialAbility == null)
            arSpecialAbility = FindFirstSceneObject<AR_SpecialAbility>();

        if (sgSpecialAbility == null)
            sgSpecialAbility = FindFirstSceneObject<SG_SpecialAbility>();
    }

    private void OnEnable()
    {
        wasSpecialAbilityActive = IsSpecialAbilityActive();
        ApplyVolumeProfile(wasSpecialAbilityActive);
    }

    private void OnDisable()
    {
        ApplyVolumeProfile(active: false);
    }

    private void Update()
    {
        bool isSpecialAbilityActive = IsSpecialAbilityActive();

        if (isSpecialAbilityActive == wasSpecialAbilityActive)
            return;

        wasSpecialAbilityActive = isSpecialAbilityActive;
        ApplyVolumeProfile(isSpecialAbilityActive);
    }

    private bool IsSpecialAbilityActive()
    {
        return
            arSpecialAbility != null && arSpecialAbility.IsActive ||
            sgSpecialAbility != null && sgSpecialAbility.IsActive;
    }

    private void ApplyVolumeProfile(bool active)
    {
        if (postProcessVolume == null)
            return;

        VolumeProfile target = active ? specialAbilityProfile : normalProfile;

        if (target != null)
            postProcessVolume.profile = target;
    }

    private static T FindFirstSceneObject<T>() where T : Object
    {
        T[] objects = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return objects.Length > 0 ? objects[0] : null;
    }
}
