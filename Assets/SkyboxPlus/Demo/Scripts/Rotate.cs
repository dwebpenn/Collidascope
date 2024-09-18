using UnityEngine;

namespace StylizedSkybox {

    public class Rotate : MonoBehaviour {
        public float speed = 1f;

        void Update() {
            transform.Rotate(Time.deltaTime * speed, -Time.deltaTime * speed * 1.1f, 0);
        }
    }

}