using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kamgam.SkyClouds
{
    public class SkyCloudsDemo : MonoBehaviour
    {
        void Awake()
        {
            // Unlimited
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;

            // Limited
            // QualitySettings.vSyncCount = 1;
        }
    }
}
