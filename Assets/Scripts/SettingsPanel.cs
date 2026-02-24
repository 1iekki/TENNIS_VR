using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanel : MonoBehaviour
{
    public Slider volumeSlider;
    public TextMeshProUGUI volumeLabel;
    
    private const string VOLUME_KEY = "MasterVolume";

    private void Start()
    {
        // Load saved volume
        float volume = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);
        volumeSlider.value = volume;

        // Register listener
        volumeSlider.onValueChanged.AddListener(SetVolume);

        // Apply volume
        ApplyVolume(volume);
        UpdateLabel(volume);
    }

    public void SetVolume(float value)
    {
        PlayerPrefs.SetFloat(VOLUME_KEY, value);
        ApplyVolume(value);
        UpdateLabel(value);
    }

    private void UpdateLabel(float value)
    {
        if (volumeLabel != null)
            volumeLabel.text = $"Volume: {Mathf.RoundToInt(value * 100)}%";
    }

    private void ApplyVolume(float volume)
    {
        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            if (gm.hitSound) gm.hitSound.volume = volume;
            if (gm.superHitSound) gm.superHitSound.volume = volume;
            if (gm.softChime) gm.softChime.volume = volume;
            if (gm.voicePraise) gm.voicePraise.volume = volume;
            if (gm.calmMusic) gm.calmMusic.volume = volume;
            if (gm.introNarrationAudio) gm.introNarrationAudio.volume = volume;
        }
    }
}
