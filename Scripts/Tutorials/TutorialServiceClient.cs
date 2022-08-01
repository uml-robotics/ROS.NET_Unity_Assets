using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ros_CSharp;
using Messages;

public class TutorialServiceClient : MonoBehaviour {

	public ROSCore rosmaster;
	private NodeHandle nh;
	private ServiceClient<Messages.std_srvs.Trigger> test;

	

	// Use this for initialization
	void Start () {
		nh = rosmaster.getNodeHandle();
		

		test = nh.serviceClient<Messages.std_srvs.Trigger>("testservice");
	}
	
	// Update is called once per frame
	void Update () {

		Messages.std_srvs.Trigger service_request = new Messages.std_srvs.Trigger();

		if (test.call(service_request))
		{
			Debug.Log(service_request.resp.message);
		}
		else
		{
			Debug.Log("Failed to the service");
		}

	}
}
