using UnityEngine;
using UnityEngine.UI;

namespace StylizedSkybox {

    public class SkyboxSwitch : MonoBehaviour {

        public Canvas UI;
        public Text text;
        public Light sun;
        public Rotate sunRotator;

        Material[] skybox;
        int index;

        void Start() {
            skybox = Resources.LoadAll<Material>("SkyboxPlus/Materials");
            SetSkybox();
        }

        void Update() {
            if (Input.GetKeyDown(KeyCode.Space)) {
                SetSkybox();
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) {
                sun.transform.eulerAngles = new Vector3(5, 0, 0);
                sunRotator.enabled = false;
            }
            if (Input.GetKeyDown(KeyCode.Alpha2)) {
                sun.transform.eulerAngles = new Vector3(90, 0, 0);
                sunRotator.enabled = false;
            }
            if (Input.GetKeyDown(KeyCode.Alpha3)) {
                sun.transform.eulerAngles = new Vector3(210, 0, 0);
                sunRotator.enabled = false;
            }
            if (Input.GetKeyDown(KeyCode.T)) {
                sunRotator.enabled = !sunRotator.enabled;
            }
            if (Input.GetKeyDown(KeyCode.U)) {
                UI.enabled = !UI.enabled;
            }
        }

        void SetSkybox() {
            if (++index >= skybox.Length) index = 0;
            Material skyboxMaterial = skybox[index];
            RenderSettings.skybox = skyboxMaterial;
            text.text = "Skybox material: " + skyboxMaterial.name;
        }
    }

}