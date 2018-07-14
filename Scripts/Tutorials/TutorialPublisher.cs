using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ros_CSharp; //Contains all core c# scripts
//using Messages; //contains all ROS messages

public class TutorialPublisher : MonoBehaviour {
    public ROSCore rosmaster;
    public string topic_name = "PleaseChooseATopic";
    private NodeHandle nh = null;
    private Publisher<Messages.std_msgs.String> pub;
    // Use this for initialization
    void Start () {
        rosmaster.StartROS("http://mjolnir.nrv:11311", "kraken-soft.nrv", "UnityProject1");
        nh = rosmaster.getNodeHandle();
        pub = nh.advertise<Messages.std_msgs.String>(topic_name, 10);

      //  msg = new Messages.std_msgs.String();
       // msg.data = "HELLO!";
       // msg.Serialized = null;

    }
	
	// Update is called once per frame
	void Update () {
        Messages.std_msgs.String msg = new Messages.std_msgs.String();
        msg.data = "HELLO!";
        msg.Serialized = null;
        pub.publish(msg);
    }
}
