using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ros_CSharp;
using Messages;


/// <summary>
/// NOT FINISHED :3
/// </summary>
public class TutorialServiceServer : MonoBehaviour {

	public ROSCore rosmaster;
	private NodeHandle nh = null;

	void Start () {
		nh = rosmaster.getNodeHandle();

	}
	
	void Update () {
		//ServiceServer serv = nh.advertiseService("/test_service", TestServ);
	}

	private void TestServ()
    {

    }
}
