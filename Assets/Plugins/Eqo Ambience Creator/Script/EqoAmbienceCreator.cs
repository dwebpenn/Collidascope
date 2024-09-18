using System.Collections.Generic;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
[AddComponentMenu("Eqo Ambience Creator/Eqo Ambience Creator")]
public class EqoAmbienceCreator : MonoBehaviour
{
    #region PROPERTIES

    public AmbienceProfile ambienceProfile;
    [Tooltip("On game end or profile swap this will roll back any change made to the previous profile to what they were before game start.")]
    public bool rollBackChanges = true;
    
    [Tooltip("If the ambience profile changes, a fade effect will take place allowing a more smooth and seamless change from one profile to another.")]
    public bool useFading = true;
    [Min(0), Tooltip("The fading speed for any fade out and fade in.")]
    public float fadeSpeed = 0.3f;

    [Tooltip("Change the global volume of the ambience.")]
    [Range(0, 1)] public float globalVolume = 1;
    
    [System.Serializable]
    public struct IncludedAudiosData 
    {
        public string name;
        public AudioSource audioSource;
        public Transform transform;
        public float chosenPlayTime;
        public float timer;
        public float chosenStopTime;

        public IncludedAudiosData(string name, AudioSource audioSource, Transform transform, float chosenPlayTime, float timer, float chosenStopTime) {
            this.name = name;
            this.audioSource = audioSource;
            this.transform = transform;
            this.chosenPlayTime = chosenPlayTime;
            this.timer = timer;
            this.chosenStopTime = chosenStopTime;
        }
    }

    public static EqoAmbienceCreator instance;

    #endregion
    
    #region SYSTEM VARIABLES

    public AudioSource previewAudio;
    [SerializeField] public List<IncludedAudiosData> includedAudios = new List<IncludedAudiosData>();
    List<AmbienceProfile.AudiosData> savedAudiosPropsList = new List<AmbienceProfile.AudiosData>();
    List<AudioSource> audiosToFadeIn = new List<AudioSource>();
    List<AudioSource> audiosToFadeOut = new List<AudioSource>();

    AmbienceProfile lastUsedProfile;
    AmbienceProfile profileUsedOnStart;
    AmbienceProfile profileUsedWithFading;

    bool gameStarted;
    bool onGameDestroy;
    bool isFading;
    
    #endregion

    #region UNITY METHODS
    
    void Start()
    {
        if (EqoAmbienceCreator.instance == null) {
            instance = this;
        }
        
        StopPreview();
        SetRandomPlayTimes();
        SetRandomStopTimes();
        UpdateAudioValues();
        PlayAll();
        SaveCurrentProfileAsLastUsed();
        SaveProfileData();

        gameStarted = true;
        profileUsedOnStart = ambienceProfile;
    }
    
    void OnDestroy()
    {
        ambienceProfile = profileUsedOnStart;
        onGameDestroy = true;
    }
    
    void OnApplicationQuit()
    {
        ambienceProfile = profileUsedOnStart;
        onGameDestroy = true;
        RollBackProfile();
    }

    void OnEnable()
    {
        EnableOrDisableAudios(true);
        PlayAll();
    }

    void OnDisable()
    {   
        EnableOrDisableAudios(false);
    }

    void OnValidate()
    {
        GetPreviewAudio();

        if (!enabled) {
            StopPreview();
        }

        PopulateIncludedAudios();
    }

    void Update()
    {
        CheckAmbienceProfile();
        UpdateAudioValues();
        RunPlayTimers();
        RunStopTimers();
        CheckForAudiosToFadeIn();
    }

    #endregion

    #region EDITOR & RUNTIME CALLS

    public bool Preview(string filePath)
    {
        // load the clip
        AudioClip clip = Resources.Load<AudioClip>(filePath);
        if (clip == null) return false;

        previewAudio.clip = clip;
        previewAudio.Play();

        return true;
    }

    public void StopPreview()
    {
        if (previewAudio == null) return;
        previewAudio.Stop();
        previewAudio.clip = null;
    }

