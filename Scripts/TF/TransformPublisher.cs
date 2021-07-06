using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ros_CSharp;
using tf.net;

public class TransformPublisher : MonoBehaviour {

    public ROSCore rosmaster;

    private NodeHandle nh = null;
    private Publisher<Messages.tf.tfMessage> tfPub;

    public GameObject trackedObject;

    public string frame_id;
    public string child_frame_id;

    public bool using_gazebo;


    // Use this for initialization
    void Start () {
        NodeHandle nh = rosmaster.getNodeHandle();
        tfPub = nh.advertise<Messages.tf.tfMessage>("/tf", 100);

    }
	
	// Update is called once per frame
	void Update () {
        Messages.tf.tfMessage tfmsg = new Messages.tf.tfMessage();

        Messages.geometry_msgs.TransformStamped[] arr = new Messages.geometry_msgs.TransformStamped[1];
        arr[0] = new Messages.geometry_msgs.TransformStamped();

        tfmsg.transforms = arr;
        Transform trans = trackedObject.transform;
        emTransform ta = new emTransform(trans, ROS.GetTime(), frame_id, child_frame_id);

        Messages.std_msgs.Header hdr = new Messages.std_msgs.Header();
        hdr.frame_id = frame_id;

        hdr.stamp = ROS.GetTime();
        if (!using_gazebo)
            hdr.stamp.data.sec += 18000;

        tfmsg.transforms[0].header = hdr;
        tfmsg.transforms[0].child_frame_id = child_frame_id;
        tfmsg.transforms[0].transform = new Messages.geometry_msgs.Transform();
        tfmsg.transforms[0].transform.translation = ta.origin.ToMsg();
        //tfmsg.transforms[0].transform.translation.z += 1.0;
        tfmsg.transforms[0].transform.rotation = ta.basis.ToMsg();
        tfmsg.Serialized = null;

        tfPub.publish(tfmsg);
    }
}
