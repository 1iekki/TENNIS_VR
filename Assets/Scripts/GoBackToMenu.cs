using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.VRTemplate
{
    /// <summary>
    /// Controls the steps in the in coaching card.
    /// </summary>
    public class GoBackToMenu : MonoBehaviour
    {
        [SerializeField]
        public string menuSceneName;

        [SerializeField]
        public TextMeshProUGUI m_StepButtonTextField;

        public void LoadScene()
        {
            SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
        }
    }
}
