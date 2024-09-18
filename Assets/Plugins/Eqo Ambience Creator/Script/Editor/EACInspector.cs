using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;

namespace EqoAmbienceCreatorNS
{
    [CustomEditor(typeof(EqoAmbienceCreator))]
    public class EACInspector : Editor
    {
        SerializedProperty ambienceProfile, useFading, fadeSpeed, rollBackChanges, globalVolume;

        List<string> categories = new List<string>();
        int categorySelected = 0;
        List<string> audios = new List<string>();
        int audioSelected = 0;

        string currentPath;

        [System.Serializable]
        public struct AudioBlockState {
            public string name;
            public Transform transform;
            public bool collapseState;

            public AudioBlockState(string name, Transform transform, bool collapseState) {
                this.name = name;
                this.transform = transform;
                this.collapseState = collapseState;
            }
        }

        List<AudioBlockState> audioBlocks = new List<AudioBlockState>();
        string[] compatibleFormats = new string[] {".mp3", ".wav", ".ogg", ".aiff"};

        EqoAmbienceCreator script;
        

        #region UNITY METHODS

        void Reset()
        {
            EqoAmbienceCreator script = (EqoAmbienceCreator) target;

            if (script.ambienceProfile) {
                if (script.ambienceProfile.includedAudios.Count <= 0) {
                    EditorPrefs.DeleteKey("AudioBlockStates");
                    EditorPrefs.DeleteKey("AudioBlockPlaying");
                    EditorPrefs.DeleteKey("IsPlayingAll");
                }
            }
            
            if (!script.previewAudio.isPlaying) {
                EditorPrefs.DeleteKey("IsPreviewing");
            }
        }

        void OnEnable()
        {
            script = (EqoAmbienceCreator) target;

            ambienceProfile = serializedObject.FindProperty("ambienceProfile");
            useFading = serializedObject.FindProperty("useFading");
            fadeSpeed = serializedObject.FindProperty("fadeSpeed");
            rollBackChanges = serializedObject.FindProperty("rollBackChanges");
            globalVolume = serializedObject.FindProperty("globalVolume");

            currentPath = $"{Application.dataPath}/Eqo Ambience Creator/Resources/";

            if(!Directory.Exists(currentPath))
            {	
                // if path doesn't exist, create it
                Directory.CreateDirectory(currentPath);
            }
        }

