using UnityEngine;
using UnityEditor;
using System.IO;
using EqoAmbienceCreatorNS;
using System.Collections.Generic;

namespace EqoAmbienceCreatorNS
{
    [CustomEditor(typeof(AmbienceProfile))]
    public class AmbienceProfileEditor : Editor
    {
        public List<string> categories = new List<string>();
        int categorySelected = 0;

        List<string> audios = new List<string>();
        int audioSelected = 0;

        string pathToResources = "Eqo Ambience Creator/Resources/";
        string[] compatibleFormats = new string[] {".mp3", ".wav", ".ogg", ".aiff"};

        
        #region UNITY METHODS
        
        public override void OnInspectorGUI () 
        {   
            serializedObject.Update();
            AmbienceProfile script = (AmbienceProfile)target;


            RenderChooseAudioSection(script);

            EditorGUILayout.Space(5);

            RenderIncludedAudiosList(script);


            EditorUtility.SetDirty(script);
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region DRAWING

        void RenderChooseAudioSection(AmbienceProfile script)
        {
            EditorGUILayout.LabelField("Choose Ambience Type", EditorStyles.boldLabel);
            PopulateCategories();


            if (categories.Count == 0) {
                EditorGUILayout.LabelField("No Categories Found", EditorStyles.boldLabel);
            }
            else {
                categorySelected = EditorGUILayout.Popup("Category", categorySelected, categories.ToArray());
            }

            
            PopulateAudios(categories[categorySelected]);

            
            if (audios.Count == 0) {
                EditorGUILayout.LabelField("No Audios Found", EditorStyles.boldLabel);
                return;
            }
            else {
                audioSelected = EditorGUILayout.Popup("Ambience Audio", audioSelected, audios.ToArray()); 
            }


            EditorGUILayout.Space(10);

            
            if (GUILayout.Button("Include", IncludeBtnStyle())) {
                AddAudioBtn(script, categories[categorySelected], audios[audioSelected]);
            }
        }


        void RenderIncludedAudiosList(AmbienceProfile script)
        {
            int max = script.includedAudios.Count;

            
            for (int i=max-1; i>=0; i--) {
                GUIStyle box = new GUIStyle();
                box.fixedHeight = 180;
                box.normal.background = MakeTex(1, 1, new Color(0, 0, 0, 0.2f));
                box.margin = new RectOffset(2, 2, 2, 4);
                

                AmbienceProfile.AudiosData item = script.includedAudios[i];
                string name = item.name;
                float volumeProp = item.volume;
                float pitch = item.pitch;
                int priority = item.priority;

                bool randomizePlay = item.randomizePlay;
                Vector2 playTime = item.playTime;

                bool randomizeStop = item.randomizeStop;
                Vector2 stopTime = item.stopTime;


                if (randomizePlay) {
                    if (!randomizeStop) box.fixedHeight = 200;
                    else box.fixedHeight = 220;
                }

                if (randomizeStop) {
                    if (!randomizePlay) box.fixedHeight = 200;
                    else box.fixedHeight = 220;
                }


                GUILayout.BeginHorizontal(box);
                {
                    GUILayout.BeginVertical();

                    // add the name
                    GUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField(item.name, EditorStyles.boldLabel);
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


                    GUILayout.Space(5f);


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
                            float clipDuration = item.clip.length;
                            Vector2 clampedTime = new Vector2(Mathf.Clamp(stopTime.x, 0, clipDuration), Mathf.Clamp(stopTime.y, 0, clipDuration));
                            stopTime = EditorGUILayout.Vector2Field("Stop Time", clampedTime);
                        }
                        GUILayout.EndHorizontal();
                    }


                    EditorGUILayout.Space(10);


                    // add the remove button
                    GUILayout.BeginHorizontal();
                    {   
                        GUILayout.FlexibleSpace();
                        
                        if (GUILayout.Button("Remove", RemoveBtnStyle())) {
                            RemoveAudioBtn(script, i);
                            return;
                        }
                    }
                    GUILayout.EndHorizontal();


                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();


                AmbienceProfile.AudiosData data = new AmbienceProfile.AudiosData(name, item.clip, volumeProp, pitch, priority, randomizePlay, playTime, randomizeStop, stopTime);
                script.includedAudios[i] = data;            
            }
        }

        #endregion

        #region IO

        void PopulateCategories()
        {
            string[] files = Directory.GetFiles(Application.dataPath, pathToResources, SearchOption.AllDirectories);
            categories.Clear();

            
            foreach (var file in files) {
                if (file.Contains(".meta")) {
                    continue;
                }

                // get cateogry name
                string catchFileName = file.Substring(file.LastIndexOf("\\") + 1);
                string[] splitName = catchFileName.Split('-');
                string categoryName = splitName[0];

                // get file format index
                int dotIndexOfFileFormat = catchFileName.LastIndexOf(".");
                

                // check the file is an actual compatible audio format
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
            string[] files = Directory.GetFiles(Application.dataPath, pathToResources, SearchOption.AllDirectories);
            audios.Clear();

            
            foreach (var file in files) {
                // if meta file ignore
                if (file.Contains(".meta")) {
                    continue;
                }


                // remove all the path and get only the actual file name
                string catchFileName = file.Substring(file.LastIndexOf("\\") + 1);
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


            if (audios.Count - 1 < audioSelected) {
                audioSelected = 0;
            }
        }


        void AddAudioBtn(AmbienceProfile script, string category, string audio)
        {
            string audioFile = DropDownInfoToFilePath(category, audio);
            

            if (audioFile == null) {
                return;
            }


            script.AddAudio(audioFile);
        }


        void RemoveAudioBtn(AmbienceProfile script, int index)
        {
            script.RemoveAudio(index);
        }


        // turns first character to capital
        string TurnFirstCharToUpper(string text)
        {
            text = text.ToLower();
            text = char.ToUpper(text[0]) + text.Substring(1, text.Length - 1);
            return text;
        }


        string DropDownInfoToFilePath(string category, string audio)
        {
            string[] files = Directory.GetFiles(Application.dataPath, pathToResources, SearchOption.AllDirectories);

            foreach (string file in files) {
                // if meta file ignore
                if (file.Contains(".meta")) {
                    continue;
                }


                // remove all the path and get only the actual file name without the file format
                string catchFileName = file.Substring(file.LastIndexOf("\\") + 1);
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

        #endregion

        #region STYLING
        
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
        
        #endregion
    }
}