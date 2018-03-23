using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ros_CSharp;
using Messages;
using System;

public class ROSCore : MonoBehaviour {
    private bool IsROSStarted;
    private NodeHandle nh;

    public bool autostart;
    public string Master_URI;
    public string HOSTNAME;
    //public string 

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
        IsROSStarted = false;
        if (autostart)
            StartROS(Master_URI, HOSTNAME);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