        public override void OnInspectorGUI () 
        {   
            serializedObject.Update();

            if (Application.isPlaying) {
                if (!script.enabled) {
                    script.EnableOrDisableAudios(false);
                }

                StopPreview(script);
                StopAudioBlock();
                EditorPrefs.DeleteKey("IsPlayingAll");
            }
            else {
                // if profile changed -> stop all audios
                if (script.ambienceProfile != script.LastUsedProfile()) {
                    StopPreview(script);
                    StopAudioBlock();
                    StopAllAmbience(script);
                }

                // updates the heirarchy based on the profile
                script.CheckAmbienceProfile();
            }

            DrawProfileSection(script);
            EditorGUILayout.Space(10);
    
            DrawChooseAudioSection(script);
            DrawIncludedAudiosList(script);


            if (script.includedAudios.Count > 0) {
                if (!Application.isPlaying) {
                    DrawPlayCurrentAmbienceButton(script);
                }
            }
            else {
                EditorPrefs.DeleteKey("IsPlayingAll");
            }


            if (!script.enabled) {
                StopPreview(script);
                StopAudioBlock();
                StopAllAmbience(script);
                script.EnableOrDisableAudios(false);
            }
            else {
                script.EnableOrDisableAudios(true);

                if (Application.isPlaying) {
                    script.PlayAll();
                }
            }

            // save the last selected category & audio
            EditorPrefs.SetInt("CategoryIndex", categorySelected);
            EditorPrefs.SetInt("AudioIndex", audioSelected);
            
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region DRAWING

        void DrawProfileSection(EqoAmbienceCreator script)
        {
            EditorGUILayout.LabelField("Ambience Profile", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            {
                EditorGUILayout.PropertyField(ambienceProfile);

                if (GUILayout.Button("New", ProfileBtnsStyle())) {
                    CreateNewProfile(script);
                }

                if (script.ambienceProfile != null) {
                    if (GUILayout.Button("Clone", ProfileBtnsStyle())) {
                        CloneToProfile(script);
                    }
                }
            }
            GUILayout.EndHorizontal();
            
            EditorGUILayout.PropertyField(rollBackChanges);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Audio Fading", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(useFading);
            if (script.useFading) {
                EditorGUILayout.PropertyField(fadeSpeed);
            }
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Set Global Volume", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(globalVolume);
        }

        void DrawChooseAudioSection(EqoAmbienceCreator script)
        {
            EditorGUILayout.LabelField("Choose Ambience Type", EditorStyles.boldLabel);

            // read the audio files and create categories dropdown
            PopulateCategories();

            // categories dropdown select
            if (categories.Count == 0) {
                // print a label that no categories found in audio file names
                EditorGUILayout.LabelField("No Categories Found", EditorStyles.boldLabel);
                return;
            }
            else {
                if (EditorPrefs.GetInt("CategoryIndex", -1) == -1) {
                    categorySelected = EditorGUILayout.Popup("Category", categorySelected, categories.ToArray()); 
                }
                else {
                    if (categories.Count - 1 < EditorPrefs.GetInt("CategoryIndex")) {
                        categorySelected = EditorGUILayout.Popup("Category", categories.Count - 1, categories.ToArray()); 
                    }
                    else {
                        categorySelected = EditorGUILayout.Popup("Category", EditorPrefs.GetInt("CategoryIndex"), categories.ToArray()); 
                    }
                }
            }

            // read the audio files and print their names in dropdown
            PopulateAudios(categories[categorySelected]);
            
            if (audios.Count == 0) {
                return;
            }

            if (EditorPrefs.GetInt("CategoryIndex", -1) == -1) {
                audioSelected = EditorGUILayout.Popup("Ambience Audio", audioSelected, audios.ToArray()); 
            }
            else {
                audioSelected = EditorGUILayout.Popup("Ambience Audio", EditorPrefs.GetInt("AudioIndex"), audios.ToArray()); 
            }

            EditorGUILayout.Space();

            Rect lastRect;
            
            GUILayout.BeginHorizontal();
            {
                // add audio button
                if (GUILayout.Button("Include", IncludeBtnStyle())) {
                    AddAudio(categories[categorySelected], audios[audioSelected]);
                }


                bool isPreviewing = EditorPrefs.GetBool("IsPreviewing");    

                // preview audio button
                if (isPreviewing) {
                    if (GUILayout.Button("Stop", PreviewBtnStyle(isPreviewing))) {
                        StopPreview(script);
                    }
                }
                else {
                    if (GUILayout.Button("Preview", PreviewBtnStyle(isPreviewing))) {
                        if (script.Preview(DropDownInfoToFilePath(categories[categorySelected], audios[audioSelected]))) {
                            EditorPrefs.SetBool("IsPreviewing", true);
                        }
                    }
                }


                lastRect = GUILayoutUtility.GetLastRect();    // button rect    
            }
            GUILayout.EndHorizontal();
            
            lastRect = GUILayoutUtility.GetLastRect();    // horizontal area rect

            if (script.includedAudios.Count > 0 || script.transform.childCount > 0) {
                EditorGUILayout.Space(10);
            }
        }

        void DrawIncludedAudiosList(EqoAmbienceCreator script)
        {
            if (!Application.isPlaying) {
                script.BuildAudioObjectsFromProfile();
                script.PopulateIncludedAudios(IsPlayingAll());
            }

            if (script.ambienceProfile == null) {
                return;
            }


            BuildBlockStates(script);
            RetreiveCollapseStates();


            int max = script.ambienceProfile.includedAudios.Count - 1;

            for (int i=max; i>=0; i--) {
                AmbienceProfile.AudiosData item = script.ambienceProfile.includedAudios[i];
                bool collapseState = GetCollapsedStateOfIndexFromSave(i);
            
    
                // render audio block
                if (!collapseState) {
                    // false is returned if remove btn is clicked in audio block
                    bool printSuccess = DrawAudioBlock(item, script, i);
                    if (!printSuccess) {
                        break;
                    }
                }
                else {
                    // render collapsed audio block
                    DrawCollapsedAudioBlock(i);

                    // make space if another audio is going beneath
                    if (i > 0) {
                        EditorGUILayout.Space(0.5f);
                    }
                }
            }

            
            script.UpdateAudioValues();
        }

        bool DrawAudioBlock(AmbienceProfile.AudiosData item, EqoAmbienceCreator script, int index)
        {
            GUIStyle box = new GUIStyle();
            box.fixedHeight = 193;
            box.normal.background = MakeTex(1, 1, new Color(0, 0, 0, 0.2f));
            box.margin = new RectOffset(2, 2, 2, 4);

            float volumeProp = item.volume;
            float pitch = item.pitch;
            int priority = item.priority;

            bool randomizePlay = item.randomizePlay;
            Vector2 playTime = item.playTime;

            bool randomizeStop = item.randomizeStop;
            Vector2 stopTime = item.stopTime;


            if (randomizePlay) box.fixedHeight += 20;
            if (randomizeStop) box.fixedHeight += 20;


            GUILayout.BeginHorizontal(box);
            {
                GUILayout.BeginVertical();


                // add the name and minimize button
                GUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField(item.name, EditorStyles.boldLabel);
                    
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("-", CollapseBtnStyle())) {
                        ChangeCollapseState(index, true);
                    }
                }
                GUILayout.EndHorizontal();
                

                EditorGUILayout.Space();
                

                // volume property
                GUILayout.BeginHorizontal();
                {
                    volumeProp = EditorGUILayout.Slider("Volume", volumeProp, 0f, 1f);
                }
                GUILayout.EndHorizontal();


                // pitch property
                GUILayout.BeginHorizontal();
                {
                    pitch = EditorGUILayout.Slider("Pitch", pitch, 0f, 1f);
                }
                GUILayout.EndHorizontal();


                // priority property
                GUILayout.BeginHorizontal();
                {
                    priority = EditorGUILayout.IntSlider("Priority", priority, 0, 256);
                }
                GUILayout.EndHorizontal();


                GUILayout.Space(5);
                GuiLine();
                GUILayout.Space(5);


                // randomize play property
                GUILayout.BeginHorizontal();
                {
                    randomizePlay = EditorGUILayout.Toggle("Randomize Play", randomizePlay);
                }
                GUILayout.EndHorizontal();


                // play time property
                if (randomizePlay) {
                    GUILayout.BeginHorizontal();
                    {
                        Vector2 clampedTime = new Vector2(Mathf.Clamp(playTime.x, 0, Mathf.Infinity), Mathf.Clamp(playTime.y, 0, Mathf.Infinity));
                        playTime = EditorGUILayout.Vector2Field("Play Time", clampedTime);
                    }
                    GUILayout.EndHorizontal();
                }


                GUILayout.Space(5f);


                // randomize stop property
                GUILayout.BeginHorizontal();
                {
                    randomizeStop = EditorGUILayout.Toggle("Randomize Stop", randomizeStop);
                }
                GUILayout.EndHorizontal();


                // stop time property
                if (randomizeStop) {
                    GUILayout.BeginHorizontal();
                    {
                        float clipDuration = script.ambienceProfile.includedAudios[index].clip.length;
                        Vector2 clampedTime = new Vector2(Mathf.Clamp(stopTime.x, 0, clipDuration), Mathf.Clamp(stopTime.y, 0, clipDuration));
                        stopTime = EditorGUILayout.Vector2Field("Stop Time", clampedTime);
                    }
                    GUILayout.EndHorizontal();
                }


                GUILayout.Space(18);


                // add the play & remove button
                GUILayout.BeginHorizontal();
                {
                    // check in save if index is playing then render stop button
                    int audioBlockBeingPlayed = EditorPrefs.GetInt("AudioBlockPlaying", -1);

                    if (index == audioBlockBeingPlayed) {
                        if (GUILayout.Button("Stop", PlayBtnStyle(false))) {
                            StopAudioBlock();
                        }
                    }
                    else {
                        // only render the play button if not playing all ambience AND game isn't running
                        bool isPlayingAll = IsPlayingAll();
                        bool isGameRunning = Application.isPlaying;

                        if (!isPlayingAll && !isGameRunning) {
                            if (GUILayout.Button("Play", PlayBtnStyle(true))) {
                                AudioBlockPlay(index);
                            }
                        }
                        else {
                            EditorPrefs.DeleteKey("AudioBlockPlaying");
                        }
                    }

                    
                    GUILayout.FlexibleSpace();
                    

                    if (GUILayout.Button("Remove", RemoveBtnStyle())) {
                        RemoveAudioBtn(script, index);
                        return false;
                    }
                }
                GUILayout.EndHorizontal();


                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();


            // check for changes
            if (item.volume != volumeProp || item.pitch != pitch || item.priority != priority || 
            item.randomizePlay != randomizePlay || item.playTime != playTime || item.randomizeStop != randomizeStop || item.stopTime != stopTime) {
                EditorUtility.SetDirty(script.ambienceProfile);
            }


            AmbienceProfile.AudiosData data = new AmbienceProfile.AudiosData(script.ambienceProfile.includedAudios[index].name, script.ambienceProfile.includedAudios[index].clip, volumeProp, pitch, priority, randomizePlay, playTime, randomizeStop, stopTime);
            script.ambienceProfile.includedAudios[index] = data;


            return true;
        }

        void DrawCollapsedAudioBlock(int index)
        {
            GUIStyle btn = new GUIStyle();
            btn.fontSize = 17;
            btn.fixedHeight = 30;
            btn.normal.background = MakeTex(1, 1, new Color(0, 0, 0, 0.5f));
            btn.active.textColor = Color.white;
            btn.normal.textColor = Color.white;
            btn.margin = new RectOffset(4,4,2,2);
            btn.alignment = TextAnchor.MiddleCenter;


            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(audioBlocks[index].name, btn)) {
                    ChangeCollapseState(index, false);
                }
            }
            GUILayout.EndHorizontal();
        }

