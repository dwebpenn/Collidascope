using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "AmbienceProfile", menuName = "Eqo Ambience Creator/Ambience Profile")]
public class AmbienceProfile : ScriptableObject
{
    [System.Serializable]
    public struct AudiosData {
        public string name;
        public AudioClip clip;
        public float volume;
        public float pitch;
        public int priority;
        public bool randomizePlay;
        public Vector2 playTime;
        public bool randomizeStop;
        public Vector2 stopTime;

        public AudiosData(string name, AudioClip clip, float volume, float pitch, int priority, bool randomizePlay, Vector2 playTime, bool randomizeStop, Vector2 stopTime) {
            this.name = name;
            this.clip = clip;
            this.volume = volume;
            this.pitch = pitch;
            this.priority = priority;
            this.randomizePlay = randomizePlay;
            this.playTime = playTime;
            this.randomizeStop = randomizeStop;
            this.stopTime = stopTime;
        }
    }

    [SerializeField] public List<AudiosData> includedAudios = new List<AudiosData>();

    
    public void AddAudio(string filePath)
    {
        AudioClip clip = Resources.Load<AudioClip>(filePath);

        if (clip == null) {
            Debug.LogWarning("Operation skipped. You're either trying to add a non-audio file or the audio doesn't exist. The passed file must be of an AudioClip with a supported format.");
            return;
        }

        AudiosData data = new AudiosData(filePath, clip, 1, 1, 128, false, new Vector3(10, 30), false, new Vector3(-1, -1));
        includedAudios.Add(data);
    }


    public void AddAudio(AudioClip clip)
    {
        if (clip == null) {
            Debug.LogWarning("Operation skipped. You're either trying to add a non-audio file or the audio doesn't exist. The passed file must be of an AudioClip with a supported format.");
            return;
        }

        AudiosData data = new AudiosData(clip.name, clip, 1, 1, 128, false, new Vector3(10, 30), false, new Vector3(-1, -1));
        includedAudios.Add(data);
    }


    public void RemoveAudio(int index)
    {
        if (index > includedAudios.Count - 1) {
            return;
        }

        includedAudios.RemoveAt(index);
    }
}