    void GetPreviewAudio()
    {
        if (previewAudio != null) {
            previewAudio.playOnAwake = false;
            previewAudio.loop = true;
            return;
        }

        // get the audio sources
        AudioSource[] audios = GetComponents<AudioSource>();

        if (audios.Length == 0) {
            previewAudio = gameObject.AddComponent(typeof(AudioSource)) as AudioSource;
            previewAudio.playOnAwake = false;
            previewAudio.loop = true;
            return;
        }
        
        foreach (var audio in audios) {
            if (audio.clip != null) continue;
            previewAudio = audio;
        }

        if (previewAudio == null) {
            previewAudio = gameObject.AddComponent(typeof(AudioSource)) as AudioSource;
        }

        previewAudio.playOnAwake = false;
        previewAudio.loop = true;
    }

    public void UpdateAudioValues()
    {
        if (ambienceProfile == null || ambienceProfile != lastUsedProfile || isFading) return;

        int max = ambienceProfile.includedAudios.Count;
        for (int i=0; i<max; i++) 
        {
            if (includedAudios.Count - 1 < i) break;

            IncludedAudiosData item = includedAudios[i];
            if (item.audioSource == null) continue;
            
            item.audioSource.volume = globalVolume * ambienceProfile.includedAudios[i].volume;
            item.audioSource.pitch = ambienceProfile.includedAudios[i].pitch;
            item.audioSource.priority = ambienceProfile.includedAudios[i].priority;

            // set the volume as the Y axis to get anytime (during fading mostly)
            Vector3 localPos = item.audioSource.transform.localPosition;
            localPos.y = item.audioSource.volume;
            item.audioSource.transform.localPosition = localPos;
            item.audioSource.loop = true;

            if (ambienceProfile.includedAudios[i].randomizePlay || !enabled) {
                item.audioSource.playOnAwake = false;
                continue;
            }

            item.audioSource.playOnAwake = true;
        }
    }