        void DrawPlayCurrentAmbienceButton(EqoAmbienceCreator script)
        {
            EditorGUILayout.Space(10);

            bool isPlayingAllAmbience = EditorPrefs.GetBool("IsPlayingAll");
            
            if (isPlayingAllAmbience) {
                if (GUILayout.Button("Stop All", PlayCurrentAmbienceBtnStyle(isPlayingAllAmbience))) {
                    StopAllAmbience(script);
                }

                return;
            }

            // if not playing
            if (GUILayout.Button("Test Ambience", PlayCurrentAmbienceBtnStyle(isPlayingAllAmbience))) {
                PlayAllAmbience(script);
            }
        }

        #endregion

        #region FUNCTIONALITY

        void BuildBlockStates(EqoAmbienceCreator script)
        {
            if (script.includedAudios.Count == 0) {
                EditorPrefs.DeleteKey("AudioBlockStates");
                audioBlocks.Clear();
                StopAudioBlock();
                return;
            }

            // copy the found audios info from includedAudios list to audioBlocks list
            for (int i=0; i<script.includedAudios.Count; i++) {
                if (script.includedAudios[i].transform == null) {
                    continue;
                }
                
                Transform item = script.includedAudios[i].transform;
                bool isFound = false;

                for (int x=0; x<audioBlocks.Count; x++) {
                    Transform item2 = audioBlocks[x].transform;

                    if (item == item2) {
                        isFound = true;
                        ChangeCollapseState(x, GetCollapsedStateOfIndexFromSave(x));
                        break;
                    }
                }

                if (!isFound) {
                    if (item.transform != null) {
                        AudioBlockState audioBlock = new AudioBlockState(item.name, item.transform, false);
                        audioBlocks.Add(audioBlock);
                    }
                }
            }
            
            // check if an audio block in editor list doesn't exist in main list
            for (int i=0; i<audioBlocks.Count; i++) {
                bool isFound = false;
                
                foreach (var item in script.includedAudios) {
                    if (item.transform == audioBlocks[i].transform) {
                        isFound = true;
                        break;
                    }
                }


                if (!isFound) {
                    int playingAudioIndex = GetPlayingAudioIndex();
                    // stop audio if it's the one to be removed
                    if (playingAudioIndex == i) {
                        StopAudioBlock();
                    }

                    audioBlocks.RemoveAt(i);
                    RemoveCollapseStateFromSave(i);

                    // block that is playing becomes one index less after removal of one audio block
                    if (playingAudioIndex >= 0) {
                        EditorPrefs.SetInt("AudioBlockPlaying", playingAudioIndex - 1);
                    }
                }
            }
        }

