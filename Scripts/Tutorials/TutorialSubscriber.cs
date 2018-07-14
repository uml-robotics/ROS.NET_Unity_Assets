using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ros_CSharp;
using Messages;

public class TutorialSubscriber : MonoBehaviour {

    public ROSCore rosmaster;
    public string topic_name = "PleaseChooseATopic";
    private NodeHandle nh = null;

    private Subscriber<Messages.std_msgs.String> sub;

    void Start () {
        nh = rosmaster.getNodeHandle();
        sub = nh.subscribe<Messages.std_msgs.String>(topic_name, 1, callback);
    }

    private void callback(Messages.std_msgs.String msg)
    {
        Debug.Log("Recieved Message: " +msg.data);
    }
}
