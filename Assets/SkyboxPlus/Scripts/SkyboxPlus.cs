using UnityEngine;

namespace SkyboxPlus {

    [ExecuteInEditMode]
    public class SkyboxPlus : MonoBehaviour {

        static class ShaderParams {
            public static readonly int LightWorldToLocal = Shader.PropertyToID("_LightWL");
            public static readonly int LightDir = Shader.PropertyToID("_WorldSpaceSunDir");
            public static readonly int MoonLightWorldToLocal = Shader.PropertyToID("_MoonLightWL");
            public static readonly int MoonLightDir = Shader.PropertyToID("_WorldSpaceMoonDir");
            public const string SKW_CUSTOM_POSITIONS = "SBP_CUSTOM_POSITIONS";
        }

        public Transform sun;
        public Transform moon;

        private void OnEnable() {
            if (sun == null) sun = transform;
            Shader.EnableKeyword(ShaderParams.SKW_CUSTOM_POSITIONS);
        }

        private void OnDisable() {
            Shader.DisableKeyword(ShaderParams.SKW_CUSTOM_POSITIONS);
        }

        void LateUpdate() {
            Shader.SetGlobalMatrix(ShaderParams.LightWorldToLocal, sun.worldToLocalMatrix);
            Shader.SetGlobalVector(ShaderParams.LightDir, -sun.forward);
            if (moon == null) {
                Shader.SetGlobalMatrix(ShaderParams.MoonLightWorldToLocal, sun.worldToLocalMatrix);
                Vector4 moonDir = sun.forward;
                moonDir.w = -1;
                Shader.SetGlobalVector(ShaderParams.MoonLightDir, moonDir);
            } else {
                Shader.SetGlobalMatrix(ShaderParams.MoonLightWorldToLocal, moon.worldToLocalMatrix);
                Vector4 moonDir = -moon.forward;
                moonDir.w = -1;
                Shader.SetGlobalVector(ShaderParams.MoonLightDir, moonDir);
            }
        }

    }

}