        void ChangeCollapseState(int index, bool state)
        {
            AudioBlockState audioBlock = new AudioBlockState(audioBlocks[index].name, audioBlocks[index].transform, state);
            audioBlocks[index] = audioBlock;

            SaveCollapseStates();
        }

        void SaveCollapseStates()
        {
            int max = audioBlocks.Count;
            string stringify = "";

            for (int i=0; i<max; i++) {
                if (i >= max-1) {
                    stringify += audioBlocks[i].collapseState;
                }
                else {
                    stringify += audioBlocks[i].collapseState + "-";
                }
            }

            EditorPrefs.SetString("AudioBlockStates", stringify);
        }

        void RetreiveCollapseStates()
        {
            int max = audioBlocks.Count;

            for (int i=0; i<max; i++) {
                AudioBlockState audioBlock = new AudioBlockState(audioBlocks[i].name, audioBlocks[i].transform, GetCollapsedStateOfIndexFromSave(i));
                audioBlocks[i] = audioBlock;
            }
        }

        bool GetCollapsedStateOfIndexFromSave(int index)
        {
            string stringified = EditorPrefs.GetString("AudioBlockStates");
            
            if (stringified == "") {
                return false;
            }
            

            string[] splitArr = stringified.Split(char.Parse("-"));


            if (index > splitArr.Length - 1) {
                return false;
            }


            if (splitArr[index] == "True") {
                return true;
            }


            return false;
        }

