using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ros_CSharp;
using Messages;
using System;
using UnityEditor;
using System.IO;

public class ROSCore : MonoBehaviour {
    private bool IsROSStarted;
    private NodeHandle nh;

    public bool autostart;
    public bool startWithLoadedProfile;

    public TextAsset ROS_CONFIG;

    private string rosProfilePath = "Assets/Ros_Profiles/main.json";
    public NodeHandle getNodeHandle()
    {
        if (IsROSStarted)
        {
            return nh;
        }
        else
        {
            Debug.LogWarning("Could not return nh, ros is not ready yet, try calling StartROS first");
            return null;
        }
    }

    public void StartROS(String master_uri, String hostname, String nodename = "UnityProject")
    {
        if (!IsROSStarted)
        {
            ROS.ROS_HOSTNAME = hostname;
            ROS.ROS_MASTER_URI = master_uri;
            ROS.Init(new String[0], nodename);
            nh = new NodeHandle();
            IsROSStarted = true;
            Debug.Log("ROS Started, Master: " + master_uri + " Hostname: " + hostname + " Node Name: " + nodename);
        }
        else
        {
            Debug.LogWarning("Can't start ROS, it is already started");
        }
    }

    // Use this for initialization
    void Awake()
    {
        if (startWithLoadedProfile)
        {

            ROS_SETTINGS ros_settings = JsonUtility.FromJson<ROS_SETTINGS>(File.ReadAllText(rosProfilePath));

            IsROSStarted = false;

            if (autostart)
            {
                StartROS(ros_settings.ROS_MASTER_URI, ros_settings.ROS_HOSTNAME, ros_settings.NODENAME);
            }

        }
        else if (ROS_CONFIG != null) {
            ROS_SETTINGS ros_settings = JsonUtility.FromJson<ROS_SETTINGS>(ROS_CONFIG.text);

            IsROSStarted = false;

            if (autostart) {
                StartROS(ros_settings.ROS_MASTER_URI, ros_settings.ROS_HOSTNAME, ros_settings.NODENAME);
            }
        }
        else if (System.IO.File.Exists(Application.dataPath + "/ROS.txt")) {
            Debug.LogWarning("You forgot to set ROS_CONFIG, but the default config exists. Please set it as the ROS_CONFIG");
            throw new Exception();
        }
        else {
            Debug.LogError("MISSING ROS CONFIG FILE, I've created a template for you. Please edit it with your ROS_MASTER_URI and ROS_HOSTNAME. Ending Application");
            System.IO.File.WriteAllText(Application.dataPath + "/ROS.txt", "{\r\n\t\"ROS_MASTER_URI\": \"http://fetch1069:11311\",\r\n\t\"ROS_HOSTNAME\": \"10.10.10.57\",\r\n\t\"NODENAME\": \"JordanVR\"\r\n}");
            throw new Exception();
        }

    }

    // Update is called once per frame
    void Update()
    {

    }


    [System.Serializable]
    public class ROS_SETTINGS
    {
        public string ROS_MASTER_URI;
        public string ROS_HOSTNAME;
        public string NODENAME;
    }
}
