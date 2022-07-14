using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;


public class RosWindow :  EditorWindow{

    string master_uri = "http://00.0.0.000:11311";
    string hostname = "00.0.0.000";
    string nodename = "node";

    string filePath = "Assets/Ros_Profiles";
    string masterRosFile = "Assets/Ros_Profiles/main.json";

    string newFileName = "new";

    string[] options;
    string[] profilePaths;

    int index = 0;


    void PopulateOptions()
    {
        if (!Directory.Exists(filePath))
        {

            Directory.CreateDirectory(filePath);

        }

        string[] dirs = Directory.GetFiles(filePath);

        int len = 0;
        foreach (string dir in dirs)
        {
            if (dir.Contains(".json") && !dir.Contains(".meta") && !dir.Contains("main"))
            {
                len++;
            }
        }
        string[] culledDirs = new string[len];

        int dirIndex = 0;
        foreach(string dir in dirs)
        {
            if (dir.Contains(".json") && !dir.Contains(".meta") && !dir.Contains("main"))
            {
                culledDirs[dirIndex] = dir;
                dirIndex++;
            }
        }

        len = culledDirs.Length;

        options = new string[len];
        profilePaths = new string[len];

        for (int i = 0; i < len; i++)
        {

            string extension = ".json";

            int k = Path.GetFileName(culledDirs[i]).IndexOf(extension);
           

            string fileName = Path.GetFileName(culledDirs[i]).Remove(k, extension.Length);
            options[i] = fileName;
            profilePaths[i] = culledDirs[i];
        }

    }


    [MenuItem("Ros/Ros Settings")]
    public static void ShowWindow()
    {
        GetWindow<RosWindow>("Ros Settings");
    }

   

    

    void UpdateText()
    {
        string profileFile = profilePaths[index];


        ROS_SETTINGS_WINDOW ros_settings = JsonUtility.FromJson<ROS_SETTINGS_WINDOW>(File.ReadAllText(profileFile));
        master_uri = ros_settings.ROS_MASTER_URI;
        nodename = ros_settings.NODENAME;
        hostname = ros_settings.ROS_HOSTNAME;
    }

    void OnGUI()
    {
        GUILayout.Label("Ros Options", EditorStyles.boldLabel);

        PopulateOptions();
        index = EditorGUILayout.Popup("Profiles", index, options);

        master_uri = EditorGUILayout.TextField("Ros Master URI", master_uri);
        hostname = EditorGUILayout.TextField("Ros Hostname", hostname);
        nodename = EditorGUILayout.TextField("Node Name", nodename);

        

        

        if (GUILayout.Button("Save"))
        {
            PlayerPrefs.SetString("ROS_MASTER_URI", master_uri);
            PlayerPrefs.SetString("ROS_HOSTNAME", hostname);
            PlayerPrefs.SetString("NODENAME", nodename);

            Debug.Log("Preferences Saved!");
        }
        if (GUILayout.Button("Load"))
        {
            UpdateText();
            LoadRosProfile();
        }

        GUILayout.Space(20f);

        newFileName = EditorGUILayout.TextField("Profile Name", newFileName);
        if(GUILayout.Button("Save New"))
        {
            CreateNewFile();
        }
        

    }

    void LoadRosProfile()
    {
        StreamWriter sw = new StreamWriter(masterRosFile);


        ROS_SETTINGS_WINDOW config = new ROS_SETTINGS_WINDOW();

        config.NODENAME = nodename;
        config.ROS_HOSTNAME = hostname;
        config.ROS_MASTER_URI = master_uri;

        string jsonData = JsonUtility.ToJson(config);

        sw.WriteLine(jsonData);

        sw.Close();

        Debug.Log("Profile settings have been loaded!");

    }
    void CreateNewFile()
    {
        string path = filePath + "/" + newFileName + ".json";
        File.Create(path).Dispose();
        StreamWriter sw = new StreamWriter(path);



        ROS_SETTINGS_WINDOW config = new ROS_SETTINGS_WINDOW();

        config.NODENAME = nodename;
        config.ROS_HOSTNAME = hostname;
        config.ROS_MASTER_URI = master_uri;

        string jsonData = JsonUtility.ToJson(config);

        sw.WriteLine(jsonData);

        sw.Close();

        
        
    }

    class ROS_SETTINGS_WINDOW
    {
        public string ROS_MASTER_URI;
        public string ROS_HOSTNAME;
        public string NODENAME;
    }

}