        void RemoveCollapseStateFromSave(int index)
        {
            string stringified = EditorPrefs.GetString("AudioBlockStates");
            
            if (stringified == "") {
                return;
            }
            

            string[] splitArr = stringified.Split(char.Parse("-"));
            int max = splitArr.Length;


            if (index > max - 1) {
                return;
            }


            // clear the original string
            stringified = "";
            

            for (int i=0; i<max; i++) {
                if (i != index) {
                    
                    if (i >= max-1) {
                        stringified += splitArr[i];
                    }
                    else {
                        stringified += splitArr[i] + "-";
                    }
                }
            }


            EditorPrefs.SetString("AudioBlockStates", stringified);
        }

        void StopPreview(EqoAmbienceCreator script)
        {
            script.StopPreview();
            EditorPrefs.SetBool("IsPreviewing", false);
        }

        void AudioBlockPlay(int index)
        {
            if (Application.isPlaying) {
                Debug.LogWarning("Can't play specific audio when game is running.");
                return;
            }

            // stops the previous on-going audio
            StopAudioBlock();


            audioBlocks[index].transform.GetComponent<AudioSource>().Play();
            EditorPrefs.SetInt("AudioBlockPlaying", index);
        }

        void StopAudioBlock()
        {
            int audioIndex = GetPlayingAudioIndex();

            if (audioIndex < 0) {
                return;
            }


            if (audioIndex <= audioBlocks.Count - 1) {
                if (audioBlocks[audioIndex].transform != null) {
                    audioBlocks[audioIndex].transform.GetComponent<AudioSource>().Stop();
                }
            }


            EditorPrefs.DeleteKey("AudioBlockPlaying");
        }

        void AddAudio(string category, string audio) 
        {
            EqoAmbienceCreator script = (EqoAmbienceCreator) target;

            if (script.ambienceProfile == null) {
                EditorUtility.DisplayDialog("No Profile", "You need to add a profile first before including audios", "OK");
                return;
            }

            string audioFile = DropDownInfoToFilePath(category, audio);
            
            if (audioFile == null) {
                return;
            }


            EditorPrefs.SetBool("IsPreviewing", false);
            script.AddAudio(audioFile);
            EditorUtility.SetDirty(script.ambienceProfile);


            if (EditorPrefs.GetBool("IsPlayingAll")) {
                script.PlayAudio(script.includedAudios.Count - 1, false);
            }
        }

        void RemoveAudioBtn(EqoAmbienceCreator script, int index)
        {
            if (index < audioBlocks.Count - 1) {
                audioBlocks.RemoveAt(index);
            }

            int playingAudioIndex = GetPlayingAudioIndex();
            if (playingAudioIndex == index) {
                StopAudioBlock();
            }

            script.RemoveAudio(index, false);
            EditorUtility.SetDirty(script.ambienceProfile);
        }