    public void RemoveAllAudios()
    {   
        while (transform.childCount > 0) {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        includedAudios.Clear();
    }

    public void SaveCurrentProfileAsLastUsed()
    {
        lastUsedProfile = ambienceProfile;
    }

    public AmbienceProfile LastUsedProfile()
    {
        return lastUsedProfile;
    }

    public void BuildAudioObjectsFromProfile()
    {   
        // avoid running on the first destroy (editor game exit)
        if (onGameDestroy) {
            onGameDestroy = false;
            return;
        }

        // *** All MARKED WITH 3 STARS ARE: VALIDATIONS TO CORRECTLY REMOVE ALL GAME OBJECTS (GAMEPLAY, EDITOR TIME ON PROFILE CHANGE)
        if (ambienceProfile == null || (ambienceProfile != lastUsedProfile)) {
            if (gameStarted) 
            {
                if (useFading) {
                    if (ambienceProfile == null) return;
                    if (isFading && profileUsedWithFading == ambienceProfile) return;

                    ProfileChangeFade();
                    return;
                }
            }

            RemoveAllAudios();

            if (transform.childCount == 0) {
                SaveCurrentProfileAsLastUsed();
            }
            
            if (ambienceProfile == null) return;
        }

        if (gameStarted && isFading) return;
        
        // return if the same profile and the audio amounts are the same in both profile and children (everything is updated already)
        if (ambienceProfile.includedAudios.Count == transform.childCount && lastUsedProfile == ambienceProfile) {
            SaveCurrentProfileAsLastUsed();
            PopulateIncludedAudios();
            return;
        }

        // ***
        if (ambienceProfile != null) {
            while (ambienceProfile.includedAudios.Count < transform.childCount) {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }
        }

        // ***
        if (ambienceProfile != lastUsedProfile && transform.childCount > 0) {
            RemoveAllAudios();
            return;
        }

        RemoveAllAudios();
         
        foreach (var item in ambienceProfile.includedAudios) {
            if (item.randomizePlay) {
                CreateAudio(item.clip, false);
                continue;
            }

            CreateAudio(item.clip, true);
        }
        
        SaveProfileData();
        PopulateIncludedAudios();
    }

    public void PopulateIncludedAudios(bool editorTestingAmbience=false)
    {
        if (ambienceProfile == null) {
            includedAudios.Clear();
            return;
        }

        if (!isFading) {
            if (ambienceProfile.includedAudios.Count == includedAudios.Count && lastUsedProfile == ambienceProfile) {
                return;
            }
        }

        includedAudios.Clear();

        int i = -1;
        foreach (Transform child in transform) 
        {
            i++;
            if (i > ambienceProfile.includedAudios.Count - 1) return;

            string name = child.gameObject.name;
            AudioSource audio = child.GetComponent<AudioSource>();
            if (audio == null) continue;

            IncludedAudiosData data = new IncludedAudiosData(name, audio, child, CreateRandomTime(ambienceProfile.includedAudios[i].playTime), 0, CreateRandomTime(ambienceProfile.includedAudios[i].stopTime));
            includedAudios.Add(data);
            
            if (Application.isPlaying) 
            {
                if (!audio.isPlaying && enabled) {
                    if (audio.playOnAwake && audio.transform.localPosition.z != 1) audio.Play();
                }

                continue;
            }
            
            if (editorTestingAmbience && !audio.isPlaying) {
                audio.Play();
            }
        }
    }

    public void CheckAmbienceProfile()
    {
        if (ambienceProfile == null) {
            RemoveAllAudios();
            return;
        }
        
        BuildAudioObjectsFromProfile();
    }

    #endregion

    #region MISC

    // print warning to console that ambience profile doesn't exist
    void PrintWarning(string functionName)
    {
        Debug.LogWarning($"Operation skipped. Can't continue {functionName} without an ambience profile attached.");
    }

    // create an audio source child
    void CreateAudio(AudioClip clip, bool playOnAwake = true, bool fadingIn = false, int volume = -1, float audioProfileVolume = -1)
    {
        if (clip == null) return;
        
        // create new child
        GameObject go = new GameObject();
        go.name = clip.name;
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;

        // add the audio
        AudioSource audioSourceComponent = go.AddComponent(typeof(AudioSource)) as AudioSource;
        audioSourceComponent.playOnAwake = playOnAwake;
        audioSourceComponent.clip = clip;
        audioSourceComponent.loop = true;
        audioSourceComponent.spatialBlend = 0;

        if (volume != -1) { 
            audioSourceComponent.volume = volume;
        }

        if (fadingIn) {
            go.transform.localPosition = new Vector3(0, audioProfileVolume, 0);
            if (audiosToFadeIn.Contains(audioSourceComponent)) return;
            audiosToFadeIn.Add(audioSourceComponent);
        }
    }

    void SetRandomPlayTimes()
    {
        if (ambienceProfile == null) return;
        CheckAmbienceProfile();

        int max = ambienceProfile.includedAudios.Count;
        for (int i=0; i<max; i++) 
        {
            if (i > includedAudios.Count - 1) return;

            AudioSource audio = includedAudios[i].audioSource;
            if (audio == null) continue;
            if (audio.isPlaying) continue;

            AmbienceProfile.AudiosData item = ambienceProfile.includedAudios[i];
            if (!item.randomizePlay) continue;
    
            IncludedAudiosData item2 = includedAudios[i];
            IncludedAudiosData newData = new IncludedAudiosData(item2.name, item2.audioSource, item2.transform, CreateRandomTime(item.playTime), 0, CreateRandomTime(item.stopTime));
            includedAudios[i] = newData;
        }
    }

    void RunPlayTimers()
    {
        if (ambienceProfile == null) return;
        CheckAmbienceProfile();

        int max = includedAudios.Count;
        for (int i=0; i<max; i++) 
        {
            IncludedAudiosData item = includedAudios[i];

            if (item.audioSource == null) continue;
            if (item.audioSource.isPlaying) continue;
            if (item.transform.localPosition.z == 1) continue;
            if (ambienceProfile.includedAudios.Count - 1 < i) return;

            // if not set to randomized -> go to next loop
            if (!ambienceProfile.includedAudios[i].randomizePlay) continue;
            
            float timer = item.timer;
            timer += Time.deltaTime;

            if (timer >= item.chosenPlayTime) 
            {
                AmbienceProfile.AudiosData profileDataItem = ambienceProfile.includedAudios[i];
                IncludedAudiosData data = new IncludedAudiosData(item.name, item.audioSource, item.transform, CreateRandomTime(profileDataItem.playTime), 0, CreateRandomTime(profileDataItem.stopTime));
                includedAudios[i] = data;

                if (!item.audioSource.isPlaying) {
                    if (!useFading) item.audioSource.Play();
                    else SetAudioToFadeIn(item.audioSource, true);
                }

                continue;
            }
            
            IncludedAudiosData newData = new IncludedAudiosData(item.name, item.audioSource, item.transform, item.chosenPlayTime, timer, item.chosenStopTime);
            includedAudios[i] = newData;
        }
    }

    void SetRandomStopTimes()
    {
        if (ambienceProfile == null) {
            return;
        }

        CheckAmbienceProfile();

        int max = ambienceProfile.includedAudios.Count;
        for (int i=0; i<max; i++) 
        {
            if (i > includedAudios.Count - 1) return;

            AudioSource audio = includedAudios[i].audioSource;
            if (audio == null) continue;
            if (audio.isPlaying) continue;

            AmbienceProfile.AudiosData item = ambienceProfile.includedAudios[i];
            if (!item.randomizeStop) continue;

            IncludedAudiosData item2 = includedAudios[i];
            IncludedAudiosData newData = new IncludedAudiosData(item2.name, item2.audioSource, item2.transform, item2.chosenPlayTime, 0, CreateRandomTime(item.stopTime));
            includedAudios[i] = newData;
        }
    }

    void RunStopTimers()
    {
        if (ambienceProfile == null || includedAudios.Count != ambienceProfile.includedAudios.Count) {
            return;
        }

        CheckAmbienceProfile();

        int max = includedAudios.Count;
        for (int i=0; i<max; i++) 
        {
            IncludedAudiosData item = includedAudios[i];

            if (item.audioSource == null) continue;
            if (!item.audioSource.isPlaying) continue;
            if (ambienceProfile.includedAudios.Count - 1 < i) return;

            // if not set -> go to next loop
            if (!ambienceProfile.includedAudios[i].randomizeStop) continue;
            if (item.audioSource.time < item.chosenStopTime) continue;

            if (useFading) {
                if (item.audioSource.time <= item.chosenStopTime) continue;
                SetAudioToFadeOut(item.audioSource, false, true);
                continue;
            }

            item.audioSource.Stop();

            AmbienceProfile.AudiosData profileDataItem = ambienceProfile.includedAudios[i];
            IncludedAudiosData data = new IncludedAudiosData(item.name, item.audioSource, item.transform, item.chosenPlayTime, 0, CreateRandomTime(profileDataItem.stopTime));
            includedAudios[i] = data;
        }
    }

    float CreateRandomTime(Vector2 range) {
        return Random.Range(range.x, range.y);
    }

    // called from StopAudio() APIs only -> to flag the audio shouldn't play again automatically
    void StopAudioSourceAndFlag(AudioSource audio)
    {
        audio.Stop();

        // this flag (z axis) tells the system that it's been stopped by an API
        // so it shouldn't play again, until this flag is removed 
        Vector3 pos = audio.transform.localPosition;
        pos.z = 1;
        audio.transform.localPosition = pos;
    }

    void PlayAudioSource(AudioSource audio, bool removeFlagOnly=false)
    {
        if (!removeFlagOnly) audio.Play();

        // removes the z axis flag
        Vector3 pos = audio.transform.localPosition;
        pos.z = 0;
        audio.transform.localPosition = pos;
    }
    
    #endregion

    #region FADING

    void ProfileChangeFade()
    {
        profileUsedWithFading = ambienceProfile;
        SaveCurrentProfileAsLastUsed();

        audiosToFadeIn.Clear();
        audiosToFadeOut.Clear();

        // set the old audios to fade out
        for (int i=0; i<transform.childCount; i++) {
            Transform child = transform.GetChild(i);
            AudioSource audio = child.GetComponent<AudioSource>();
            
            if (audio == null) {
                DestroyImmediate(child.gameObject);
                continue;
            }

            SetAudioToFadeOut(audio, true, false);
        }

        // add the new profile audios with a default volume of 0
        int max = ambienceProfile.includedAudios.Count;
        for (int i=0; i<max; i++) {
            var item = ambienceProfile.includedAudios[i];
            if (item.randomizePlay) {
                CreateAudio(item.clip, false, true, 0, item.volume);
                continue;
            }
            
            CreateAudio(item.clip, true, true, 0, item.volume);
        }

        isFading = true;
    }

    void CheckForAudiosToFadeIn()
    {
        if (!isFading) return;
        
        int max = audiosToFadeIn.Count;
        for (int i=0; i<max; i++) {
            if (i + 1 > audiosToFadeIn.Count) break;
            AudioSource audio = audiosToFadeIn[i];

            if (audio == null) {
                audiosToFadeIn.RemoveAt(i);
                continue;
            }

            if (audio.playOnAwake && !audio.isPlaying) audio.Play();

            float wantedVolume = audio.transform.localPosition.y;
            if (audio.volume >= wantedVolume) {
                audio.volume = wantedVolume;
                audiosToFadeIn.RemoveAt(i);
                continue;
            }

            audio.volume += Time.deltaTime * fadeSpeed;
        }

        CheckForAudiosToFadeOut();
    }

    void CheckForAudiosToFadeOut()
    {   
        int max = audiosToFadeOut.Count;
        for (int i=0; i<max; i++) {
            if (i + 1 > audiosToFadeOut.Count) break;
            AudioSource audio = audiosToFadeOut[i];
            
            if (audio == null) {
                audiosToFadeOut.RemoveAt(i);
                continue;
            }

            if (!audio.isPlaying) {
                audiosToFadeOut.RemoveAt(i);
                
                if (audio.transform.localPosition.x == 1) {
                    DestroyImmediate(audio.transform.gameObject);
                }

                continue;
            }

            audio.volume -= Time.deltaTime * fadeSpeed;
            if (audio.volume > 0) continue;

            audio.Stop();

            if (audio.transform.localPosition.x == 1) {
                DestroyImmediate(audio.transform.gameObject);
            }

            audiosToFadeOut.RemoveAt(i);
        }
        
        if (audiosToFadeIn.Count == 0 && audiosToFadeOut.Count == 0) {
            FadingCompleted();
        }
    }

    void SetAudioToFadeOut(AudioSource audio, bool shouldDestroy, bool triggerFading, bool stopAPI=false)
    {
        if (audiosToFadeIn.Contains(audio)) {
            audiosToFadeIn.Remove(audio);
        }

        if (audiosToFadeOut.Contains(audio)) return;
        
        // if audio is supposed to fade out and then get destroyed -> set the X axis to 1
        if (shouldDestroy) {
            audio.transform.localPosition = new Vector3(1, audio.transform.localPosition.y, audio.transform.localPosition.z);
        }
        else {
            audio.transform.localPosition = new Vector3(0, audio.transform.localPosition.y, audio.transform.localPosition.z);
        }

        // if Z axis is set to 1 -> means the stop API has called and thus nothing should play it again
        if (stopAPI) {
            Vector3 pos = audio.transform.localPosition;
            pos.z = 1;
            audio.transform.localPosition = pos;
        }
        
        audiosToFadeOut.Add(audio);
        if (triggerFading) isFading = true;
    }

    void SetAudioToFadeIn(AudioSource audio, bool triggerFading)
    {
        if (audiosToFadeOut.Contains(audio)) audiosToFadeOut.Remove(audio);
        if (audiosToFadeIn.Contains(audio)) return;

        Vector3 pos = audio.transform.localPosition;
        pos.z = 0;
        audio.transform.localPosition = pos;

        audio.volume = 0;
        audiosToFadeIn.Add(audio);
        if (triggerFading) isFading = true;
        audio.Play();
    }

    void FadingCompleted()
    {
        PopulateIncludedAudios();
        isFading = false;
        audiosToFadeIn.Clear();
        audiosToFadeOut.Clear();
        SaveProfileData();
    }

    #endregion

    #region APIs

    // play all added audios
    public void PlayAll(bool editorTestingAmbience=false)
    {
        if (ambienceProfile == null) {
            PrintWarning("PlayAll()");
            return;
        }

        CheckAmbienceProfile();

        foreach (var item in includedAudios) 
        {
            if (item.audioSource == null) continue;
            
            if (editorTestingAmbience) {
                if (!item.audioSource.isPlaying) item.audioSource.Play();
                continue;
            }
            
            if (item.audioSource.playOnAwake) {
                if (!item.audioSource.isPlaying) {
                    PlayAudioSource(item.audioSource);
                }

                continue;
            }

            PlayAudioSource(item.audioSource, true);
        }
    }

    // stops all the audios
    public void StopAll(bool fadeOut = true)
    {
        if (ambienceProfile == null) {
            PrintWarning("StopAll()");
            return;
        }

        if (isFading) {
            BuildAudioObjectsFromProfile();
        }

        foreach (Transform child in transform) {
            AudioSource audio = child.GetComponent<AudioSource>();

            if (audio == null) {
                DestroyImmediate(child.gameObject);
                continue;
            }

            if (fadeOut) {
                SetAudioToFadeOut(audio, false, true, true);
                continue;
            }
            
            StopAudioSourceAndFlag(audio);
        }
    }

    public void EnableOrDisableAudios(bool toEnable)
    {
        if (ambienceProfile == null) {
            PrintWarning("EnableOrDisableAudios()");
            return;
        }

        CheckAmbienceProfile();

        foreach (var item in includedAudios) 
        {
            if (item.audioSource == null) continue;
            
            if (toEnable) {
                item.audioSource.enabled = true;
                continue;
            }

            item.audioSource.enabled = false;
        }
        
        if (!isFading) return;
        
        foreach (Transform child in transform) {
            AudioSource audio = child.GetComponent<AudioSource>();
            if (audio == null) continue;

            if (toEnable) {
                audio.enabled = true;
                continue;
            }

            audio.enabled = false;
        }
    }

    // add audio by file name
    public void AddAudio(string fileName)
    {
        if (ambienceProfile == null) {
            PrintWarning("AddAudio()");
            return;
        }

        AudioClip clip = Resources.Load<AudioClip>(fileName);
        if (clip == null) {
            Debug.LogWarning("AddAudio() - operation skipped. You're either trying to add a non-audio file or the audio doesn't exist. The passed file must be of an AudioClip with a supported format.");
            return;
        }

        ambienceProfile.AddAudio(fileName);
        CreateAudio(clip);
        
        StopPreview();
        CheckAmbienceProfile();
    }

    public void AddAudio(AudioClip clip)
    {
        if (ambienceProfile == null) {
            PrintWarning("AddAudio()");
            return;
        }

        if (clip == null) {
            Debug.LogWarning("AddAudio() - operation skipped. No audio clip passed.");
            return;
        }

        ambienceProfile.AddAudio(clip);
        CreateAudio(clip);

        StopPreview();
        CheckAmbienceProfile();
    }

    // remove a certain audio by index
    public void RemoveAudio(int index, bool fadeOut=true)
    {
        if (ambienceProfile == null) {
            PrintWarning("RemoveAudio()");
            return;
        }

        CheckAmbienceProfile();
        
        if (index > ambienceProfile.includedAudios.Count - 1) {
            Debug.LogWarning("RemoveAudio() - operation skipped. Passed index is out of bounds.");
            return;
        }

        ambienceProfile.includedAudios.RemoveAt(index);
        
        if (fadeOut) {
            SetAudioToFadeOut(includedAudios[index].audioSource, true, true);
        }
        else {
            DestroyImmediate(includedAudios[index].transform.gameObject);
        }

        includedAudios.RemoveAt(index);
    }

    // play a certain audio by index
    public void PlayAudio(int index, bool fadeIn=true)
    {
        if (ambienceProfile == null) {
            PrintWarning("PlayAudio()");
            return;
        }

        CheckAmbienceProfile();

        if (index > includedAudios.Count - 1) {
            Debug.LogWarning("PlayAudio() - operation skipped. The passed index is out of bounds.");
            return;
        }

        if (fadeIn) {
            SetAudioToFadeIn(includedAudios[index].audioSource, true);
            return;
        }

        PlayAudioSource(includedAudios[index].audioSource);
    }

    // play a certain audio(s) by name
    public void PlayAudio(string name, bool playAll=false)
    {
        if (ambienceProfile == null) {
            PrintWarning("PlayAudio()");
            return;
        }

        CheckAmbienceProfile();

        foreach (var item in includedAudios) {
            if (item.name == name) {
                PlayAudioSource(item.audioSource);
                if (!playAll) return;
            }
        }
    }

    // stop a certain audio by index
    public void StopAudio(int index, bool fadeOut=true)
    {
        if (ambienceProfile == null) {
            PrintWarning("StopAudio()");
            return;
        }

        CheckAmbienceProfile();

        if (index > includedAudios.Count - 1) {
            Debug.LogWarning("StopAudio() - operation skipped. The passed index is out of bounds.");
            return;
        }

        if (fadeOut) {
            SetAudioToFadeOut(includedAudios[index].audioSource, false, true, true);
            return;
        }

        StopAudioSourceAndFlag(includedAudios[index].audioSource);
    }

    // stop certain audio(s) by name
    public void StopAudio(string name, bool stopAll = false, bool fadeOut=true)
    {
        if (ambienceProfile == null) {
            PrintWarning("StopAudio()");
            return;
        }

        CheckAmbienceProfile();

        foreach (var item in includedAudios) {
            if (item.name == name) {
                if (fadeOut) {
                    SetAudioToFadeOut(item.audioSource, false, true, true);
                    if (!stopAll) return;
                }

                StopAudioSourceAndFlag(item.audioSource);
                if (!stopAll) return;
            }
        }
    }

    // returns a list of the profile data
    public List<AmbienceProfile.AudiosData> GetProfileData()
    {
        if (ambienceProfile == null) {
            PrintWarning("GetProfileData()");
            return null;
        }

        return ambienceProfile.includedAudios;
    }

    // returns the audio source of passed index
    public AudioSource GetAudioSource(int index)
    {
        if (ambienceProfile == null) {
            PrintWarning("GetAudioSource()");
            return null;
        }


        CheckAmbienceProfile();


        if (index > includedAudios.Count - 1) {
            Debug.LogWarning("GetAudioSource() - operation skipped. The passed index is bigger than the list.");
            return null;
        }


        return includedAudios[index].audioSource;
    }

    // returns an array of type audio source of the audios with the passed name
    public AudioSource[] GetAudioSource(string name)
    {
        if (ambienceProfile == null) {
            PrintWarning("GetAudioSource()");
            return null;
        }


        CheckAmbienceProfile();
        List<AudioSource> audios = new List<AudioSource>();


        foreach (var item in includedAudios) {
            if (item.audioSource == null) {
                continue;
            }

            if (item.name != name && item.audioSource.clip.name != name) {
                continue;
            }
            
            audios.Add(item.audioSource);
        }


        return audios.ToArray();
    }

    public AudioSource[] GetAudioSources()
    {
        if (ambienceProfile == null) {
            PrintWarning("GetAudioSources()");
            return null;
        }

        CheckAmbienceProfile();
        List<AudioSource> list = new List<AudioSource>();

        foreach (var item in includedAudios) {
            list.Add(item.audioSource);
        }

        return list.ToArray();
    }

    // returns an int indicating the number of audios set in a profile -> returns (-1) if no profile added
    public int GetAudiosLength()
    {
        if (ambienceProfile == null) {
            PrintWarning("GetAudiosLength()");
            return -1;
        }

        return ambienceProfile.includedAudios.Count;
    }

    // returns an int array of indexes of all the audios with the passed name
    public int[] GetAudioIndexFromName(string name)
    {
        if (ambienceProfile == null) {
            PrintWarning("GetAudioIndexFromName");
            return null;
        }


        CheckAmbienceProfile();


        int max = includedAudios.Count;
        List<int> list = new List<int>();

        for (int i=0; i<max; i++) {
            if (includedAudios[i].name == name) {
                list.Add(i);
            }
        }

        return list.ToArray();
    }

    // change volume, pitch or priority of an audio by index
    public void ChangeAudioProperty(int index, string propertyType, float value)
    {
        if (ambienceProfile == null) {
            PrintWarning("ChangeAudioProperty()");
            return;
        }


        if (index > ambienceProfile.includedAudios.Count - 1) {
            Debug.LogWarning("ChangeAudioProperty() - operation skipped. The passed index is out of bounds.");          
            return;
        }


        AmbienceProfile.AudiosData data = ambienceProfile.includedAudios[index];
        bool isChanged = false;
        propertyType = propertyType.ToLower();

        
        if (propertyType == "volume") {
            data.volume = value;
            isChanged = true;
        }

        if (propertyType == "pitch") {
            data.pitch = value;
            isChanged = true;
        }

        if (propertyType == "priority") {
            data.priority = (int)value;
            isChanged = true;
        }

        if (!isChanged) {
            Debug.LogWarning("ChangeAudioProperty - operation skipped. The passed property type is invalid. You should pass either volume, pitch or priority keywords as a string.");
            return;
        }


        ambienceProfile.includedAudios[index] = data;
    }

    // save the current audio properties
    public void SaveProfileData()
    {
        if (ambienceProfile == null || !rollBackChanges) return;

        savedAudiosPropsList.Clear();

        foreach (var item in ambienceProfile.includedAudios) {
            savedAudiosPropsList.Add(item);
        }
    }

    // return the profile's original data
    public void RollBackProfile()
    {
        if (!rollBackChanges || isFading) return;

        lastUsedProfile.includedAudios.Clear();

        foreach (var item in savedAudiosPropsList) {
            lastUsedProfile.includedAudios.Add(item);
        }
    }

    #endregion
}