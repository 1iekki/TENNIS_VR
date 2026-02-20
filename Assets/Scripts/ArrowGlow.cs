using System;
using UnityEngine;

public class ArrowAffordanceGlow : MonoBehaviour
{
    [Serializable]
    public struct Part
    {
        [Tooltip("Renderer for this part (e.g., Shaft mesh renderer or Head mesh renderer).")]
        public Renderer renderer;

        [Tooltip("Material slot index on the renderer to write the property block to (matches 'Material Index' in Material Property Block Helper).")]
        public int materialIndex;

        [Tooltip("Shader Graph property used for the rim/glow color (your setup shows _RimColor).")]
        public string rimColorProperty;

        [Tooltip("Shader Graph property used for the rim/glow strength/power (your setup shows _RimPower).")]
        public string rimPowerProperty;
    }

    [Header("Assign both parts (shaft + head)")]
    [SerializeField]
    private Part[] parts = new Part[2]
    {
        new Part { materialIndex = 0, rimColorProperty = "_RimColor", rimPowerProperty = "_RimPower" },
        new Part { materialIndex = 0, rimColorProperty = "_RimColor", rimPowerProperty = "_RimPower" }
    };

    [Header("Glow look")]
    [SerializeField] private Color glowColor = Color.cyan;
    [SerializeField] private float glowPower = 2.5f;

    [SerializeField] private Color offColor = Color.black;
    [SerializeField] private float offPower = 0f;

    private MaterialPropertyBlock _mpb;

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    public void SetGlow(bool on)
    {
        var c = on ? glowColor : offColor;
        var p = on ? glowPower : offPower;
        Apply(c, p);
    }

    public void Apply(Color rimColor, float rimPower)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (!part.renderer) continue;

            // Use the overload with materialIndex so head/shaft can be controlled independently.
            part.renderer.GetPropertyBlock(_mpb, part.materialIndex);

            _mpb.SetColor(part.rimColorProperty, rimColor);
            _mpb.SetFloat(part.rimPowerProperty, rimPower);

            part.renderer.SetPropertyBlock(_mpb, part.materialIndex);
        }
    }

    // Optional convenience methods
    public void GlowOn() => SetGlow(true);
    public void GlowOff() => SetGlow(false);
}