        int GetPlayingAudioIndex()
        {
            return EditorPrefs.GetInt("AudioBlockPlaying", -1);
        }

        void PlayAllAmbience(EqoAmbienceCreator script)
        {
            EditorPrefs.SetBool("IsPlayingAll", true);
            StopAudioBlock();
            script.PlayAll(true);
        }

        void StopAllAmbience(EqoAmbienceCreator script)
        {
            EditorPrefs.SetBool("IsPlayingAll", false);
            
            if (script.ambienceProfile != null) {
                script.StopAll(false);
            }
        }

        bool IsPlayingAll()
        {
            return EditorPrefs.GetBool("IsPlayingAll");
        }

        #endregion

        #region PROFILES

        AmbienceProfile CreateNewProfile(EqoAmbienceCreator script, bool setAsProfile = true)
        {
            AmbienceProfile profile = ScriptableObject.CreateInstance<AmbienceProfile>();
            string path = "Assets/";


            for (int i=0; i<Mathf.Infinity; i++) {

                if (i > 0) path += "Ambience Profile" + i;
                else path += "Ambience Profile";

                bool exists = System.IO.File.Exists($"{path}.asset");

                if (!exists) break;

                path = "Assets/";
            }


            path += ".asset";

            AssetDatabase.CreateAsset(profile, path);            
            EditorUtility.FocusProjectWindow();
            if (setAsProfile) script.ambienceProfile = profile;


            return profile;
        }

        void CloneToProfile(EqoAmbienceCreator script)
        {
            AmbienceProfile newProfile = CreateNewProfile(script, false);
            int max = script.ambienceProfile.includedAudios.Count;

            for (int i=0; i<max; i++) {
                AmbienceProfile.AudiosData item = script.ambienceProfile.includedAudios[i];
                AmbienceProfile.AudiosData newData = new AmbienceProfile.AudiosData(item.name, item.clip, item.volume, item.pitch, item.priority, item.randomizePlay, item.playTime, item.randomizeStop, item.stopTime);
                
                newProfile.includedAudios.Add(newData);
            }
            
            EditorUtility.SetDirty(newProfile);
            script.SaveCurrentProfileAsLastUsed();
            script.ambienceProfile = newProfile;
        }

        #endregion

        #region IO

        // read the resources folder files and populate the categories drop down
        void PopulateCategories()
        {
            string[] filePaths = Directory.GetFiles($"{currentPath}");
            
            foreach (string file in filePaths) {
                // if meta file ignore
                if (file.Contains(".meta")) {
                    continue;
                }


                // get cateogry name
                string catchFileName = file.Substring(file.LastIndexOf("/") + 1);
                string[] splitName = catchFileName.Split('-');
                string categoryName = splitName[0];

                // get file format index
                int dotIndexOfFileFormat = catchFileName.LastIndexOf(".");
                

                // check the file is an actual compatible audio file
                string fileFormat = catchFileName.Substring(dotIndexOfFileFormat);
                if (ArrayUtility.IndexOf(compatibleFormats, fileFormat) < 0) {
                    continue;
                }


                // turn first character to capital
                categoryName = TurnFirstCharToUpper(categoryName);
                

                // check if category already added
                if (categories.Contains(categoryName)) {
                    continue;
                }

                
                categories.Add(categoryName);
            }
        }
        
        // read the files in resources folder and populate the ambience audio drop down
        void PopulateAudios(string category)
        {
            string[] filePaths = Directory.GetFiles($"{currentPath}");
            audios.Clear();
            

            // reset the audio drop down to first item if category changed
            if (categorySelected != EditorPrefs.GetInt("CategoryIndex")) {
                audioSelected = 0;
                EditorPrefs.SetInt("AudioIndex", audioSelected);
            }
            

            foreach (string file in filePaths) {
                // if meta file ignore
                if (file.Contains(".meta")) {
                    continue;
                }


                // remove all the path and get only the actual file name
                string catchFileName = file.Substring(file.LastIndexOf("/") + 1);
                int dotIndexOfFileFormat = catchFileName.LastIndexOf(".");
                

                // check the file is an actual compatible audio file
                string fileFormat = catchFileName.Substring(dotIndexOfFileFormat);
                if (ArrayUtility.IndexOf(compatibleFormats, fileFormat) < 0) {
                    continue;
                }


                // remove the file format from the name (.mp3, .wav, etc...)
                catchFileName = catchFileName.Substring(0, dotIndexOfFileFormat);


                // now the string is like: Night - Silent grass
                string[] splitName = catchFileName.Split('-');
                string currentAudioCategory = splitName[0].ToLower();
                

                // check the category of the audio is the same as the chosen category
                if (currentAudioCategory != category.ToLower()) {
                    continue;
                }


                string fileAudioName = TurnFirstCharToUpper(splitName[1].Trim());
                

                if (audios.Contains(fileAudioName)) {
                    continue;
                }


                audios.Add(fileAudioName);
            }
        }

