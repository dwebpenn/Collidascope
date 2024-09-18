using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SkyboxPlus {

    [CustomEditor(typeof(SkyboxPlus))]
    public class SkyboxPlusEditor : Editor {

        public override void OnInspectorGUI() {

            if (GUILayout.Button("Edit Skybox Material Properties")) {
                if (RenderSettings.skybox != null) {
                    Selection.activeObject = RenderSettings.skybox;
                    EditorGUIUtility.ExitGUI();
                }
            }
            DrawDefaultInspector();
        }

    }

}