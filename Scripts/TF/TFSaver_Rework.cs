using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Messages.geometry_msgs;
using Messages.tf;
using tf.net;
using Ros_CSharp;
using System;
public class TFSaver_Rework : MonoBehaviour
{
	/* NOTES
	 *  - The subset of dictionaries dont remove themselves, while they may end up empty I dont know if this
	 *  would cause problems in situations with many different frame interactions
	 * 
	 * 
	 * 
	 */
	public ROSCore rosmaster;
	private NodeHandle nh = null;
	private Subscriber<Messages.tf.tfMessage> tfsub, tfstaticsub;
	private Queue<Messages.tf.tfMessage> transforms = new Queue<Messages.tf.tfMessage>();
	Dictionary<string,SortedList<double, Messages.geometry_msgs.TransformStamped>> stampedDict = new Dictionary<string, SortedList<double, Messages.geometry_msgs.TransformStamped>>();
	Dictionary<string , Messages.geometry_msgs.TransformStamped> stampedDictStatic = new Dictionary<string, Messages.geometry_msgs.TransformStamped>();

	const double SEC_TO_NSEC = 1000000000f;

	Publisher<Messages.tf.tfMessage> pub;

	//Test Stuff
	public uint time = 10, storageTime;
	public string link, child;
	public double seconds;

	void Start()
	{
		nh = rosmaster.getNodeHandle();

		tfstaticsub = nh.subscribe<Messages.tf.tfMessage>("/tf_static", 0, tf_static_callback);
		tfsub = nh.subscribe<Messages.tf.tfMessage>("/tf", 0, tf_callback);
		pub = nh.advertise<Messages.tf.tfMessage>("/tf", 10);
	}

	private void tf_callback(tfMessage msg)
	{
		//Queues recieved transforms to be added to the dictionary
		lock (transforms)
		{
			transforms.Enqueue(msg);
		}
	}

	private void tf_static_callback(tfMessage msg)
	{   //update existing statics - todo
		lock (stampedDictStatic)
		{
			foreach (Messages.geometry_msgs.TransformStamped t in msg.transforms)
			{
				string key = t.header.frame_id + t.child_frame_id;
				Debug.Log("found " + key);
				if (stampedDictStatic.ContainsKey(key))
				{
					stampedDictStatic.Remove(key);
					stampedDictStatic.Add(key, t);
				}
				else
				{
					stampedDictStatic.Add(key, t);
				}

			}
		}
	} //work later

	void Update()
	{
		//Update frame dictionary according to queue of transforms
		Dictionary<string, Messages.geometry_msgs.TransformStamped> tfs = new Dictionary<string, Messages.geometry_msgs.TransformStamped>();
		lock (transforms)
		{
			while (transforms.Count > 0)
			{
				Messages.tf.tfMessage tm = transforms.Dequeue();

				foreach (Messages.geometry_msgs.TransformStamped t in tm.transforms)
				{

					double time = (double)t.header.stamp.data.sec + ((double)t.header.stamp.data.nsec / SEC_TO_NSEC);
					string key = t.header.frame_id + t.child_frame_id;
					lock (stampedDict)
					{
						
						if (!stampedDict.ContainsKey(key))
						{
							SortedList<double, Messages.geometry_msgs.TransformStamped> dict = new SortedList<double, Messages.geometry_msgs.TransformStamped>();
							dict.Add(time, t);
							stampedDict.Add(key, dict);
							StartCoroutine(RemoveTransform(key, time));

						}
						else
						{
							if (!stampedDict[key].ContainsKey(time))
							{
								stampedDict[key].Add(time, t);
								StartCoroutine(RemoveTransform(key, time));
							}

						}
					}
					
				}
			}
		}

		double nsecs = (double)ROS.GetTime().data.sec  + ((double)ROS.GetTime().data.nsec / SEC_TO_NSEC) - seconds;
		
		Messages.geometry_msgs.TransformStamped m = getTransformStamped(link, child,nsecs);
		if(m != null)
        {
			//Debug.Log("{" + m.transform.translation.x + "," + m.transform.translation.y + "," + m.transform.translation.z + "}");
			//Debug.Log(" !" + m.header.stamp.data.sec + "." + m.header.stamp.data.nsec);

			m.child_frame_id = child + "_delayed";
			m.Serialized = null;


			Messages.tf.tfMessage tfPub = new Messages.tf.tfMessage();
			tfPub.transforms = new Messages.geometry_msgs.TransformStamped[1];
			tfPub.transforms[0] = m;
			pub.publish(tfPub);
        }


	}

	public Messages.geometry_msgs.TransformStamped getTransformStamped(string baseFrame, string childFrame, double nTime)
	{
		string key = baseFrame + childFrame;

        if (!stampedDict.ContainsKey(key))
        {
			Debug.Log("No records of stamped transforms between these two frames found.");
			return null;
        }
        else
        {
			if(nTime == 0) //Return latest stamped transform
            {
				int i = stampedDict[key].Count;
				return stampedDict[key].Values[i-1];
            }


			if (stampedDict[key].ContainsKey(nTime)) //You got the exact time, thats crazy
			{
				return stampedDict[key][nTime];
			}
			else
			{
				int numKeys = stampedDict[key].Count;
				if (stampedDict[key].Keys[0] > nTime || stampedDict[key].Keys[numKeys - 1] < nTime) //Time is ahead or before any records
				{
					Debug.Log("The time you are trying to access is either ahead of our records or before");
					return null;
				}
				else
				{
					if (numKeys == 1)// One value in this key
					{
						return stampedDict[key][0];
					}
					else if (numKeys > 1)// More than one value
					{
						int min = 0, max = numKeys;
						while (max - min >= 2)
						{
							int mid = (max + min) / 2;
							if(stampedDict[key].Keys[mid] > nTime)
                            {
								max = mid;
                            }
                            else
                            {
								min = mid;
                            }
						}

						double toReturnKey = stampedDict[key].Keys[(max + min) / 2];
						//Debug.Log(toReturnKey + " " + nTime);
						return stampedDict[key][toReturnKey];
					}


				}
			}
        }
		return null;
	}


	public Messages.geometry_msgs.TransformStamped getTransformStampedStatic(string baseFrame, string childFrame)
    {
		string key = baseFrame + childFrame;

		lock (stampedDictStatic)
		{
			if (stampedDictStatic.ContainsKey(key))
			{
				//Debug.Log(key + " found!");
				//Debug.Log(stampedDictStatic[key].transform.translation.x);
				return stampedDictStatic[key];
			}
			else
			{
				//Debug.Log("Static Transform log of " + key + " was not found");
				return null;
			}
		}
    }
	IEnumerator RemoveTransform(string frameskey, double nTime)
	{
		//Remove stored transform after designated time
		yield return new WaitForSeconds(storageTime);

		stampedDict[frameskey].Remove(nTime);
		//Debug.Log(frameskey + time +  " removed!");
	}
}