        // turns first character to capital
        string TurnFirstCharToUpper(string text)
        {
            text = text.ToLower();
            text = char.ToUpper(text[0]) + text.Substring(1, text.Length - 1);

            return text;
        }

        // returns a string of the file name according to the chosen options from both dropdowns
        string DropDownInfoToFilePath(string category, string audio)
        {
            string[] filePaths = Directory.GetFiles($"{currentPath}");

            foreach (string file in filePaths) {
                // if meta file ignore
                if (file.Contains(".meta")) {
                    continue;
                }


                // remove all the path and get only the actual file name without the file format
                string catchFileName = file.Substring(file.LastIndexOf("/") + 1);
                int dotIndexOfFileFormat = catchFileName.LastIndexOf(".");


                // remove the file format from the name (.mp3, .wav, etc...)
                catchFileName = catchFileName.Substring(0, dotIndexOfFileFormat);


                // now the string is like: Night - Quiet night
                string[] splitName = catchFileName.Split('-');
                string currentAudioCategory = splitName[0].ToLower();
                

                // check the category of the audio is the same as the chosen category
                if (currentAudioCategory != category.ToLower()) {
                    continue;
                }

                string fileAudioName = splitName[1].Trim().ToLower();

                // check the audio name is the same as the file name
                if (fileAudioName != audio.ToLower()) {
                    continue;
                }


                // returns ex: Night - Quiet night
                return catchFileName;
            }

            return null;
        }

        string FilterPathToFileName(string filePath)
        {
            // remove all the path and get only the actual file name without the file format
            string catchFileName = filePath.Substring(filePath.LastIndexOf("/") + 1);
            string originalFileName = filePath;
            int dotIndexOfFileFormat = catchFileName.LastIndexOf(".");

            // remove the file format from the name (.mp3, .wav, etc...)
            catchFileName = catchFileName.Substring(0, dotIndexOfFileFormat);


            return catchFileName;
        }

        #endregion

        #region STYLING

        GUIStyle ProfileBtnsStyle()
        {
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fixedWidth = 50;

            return btnStyle;
        }

        GUIStyle PreviewBtnStyle(bool isPreviewing)
        {
            GUIStyle btnStyle = new GUIStyle();

            btnStyle.fontSize = 17;
            btnStyle.normal.textColor = Color.white;
            btnStyle.active.textColor = Color.white;
            btnStyle.margin = new RectOffset(4,4,2,2);
            btnStyle.alignment = TextAnchor.MiddleCenter;
            btnStyle.fixedHeight = 35;


            if (!isPreviewing) {
                btnStyle.normal.background = MakeTex(1, 1, new Color(0.3f, 1f, 0.4f, 0.6f));
                btnStyle.active.background = MakeTex(1, 1, new Color(0.3f, 1f, 0.4f, 0.3f));
                return btnStyle;
            }


            // for stop
            btnStyle.normal.background = MakeTex(1, 1, new Color(1f, 0.3f, 0.4f, 0.6f));
            btnStyle.active.background = MakeTex(1, 1, new Color(1f, 0.3f, 0.4f, 0.3f));
            return btnStyle;
        }

        GUIStyle IncludeBtnStyle()
        {
            GUIStyle btnStyle = new GUIStyle();

            btnStyle.fontSize = 17;
            btnStyle.normal.textColor = Color.white;
            btnStyle.active.textColor = Color.white;
            btnStyle.margin = new RectOffset(4,4,2,2);
            btnStyle.alignment = TextAnchor.MiddleCenter;
            btnStyle.fixedHeight = 35;

            btnStyle.normal.background = MakeTex(1, 1, new Color(1f, 0.6f, 0f, 0.7f));
            btnStyle.active.background = MakeTex(1, 1, new Color(1f, 0.64f, 0f, 0.4f));

            return btnStyle;
        }

