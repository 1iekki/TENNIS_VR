using Mono.Cecil;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum ScenarioType { S1, S2, S3, S4, S5 }

    [Header("Scenario Settings")]
    public ScenarioType currentScenario = ScenarioType.S1;

    [Header("Ball Settings")]
    public GameObject ballPrefab;
    public Transform ballSpawnPoint;
    public Transform ballSpawnPointLeft;
    public Transform ballSpawnPointRight;
    public Transform PlayerPos;
    public float minSpeed = 5f;
    public float maxSpeed = 5f;
    public float speedIncrement = 0.0f;
    private float currentSpeed;

    [Header("Game Settings")]
    public float spawnDelay = 3f;
    public float gameTime = 180f; // 3 minutes
    private float timeRemaining;
    public bool enableWaveMode = false;
    private float waveTimer = 0f;

    [Header("Difficulty Adaptation")]
    public int missesBeforeSimplify = 2;
    private int consecutiveMisses = 0;
    private Queue<GameObject> activeBalls = new Queue<GameObject>();

    [Header("UI Elements (Parents that have RawImage + TextMeshProUGUI child)")]
    public GameObject scoreTextObject;
    public GameObject speedLevelTextObject;
    public GameObject timerTextObject;
    public GameObject introductionTextObject;
    public GameObject feedbackTextObject;
    public GameObject BackToMenu;

    public AudioSource introNarrationAudio;
    public GameObject badgeNotification;
    public GameObject pauseMenu;
    public GameObject overstimulationOverlay;

    [Header("UI Icons (Prefabs to spawn)")]
    public GameObject smileyIconPrefab;
    public GameObject starIconPrefab;
    public Transform uiSpawnPoint;

    [Header("Audio")]
    public AudioSource hitSound;
    public AudioSource superHitSound;
    public AudioSource softChime;
    public AudioSource calmMusic;
    public AudioSource voicePraise;

    [Header("Visual Effects")]
    public GameObject sparkleEffect;
    public GameObject starEffect;
    public GameObject glowRacketEffect;
    public Light sceneLight;

    [Header("Tracking")]
    public int correctHits = 0;
    public int fastBallHits = 0;
    public int totalBalls = 0;
    private bool isGameActive = true;
    private bool isPaused = false;
    private float inactivityTimer = 0f;
    private Vector3 lastPlayerPosition;

    [SerializeField] private ArrowAffordanceGlow arrowCenter;
    [SerializeField] private ArrowAffordanceGlow arrowLeft;
    [SerializeField] private ArrowAffordanceGlow arrowRight;

    private bool arrows = false;
    private ArrowAffordanceGlow currentActiveArrow = null;

    // Cached TMP components (resolved from the parent GameObjects)
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI speedLevelText;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI introductionText;
    private TextMeshProUGUI feedbackText;

    private bool isBackToMenuVisible = false;

    public void GlowCenter(bool on)
    {
        if (arrowCenter == null) return;
        if (currentActiveArrow != null && currentActiveArrow != arrowCenter)
            currentActiveArrow.SetAnimatedGlow(false);
        arrowCenter.SetGlow(on);
        if (on) currentActiveArrow = arrowCenter;
    }

    public void GlowLeft(bool on)
    {
        if (arrowLeft == null) return;
        if (currentActiveArrow != null && currentActiveArrow != arrowLeft)
            currentActiveArrow.SetAnimatedGlow(false);
        arrowLeft.SetGlow(on);
        if (on) currentActiveArrow = arrowLeft;
    }

    public void GlowRight(bool on)
    {
        if (arrowRight == null) return;
        if (currentActiveArrow != null && currentActiveArrow != arrowRight)
            currentActiveArrow.SetAnimatedGlow(false);
        arrowRight.SetGlow(on);
        if (on) currentActiveArrow = arrowRight;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;

        // Cache TMP components from their parents (works even if parents are inactive)
        scoreText = GetTextFromParent(scoreTextObject, nameof(scoreTextObject));
        speedLevelText = GetTextFromParent(speedLevelTextObject, nameof(speedLevelTextObject));
        timerText = GetTextFromParent(timerTextObject, nameof(timerTextObject));
        introductionText = GetTextFromParent(introductionTextObject, nameof(introductionTextObject));
        feedbackText = GetTextFromParent(feedbackTextObject, nameof(feedbackTextObject));

        currentSpeed = minSpeed;
        timeRemaining = gameTime;

        // Ensure initial UI state is correct even if scene starts mid-test
        ApplyUIState(introActive: true, gameplayUIActive: false);
        HideFeedbackImmediate();
    }

    private TextMeshProUGUI GetTextFromParent(GameObject parent, string fieldName)
    {
        if (parent == null) return null;

        TextMeshProUGUI tmp = parent.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp == null)
        {
            Debug.LogWarning($"GameManager: '{fieldName}' is set but no TextMeshProUGUI component was found in children of '{parent.name}'.");
        }
        return tmp;
    }

    private void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    /// Central place controlling visibility of Intro vs Gameplay HUD.
    /// (Does NOT touch pause/overstimulation overlays.)
    private void ApplyUIState(bool introActive, bool gameplayUIActive)
    {
        // Intro
        SetActiveSafe(introductionTextObject, introActive);

        // Gameplay HUD
        SetActiveSafe(scoreTextObject, gameplayUIActive);
        SetActiveSafe(speedLevelTextObject, gameplayUIActive);
        SetActiveSafe(timerTextObject, gameplayUIActive);
    }

    private bool IsIntroShowing()
    {
        // If there's an intro parent, trust that; otherwise fall back to TMP object.
        if (introductionTextObject != null) return introductionTextObject.activeSelf;
        return introductionText != null && introductionText.gameObject.activeSelf;
    }

    private void ShowFeedback(string message, float durationSeconds = 2f)
    {
        if (feedbackTextObject == null || feedbackText == null) return;

        StopCoroutine(nameof(HideFeedbackAfterDelay));
        feedbackText.text = message;
        feedbackTextObject.SetActive(true);
        StartCoroutine(HideFeedbackAfterDelay(durationSeconds));
    }

    private IEnumerator HideFeedbackAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        HideFeedbackImmediate();
    }

    private void HideFeedbackImmediate()
    {
        if (feedbackTextObject != null)
            feedbackTextObject.SetActive(false);
    }

    void Start()
    {
        if (arrowCenter != null && arrowLeft != null && arrowRight != null)
        {
            GlowCenter(false);
            GlowLeft(false);
            GlowRight(false);
            arrows = true;
        }

        // If we have intro text, show intro flow; otherwise start immediately with gameplay UI
        if (introductionText != null && introductionTextObject != null)
        {
            StartCoroutine(ShowIntroduction());
        }
        else
        {
            ApplyUIState(introActive: false, gameplayUIActive: true);
            StartGameplay();
        }
    }

    IEnumerator ShowIntroduction()
    {
        // Intro on, gameplay HUD off
        ApplyUIState(introActive: true, gameplayUIActive: false);

        // Play intro narration audio if available (separate from voice praise)
        if (introNarrationAudio != null)
        {
            introNarrationAudio.Play();
        }

        yield return new WaitForSeconds(8f);

        // Intro off, gameplay HUD on
        ApplyUIState(introActive: false, gameplayUIActive: true);

        StartGameplay();
    }

    void StartGameplay()
    {
        StartCoroutine(SpawnBalls());
        StartCoroutine(MonitorInactivity());
        UpdateUI();

        if (calmMusic != null)
            calmMusic.Play();
    }

    void Update()
    {
        // Handle pause input (Escape key, P key, or VR Menu button)
        bool pausePressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pausePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
#endif

#if ENABLE_INPUT_SYSTEM
        pausePressed = UnityEngine.InputSystem.Keyboard.current != null &&
                      (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame ||
                       UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame);
#endif

        // Check VR controller menu button (works with XR Toolkit)
        pausePressed = pausePressed || CheckVRMenuButtonDown();
        if (pausePressed) Debug.Log("Menu button detected");
        if (pausePressed)
        {
            // Only allow pausing after intro is gone
            if (isGameActive && !IsIntroShowing())
            {
                ToggleBackToMenu();
            }
        }

        if (!isGameActive || isPaused) return;

        // Update timer
        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0)
        {
            EndGame();
            return;
        }

        // Wave mode: speed increases, stabilizes, then increases again
        if (enableWaveMode)
        {
            waveTimer += Time.deltaTime;

            // 30s increasing, 20s stable, 30s increasing, then reset
            if (waveTimer < 30f)
            {
                // Phase 1: Increasing
                currentSpeed = Mathf.Lerp(minSpeed, maxSpeed * 0.6f, waveTimer / 30f);
            }
            else if (waveTimer < 50f)
            {
                // Phase 2: Stable
                currentSpeed = maxSpeed * 0.6f;
            }
            else if (waveTimer < 80f)
            {
                // Phase 3: Increasing again
                currentSpeed = Mathf.Lerp(maxSpeed * 0.6f, maxSpeed, (waveTimer - 50f) / 30f);
            }
            else
            {
                // Reset cycle
                waveTimer = 0f;
            }
        }

        UpdateUI();
    }

    IEnumerator SpawnBalls()
    {
        yield return new WaitForSeconds(2f); // Initial delay

        while (isGameActive && timeRemaining > 0)
        {
            if (!isPaused)
            {
                SpawnBall();
                totalBalls++;
            }
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    void SpawnBall()
    {
        if (arrows)
        {
            GlowCenter(false);
            GlowLeft(false);
            GlowRight(false);
        }

        int ran = Random.Range(1, 4);
        Vector3 spawnPos = ballSpawnPoint.position;
        if (ballSpawnPoint != null && ballSpawnPointLeft != null && ballSpawnPointRight != null)
        {
            if (ran == 1)
            {
                spawnPos = ballSpawnPoint.position;
                if (arrows)
                {
                    GlowCenter(true);
                }
            }
            else if (ran == 2)
            {
                spawnPos = ballSpawnPointLeft.position;
                if (arrows)
                {
                    GlowLeft(true);
                }
            }
            else if (ran == 3)
            {
                spawnPos = ballSpawnPointRight.position;
                if (arrows)
                {
                    GlowRight(true);
                }
            }
        }

        Vector3 PlPos = (PlayerPos.position);
        if (currentScenario != ScenarioType.S1 && currentScenario != ScenarioType.S2)
        {
            PlPos += Vector3.right * Random.Range(-1.0f, 1.0f);
        }

        Vector3 direction = (PlPos - spawnPos).normalized;

        GameObject ball = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
        EnhancedBallController bc = ball.GetComponent<EnhancedBallController>();

        if (bc != null)
        {
            float speedForThisBall = currentSpeed;
            if (currentScenario == ScenarioType.S5)
            {
                speedForThisBall = Random.Range(minSpeed, maxSpeed);
            }

            bc.speed = speedForThisBall;
            bc.direction = direction;

            // Enable trajectory guide ONLY if player has missed enough balls
            if (consecutiveMisses >= missesBeforeSimplify)
            {
                bc.EnableTrajectoryGuide(true);
            }
        }

        activeBalls.Enqueue(ball);

        // Clean up destroyed balls from queue
        while (activeBalls.Count > 0 && activeBalls.Peek() == null)
        {
            activeBalls.Dequeue();
        }
    }

    public void OnBallHit(float ballSpeed, Vector3 hitPosition, bool isQualityHit)
    {
        correctHits++;
        consecutiveMisses = 0;
        inactivityTimer = 0f;

        // Play hit sound
        if (hitSound != null)
            hitSound.Play();

        // ALWAYS show smiley for any correct hit
        if (smileyIconPrefab != null && uiSpawnPoint != null)
        {
            GameObject smiley = Instantiate(smileyIconPrefab);
            smiley.transform.SetParent(uiSpawnPoint.transform.parent, false);
            RectTransform rectTransform = smiley.GetComponent<RectTransform>();
            RectTransform spawnRect = uiSpawnPoint.GetComponent<RectTransform>();

            if (rectTransform != null && spawnRect != null)
            {
                // Add random spread so icons don't stack perfectly
                Vector2 randomOffset = new Vector2(Random.Range(-30f, 30f), Random.Range(-20f, 20f));
                rectTransform.anchoredPosition = spawnRect.anchoredPosition + randomOffset;
            }

            smiley.SetActive(true);
            Debug.Log("Smiley spawned!");
        }
        else
        {
            Debug.LogWarning($"Cannot spawn smiley - Prefab: {smileyIconPrefab != null}, SpawnPoint: {uiSpawnPoint != null}");
        }

        // Check if it's a fast ball (fastest speed)
        bool isFastBall = ballSpeed >= (maxSpeed - 1f);
        if (isFastBall)
        {
            fastBallHits++;

            // Show STAR icon for fastest speed hits
            if (starIconPrefab != null && uiSpawnPoint != null)
            {
                GameObject star = Instantiate(starIconPrefab);
                star.transform.SetParent(uiSpawnPoint.transform.parent, false);
                RectTransform rectTransform = star.GetComponent<RectTransform>();
                RectTransform spawnRect = uiSpawnPoint.GetComponent<RectTransform>();

                if (rectTransform != null && spawnRect != null)
                {
                    // Add random spread + offset upward
                    Vector2 randomOffset = new Vector2(Random.Range(-40f, 40f), Random.Range(0f, 30f));
                    rectTransform.anchoredPosition = spawnRect.anchoredPosition + randomOffset;
                }

                star.SetActive(true);
                Debug.Log("Star spawned!");
            }
            else
            {
                Debug.LogWarning($"Cannot spawn star - Prefab: {starIconPrefab != null}, SpawnPoint: {uiSpawnPoint != null}");
            }

            // Show star particle effect
            if (starEffect != null)
            {
                GameObject star = Instantiate(starEffect, hitPosition, Quaternion.identity);
                Destroy(star, 2f);
            }

            // Play super sound and voice praise
            if (superHitSound != null)
                superHitSound.Play();

            if (voicePraise != null && Random.value > 0.5f)
                voicePraise.Play();
        }

        // Show sparkle for quality hits
        if (isQualityHit && sparkleEffect != null)
        {
            GameObject sparkle = Instantiate(sparkleEffect, hitPosition, Quaternion.identity);
            Destroy(sparkle, 1f);
        }

        // Check for badge unlock (10 hits)
        if (correctHits == 10 && badgeNotification != null)
        {
            StartCoroutine(ShowBadgeNotification("badge"));
        }

        // Increase difficulty progressively
        if (correctHits % 3 == 0 && currentSpeed < maxSpeed)
        {
            currentSpeed = Mathf.Min(currentSpeed + speedIncrement, maxSpeed);
        }

        UpdateUI();
    }

    public void OnBallMissed()
    {
        consecutiveMisses++;

        // Simplify if struggling
        if (consecutiveMisses >= missesBeforeSimplify)
        {
            currentSpeed = Mathf.Max(currentSpeed - speedIncrement, minSpeed);

            // Enable trajectory on ALL active balls
            foreach (GameObject ball in activeBalls)
            {
                if (ball != null)
                {
                    EnhancedBallController bc = ball.GetComponent<EnhancedBallController>();
                    if (bc != null)
                    {
                        bc.EnableTrajectoryGuide(true);
                    }
                }
            }

            // Show helpful message (use feedback panel, not score panel)
            ShowFeedback("Don't worry! Let's try slower.", 2f);
        }
    }

    IEnumerator MonitorInactivity()
    {
        while (isGameActive)
        {
            yield return new WaitForSeconds(1f);

            if (isPaused) continue; // Don't count inactivity while paused

            // Check for player movement (head tracking in VR)
            Vector3 currentPos = Camera.main.transform.position;
            float movement = Vector3.Distance(currentPos, lastPlayerPosition);
            lastPlayerPosition = currentPos;

            if (movement < 0.01f)
            {
                inactivityTimer += 1f;

                // Stage 1: Gentle encouragement at 10 seconds
                if (inactivityTimer >= 10f && inactivityTimer < 20f)
                {
                    HandleInactivity();
                }
                // Stage 2: Overstimulation pause at 20 seconds
                else if (inactivityTimer >= 20f)
                {
                    Debug.Log("Triggering overstimulation pause - player inactive for 20+ seconds");
                    OnPlayerOverstimulated();
                    inactivityTimer = 0f; // Reset after triggering
                }
            }
            else
            {
                inactivityTimer = 0f;
            }
        }
    }

    void HandleInactivity()
    {
        // Only trigger once per inactivity period
        if (inactivityTimer < 10.5f) return;

        Debug.Log("Gentle encouragement - player inactive for 10 seconds");

        // Play soft chime ONCE
        if (softChime != null && !softChime.isPlaying)
            softChime.Play();

        // Make balls glow with animated effect
        foreach (GameObject ball in activeBalls)
        {
            if (ball != null)
            {
                EnhancedBallController bc = ball.GetComponent<EnhancedBallController>();
                if (bc != null)
                {
                    bc.EnableGlow(true);
                }
            }
        }

        // Make the currently active arrow glow with animated effect
        if (currentActiveArrow != null)
            currentActiveArrow.SetAnimatedGlow(true);

        // Glow racket
        if (glowRacketEffect != null && !glowRacketEffect.activeInHierarchy)
        {
            glowRacketEffect.SetActive(true);
            Invoke("DisableRacketGlow", 3f);
        }
    }

    void DisableRacketGlow()
    {
        if (glowRacketEffect != null)
            glowRacketEffect.SetActive(false);
    }

    void ToggleBackToMenu()
    {
        if (BackToMenu != null)
        {
            if(isBackToMenuVisible) { 
                BackToMenu.SetActive(false);
                isBackToMenuVisible = false;
                isPaused = false;
                Time.timeScale = 1f;
            }
            else
            {
                isBackToMenuVisible = true;
                BackToMenu.SetActive(true);
                isPaused = true;
                Time.timeScale = 0f;
            }
        }
    }

    public void OnPlayerOverstimulated()
    {
        // Show overstimulation overlay
        if (overstimulationOverlay != null)
        {
            overstimulationOverlay.SetActive(true);
        }

        // Pause gameplay but don't show pause menu
        Time.timeScale = 0f;
        isPaused = true;

        // Dim scene
        if (sceneLight != null)
        {
            StartCoroutine(DimLighting());
        }

        // Play calm music
        if (calmMusic != null && !calmMusic.isPlaying)
        {
            calmMusic.Play();
        }

        // Auto-resume after calming period
        StartCoroutine(AutoResumeFromOverstimulation());
    }

    IEnumerator AutoResumeFromOverstimulation()
    {
        yield return new WaitForSecondsRealtime(5f); // Use realtime since Time.timeScale = 0

        // Hide overlay
        if (overstimulationOverlay != null)
        {
            overstimulationOverlay.SetActive(false);
        }

        // Resume
        ResumeGame();
    }

    IEnumerator DimLighting()
    {
        if (sceneLight == null) yield break;

        float originalIntensity = sceneLight.intensity;
        float targetIntensity = originalIntensity * 0.4f; // Dim to 40%

        // Fade out
        float elapsed = 0f;
        float duration = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaled time since game is paused
            sceneLight.intensity = Mathf.Lerp(originalIntensity, targetIntensity, elapsed / duration);
            yield return null;
        }

        sceneLight.intensity = targetIntensity;
    }

    IEnumerator RestoreLighting()
    {
        if (sceneLight == null) yield break;

        float currentIntensity = sceneLight.intensity;
        float originalIntensity = 1f; // Default scene light intensity

        // Fade back in
        float elapsed = 0f;
        float duration = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            sceneLight.intensity = Mathf.Lerp(currentIntensity, originalIntensity, elapsed / duration);
            yield return null;
        }

        sceneLight.intensity = originalIntensity;
    }

    IEnumerator ShowBadgeNotification(string message)
    {
        if (badgeNotification != null)
        {
            TextMeshProUGUI badgeText = badgeNotification.GetComponentInChildren<TextMeshProUGUI>(true);
            if (badgeText != null)
                badgeText.text = message;

            badgeNotification.SetActive(true);
            yield return new WaitForSeconds(3f);
            badgeNotification.SetActive(false);
        }
    }

    void UpdateUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Hits: {correctHits}";
        }

        if (speedLevelText != null)
        {
            string level = "Slow";
            if (currentSpeed > 10f) level = "Fast";
            else if (currentSpeed > 5f) level = "Medium";
            speedLevelText.text = $"Speed: {level}";
        }

        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60);
            int seconds = Mathf.FloorToInt(timeRemaining % 60);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    public void spawnRateUp()
    {
        spawnDelay -= 0.1f;
    }

    public void spawnRateDown()
    {
        spawnDelay += 0.1f;
    }

    void EndGame()
    {
        isGameActive = false;

        // Stop all coroutines
        StopAllCoroutines();

        // Destroy all active balls
        foreach (GameObject ball in activeBalls)
        {
            if (ball != null)
            {
                Destroy(ball);
            }
        }
        activeBalls.Clear();

        // Show final stats (keep score panel visible, hide speed/timer)
        if (scoreTextObject != null) scoreTextObject.SetActive(true);

        if (scoreText != null)
        {
            float accuracy = totalBalls > 0 ? (float)correctHits / totalBalls * 100f : 0;
            scoreText.text = $"Game Over!\nHits: {correctHits}\nAccuracy: {accuracy:F1}%";
        }

        if (speedLevelTextObject != null)
            speedLevelTextObject.SetActive(false);
        if (timerTextObject != null)
            timerTextObject.SetActive(false);

        // Also ensure intro + feedback are not showing
        if (introductionTextObject != null) introductionTextObject.SetActive(false);
        HideFeedbackImmediate();

        // Play celebration if did well
        if (correctHits >= 10 && voicePraise != null)
        {
            voicePraise.Play();
        }
    }

    public void PlaySoftChime()
    {
        if (softChime != null && !softChime.isPlaying)
            softChime.Play();
    }

    // Public methods for settings adjustments
    public void SetMaxSpeed(float speed)
    {
        maxSpeed = speed;
    }

    public void SetTrajectoryGuides(bool enabled)
    {
        foreach (GameObject ball in activeBalls)
        {
            if (ball != null)
            {
                EnhancedBallController bc = ball.GetComponent<EnhancedBallController>();
                if (bc != null)
                {
                    bc.EnableTrajectoryGuide(enabled);
                }
            }
        }
    }

    public void ToggleTrajectoryGuides()
    {
        foreach (GameObject ball in activeBalls)
        {
            if (ball != null)
            {
                EnhancedBallController bc = ball.GetComponent<EnhancedBallController>();
                if (bc != null)
                {
                    if (bc.trajectoryLine.enabled)
                    {
                        bc.EnableTrajectoryGuide(false);
                    }
                    else
                    {
                        bc.EnableTrajectoryGuide(true);
                    }
                }
            }
        }
    }

    public void SetAudioVolume(float volume)
    {
        AudioListener.volume = volume;
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        // Show pause menu
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(true);
        }

        // Dim scene
        if (sceneLight != null)
        {
            StartCoroutine(DimLighting());
        }

        // Make sure calm music is playing
        if (calmMusic != null && !calmMusic.isPlaying)
        {
            calmMusic.Play();
        }
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        // Hide pause menu
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }

        // Restore lighting
        if (sceneLight != null)
        {
            StartCoroutine(RestoreLighting());
        }
    }

    public void ExitGame()
    {
        // Return to main menu or quit application
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    bool CheckVRMenuButtonDown()
    {
        return CheckMenuDownForHand(InputDeviceCharacteristics.Left) ||
               CheckMenuDownForHand(InputDeviceCharacteristics.Right);
    }

    bool prevMenuPressedLeft;
    bool prevMenuPressedRight;

    bool CheckMenuDownForHand(InputDeviceCharacteristics hand)
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(hand | InputDeviceCharacteristics.Controller, devices);

        bool pressedNow = false;
        for (int i = 0; i < devices.Count; i++)
        {
            if (devices[i].TryGetFeatureValue(CommonUsages.menuButton, out bool pressed) && pressed)
            {
                pressedNow = true;
                break;
            }
        }

        // edge detect
        if (hand == InputDeviceCharacteristics.Left)
        {
            bool down = pressedNow && !prevMenuPressedLeft;
            prevMenuPressedLeft = pressedNow;
            return down;
        }
        else
        {
            bool down = pressedNow && !prevMenuPressedRight;
            prevMenuPressedRight = pressedNow;
            return down;
        }
    }

}