        GUIStyle RemoveBtnStyle()
        {
            GUIStyle btnStyle = new GUIStyle();

            btnStyle.fontSize = 14;
            btnStyle.normal.textColor = Color.white;
            btnStyle.active.textColor = Color.white;
            btnStyle.margin = new RectOffset(4,4,4,4);
            btnStyle.alignment = TextAnchor.MiddleCenter;
            btnStyle.fixedHeight = 20;
            btnStyle.fixedWidth = 70;

            btnStyle.normal.background = MakeTex(1, 1, new Color(1f, 0f, 0f, 0.7f));
            btnStyle.active.background = MakeTex(1, 1, new Color(1f, 0f, 0f, 0.4f));
            return btnStyle;
        }

        GUIStyle CollapseBtnStyle()
        {
            GUIStyle btnStyle = new GUIStyle();

            btnStyle.fontSize = 14;
            btnStyle.normal.textColor = Color.white;
            btnStyle.active.textColor = Color.white;
            btnStyle.margin = new RectOffset(2,2,2,2);
            btnStyle.alignment = TextAnchor.MiddleCenter;
            btnStyle.fixedHeight = 15;
            btnStyle.fixedWidth = 30;

            btnStyle.normal.background = MakeTex(1, 1, new Color(0.5f, 0f, 0.5f, 0.8f));
            btnStyle.active.background = MakeTex(1, 1, new Color(0.5f, 0f, 0.5f, 0.4f));

            return btnStyle;
        }

        GUIStyle PlayBtnStyle(bool state)
        {
            GUIStyle btnStyle = new GUIStyle();

            btnStyle.fontSize = 14;
            btnStyle.normal.textColor = Color.white;
            btnStyle.active.textColor = Color.white;
            btnStyle.margin = new RectOffset(4,4,4,4);
            btnStyle.alignment = TextAnchor.MiddleCenter;
            btnStyle.fixedHeight = 20;
            btnStyle.fixedWidth = 70;


            // play btn
            if (state) {
                btnStyle.active.background = MakeTex(1, 1, new Color(0.3f, 1f, 0.4f, 0.3f));
                btnStyle.normal.background = MakeTex(1, 1, new Color(0.3f, 1f, 0.4f, 0.6f));

                return btnStyle;
            }
            

            // stop btn
            btnStyle.normal.background = MakeTex(1, 1, new Color(1f, 0.3f, 0.4f, 0.6f));
            btnStyle.active.background = MakeTex(1, 1, new Color(1f, 0.3f, 0.4f, 0.3f));

            return btnStyle;
        }

        GUIStyle PlayCurrentAmbienceBtnStyle(bool isPlaying)
        {
            GUIStyle btnStyle = new GUIStyle();

            btnStyle.fontSize = 17;
            btnStyle.normal.textColor = Color.white;
            btnStyle.active.textColor = Color.white;
            btnStyle.margin = new RectOffset(4,4,2,2);
            btnStyle.alignment = TextAnchor.MiddleCenter;
            btnStyle.fixedHeight = 35;


            if (!isPlaying) {
                btnStyle.normal.background = MakeTex(1, 1, new Color(0f, 0.55f, 1f, 0.7f));
                btnStyle.active.background = MakeTex(1, 1, new Color(0f, 0.55f, 1f, 0.4f));
                return btnStyle;
            }


            btnStyle.normal.background = MakeTex(1, 1, new Color(1f, 0.3f, 0.4f, 0.6f));
            btnStyle.active.background = MakeTex(1, 1, new Color(1f, 0.3f, 0.4f, 0.3f));
            return btnStyle;
        }

        Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            

            for (int i = 0; i < pix.Length; ++i) {
                pix[i] = col;
            }


            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();


            return result;
        }

        void GuiLine(int i_height = 1)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, i_height);
            rect.height = i_height;
            EditorGUI.DrawRect(rect, new Color (0, 0, 0, 0.2f));
        }

        #endregion
    }
}