using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Messages.geometry_msgs;
using Messages.tf;
using tf.net;
using Ros_CSharp;
using System;
public class TfSaver_V2 : MonoBehaviour
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
	private Queue<Messages.tf.tfMessage> static_transforms = new Queue<Messages.tf.tfMessage>();
	BinaryTree tree;
	Dictionary<string , Messages.geometry_msgs.TransformStamped> stampedDictStatic = new Dictionary<string, Messages.geometry_msgs.TransformStamped>();

	const double SEC_TO_NSEC = 1000000000f;

	Publisher<Messages.tf.tfMessage> pub;

	public string upperFrameId;

	//Test Stuff
	public uint storageTime = 10;
	public string link, child;
	public double seconds;
	public bool getReverse;

	void Start()
	{
		nh = rosmaster.getNodeHandle();

		tfstaticsub = nh.subscribe<Messages.tf.tfMessage>("/tf_static", 0, tf_static_callback);
		tfsub = nh.subscribe<Messages.tf.tfMessage>("/tf", 0, tf_callback);
		pub = nh.advertise<Messages.tf.tfMessage>("/tf", 10);

		tree = new BinaryTree(upperFrameId);
		//StartCoroutine(print());
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
        lock (static_transforms)
        {
			static_transforms.Enqueue(msg);
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

					tree.AddTransform(t);
					StartCoroutine(Remove(t));
				}
			}
		}

        //Static queue
        lock (static_transforms)
        {
			while(static_transforms.Count > 0){
				Messages.tf.tfMessage tm = static_transforms.Dequeue();

				foreach (Messages.geometry_msgs.TransformStamped t in tm.transforms)
				{

					tree.AddTransform(t);
					StartCoroutine(Remove(t));
				}
			}
        }

		double nsecs = (double)ROS.GetTime().data.sec + ((double)ROS.GetTime().data.nsec / SEC_TO_NSEC) - seconds;
		if (nsecs < 0) { Debug.Log("[TFCache]: Haven't waited enough yet I guess"); return; }
		Messages.geometry_msgs.TransformStamped m;
		if (!getReverse)
		{
			m = tree.GetTransform(link, child, nsecs);
        }
        else
        {
			m = tree.GetTransformReverse(link, child, nsecs);
        }
		if (m != null)
		{
			//Debug.Log("{" + m.transform.translation.x + "," + m.transform.translation.y + "," + m.transform.translation.z + "}");
			//Debug.Log(" !" + m.header.stamp.data.sec + "." + m.header.stamp.data.nsec);
			//m.header = new Messages.std_msgs.Header();
			//m.header.frame_id = link;
			//m.header.stamp = ROS.GetTime();
			m.child_frame_id = m.child_frame_id + "_delayed";
			m.Serialized = null;


			Messages.tf.tfMessage tfPub = new Messages.tf.tfMessage();
			tfPub.transforms = new Messages.geometry_msgs.TransformStamped[1];
			tfPub.transforms[0] = m;
			pub.publish(tfPub);
		}
	}


	IEnumerator Remove(TransformStamped ts)
	{
		//Remove stored transform after designated time
		yield return new WaitForSeconds(storageTime);

		tree.RemoveTransform(ts);
		//Debug.Log(frameskey + time +  " removed!");
	}

	IEnumerator print()
    {
		yield return new WaitForSeconds(4);
		tree.PrintTree();
		StartCoroutine(print());
    }
}



class Node
{
    public string frame_id;
	public string parent_id;
    public SortedList<double, TransformStamped> frames;
	public Dictionary<string,Node> children;

	public Node(string id,string parentId)
    {
		this.frame_id = id;
		this.parent_id = parentId;
		this.frames = new SortedList<double, TransformStamped>();
		this.children = new Dictionary<string, Node>();
    }
}

class BinaryTree
{
	private const double SEC_TO_NSEC = 1000000000f;
	public Node root { get; set; }

	public BinaryTree()
    {
		this.root = null;
    }

	public BinaryTree(Node n)
    {
		this.root = n;
    }

	public BinaryTree(string top_frame_id)
    {
		Node r = new Node(top_frame_id,null);
		this.root = r;

    }

	public Node Find(Node n, string id)
    {
		//Anytime this function is used please becareful of null returns!
		Queue<Node> q = new Queue<Node>();
		List<Node> visited = new List<Node>();

		//Bfs
		q.Enqueue(n);
		while (q.Count > 0)
		{

			Node currNode = q.Dequeue();
			visited.Add(currNode);

			if(currNode.frame_id == id)
            {
				return currNode;
            }

			foreach (KeyValuePair<string, Node> childEntry in currNode.children)
			{
				if (!visited.Contains(childEntry.Value))
				{
					q.Enqueue(childEntry.Value);
				}
			}
		}

		//Debug.Log("Could not find " + id);
		return null;
	}
	//Mainly for testing reasons
	public void PrintTree()
    {
		Queue<Node> q = new Queue<Node>();
		List<Node> visited = new List<Node>();
		string tree = "";
		//Bfs
		q.Enqueue(this.root);
		while (q.Count > 0)
		{

			Node currNode = q.Dequeue();
			visited.Add(currNode);

			tree += currNode.frame_id + " ";
			foreach (KeyValuePair<string, Node> childEntry in currNode.children)
			{
				if (!visited.Contains(childEntry.Value))
				{
					q.Enqueue(childEntry.Value);
				}
			}
		}

		Debug.Log(tree);
	}

	private TransformStamped SearchFramesForTransform(Node n, double time)
    {
		SortedList<double, TransformStamped> stampedDict = n.frames;

		if (stampedDict.ContainsKey(time)) //You got the exact time, thats crazy
		{
			return stampedDict[time];
		}
		else
		{
			if (time == 0) //Return latest stamped transform
			{
				int i = stampedDict.Count;
				return stampedDict.Values[i - 1];
			}

			int numKeys = stampedDict.Count;
			if (numKeys == 0) { Debug.Log("[TFCache]: Currently no tfs for key " + n.frame_id); return null; }
			if (stampedDict.Keys[0] > time || stampedDict.Keys[numKeys - 1] < time) //Time is ahead or before any records
			{
				Debug.Log("The time you are trying to access is either ahead of our records or before");
				return null;
			}
			else
			{
				if (numKeys == 1)// One value in this key
				{
					return stampedDict[0];
				}
				else if (numKeys > 1)// More than one value
				{
					int min = 0, max = numKeys;
					while (max - min >= 2)
					{
						int mid = (max + min) / 2;
						if (stampedDict.Keys[mid] > time)
						{
							max = mid;
						}
						else
						{
							min = mid;
						}
					}

					double toReturnKey = stampedDict.Keys[(max + min) / 2];
					//Debug.Log(toReturnKey + " " + nTime);
					return stampedDict[toReturnKey];
				}

				return null;
			}
		}
	}

	
	public TransformStamped GetTransform(string parent_id,string child_id,double time)
    {

		Node childNode = Find(this.root, child_id);
		Node parentNode = Find(this.root, parent_id);

		if(parentNode == null || childNode == null)
        {
			Debug.Log("Parent or Child not found");
			return null;
        }

        if (parentNode.children.ContainsKey(child_id)) //No gap between frames
        {
			return SearchFramesForTransform(childNode, time);
        }
        else //Gap between frames :(
        {
			Debug.Log(parent_id + " " + child_id);
			//Queue up parent nodes
			Queue<Node> qNodes = new Queue<Node>();
			qNodes.Enqueue(childNode);
			Node currNode = Find(parentNode, childNode.parent_id);
			qNodes.Enqueue(currNode);
			while(currNode.parent_id != parent_id)
            {
				currNode = Find(parentNode, currNode.parent_id);
				if(currNode == null)
                {
					Debug.Log("Intermedian link " + currNode.parent_id + " not made yet");
					return null;
                }
				//Debug.Log(currNode.frame_id + " queued!");
				qNodes.Enqueue(currNode);
            }
			Debug.Log(qNodes.Count);
			//Make a new transform
			TransformStamped ts = new TransformStamped();
			ts.header = new Messages.std_msgs.Header();
			ts.header.stamp = new Messages.std_msgs.Time();
			ts.header.frame_id = parent_id;
			ts.child_frame_id = child_id;		
			ts.header.stamp.data.sec = (uint)time;
			//Queue of all transforms
			Queue<TransformStamped> qTransforms = new Queue<TransformStamped>();
			while(qNodes.Count > 0)
			{
				Node currQNode = qNodes.Dequeue();
				TransformStamped e = SearchFramesForTransform(currQNode, time);
				if(e == null)
                {
					Debug.Log("Transform not found, returning null INFO:\nID: " + currQNode.frame_id);
					return null;
                }
				qTransforms.Enqueue(e);

            }
			//Add up positions and rotations for new reference position
			TransformStamped before = qTransforms.Dequeue();
			Debug.Log(qTransforms.Count);
			Messages.geometry_msgs.Transform newTransform = new Messages.geometry_msgs.Transform();
			newTransform.translation = new Messages.geometry_msgs.Vector3();
			newTransform.rotation = new Messages.geometry_msgs.Quaternion();
			bool isFirst = true;
			while(qTransforms.Count > 0)
            {
				TransformStamped after = qTransforms.Dequeue();
                if (isFirst)
                {
					//math with before and after

					newTransform = AddTwoTransforms(after.transform, before.transform);

					isFirst = false;
                }
                else
				{ 	
					newTransform = AddTwoTransforms(after.transform, before.transform);
				}
            }

			//return :)
			ts.transform = newTransform;

			return ts;
		}
    }
	public TransformStamped GetTransformReverse(string parent_id,string child_id,double time)
    {

		Node childNode = Find(this.root, child_id);
		Node parentNode = Find(this.root, parent_id);

		if(parentNode == null || childNode == null)
        {
			Debug.Log("Parent or Child not found");
			return null;
        }

        if (parentNode.children.ContainsKey(child_id)) //No gap between frames
        {
			TransformStamped tf = SearchFramesForTransform(childNode, time);
			if(tf == null)
            {
				return null;
            }
			tf.transform.translation.x *= -1;
			tf.transform.translation.y *= -1;
			tf.transform.translation.z *= -1;
			tf.child_frame_id = parent_id;
			tf.header.frame_id = child_id;

			return tf;
        }
        else //Gap between frames :(
        {
			Debug.Log(parent_id + " " + child_id);
			//Queue up parent nodes
			Queue<Node> qNodes = new Queue<Node>();
			Node currNode = Find(parentNode, childNode.parent_id);
			qNodes.Enqueue(currNode);
			while(currNode.parent_id != parent_id)
            {
				currNode = Find(parentNode, currNode.parent_id);
				if(currNode == null)
                {
					Debug.Log("Intermedian link " + currNode.parent_id + " not made yet");
					return null;
                }
				//Debug.Log(currNode.frame_id + " queued!");
				qNodes.Enqueue(currNode);
            }
			//Reverse the queue
			Stack<Node> sNodes = new Stack<Node>();
			sNodes.Push(parentNode);
			while(!(qNodes.Count > 0))
            {
				sNodes.Push(qNodes.Dequeue());
            }
			Debug.Log(sNodes.Count);
			//Make a new transform
			TransformStamped ts = new TransformStamped();
			ts.header = new Messages.std_msgs.Header();
			ts.header.stamp = new Messages.std_msgs.Time();
			ts.header.frame_id = child_id;
			ts.child_frame_id = parent_id;		
			ts.header.stamp.data.sec = (uint)time;
			//Queue of all transforms
			Queue<TransformStamped> qTransforms = new Queue<TransformStamped>();
			while(sNodes.Count > 0)
			{
				Node currQNode = sNodes.Pop();
				TransformStamped e = SearchFramesForTransform(currQNode, time);
				if(e == null)
                {
					if (currQNode.frame_id == this.root.frame_id)
					{
						e = new TransformStamped();
						e.transform = new Messages.geometry_msgs.Transform();
						e.transform.translation = new Messages.geometry_msgs.Vector3();
						e.transform.rotation = new Messages.geometry_msgs.Quaternion();
						e.transform.translation.x = 0;
						e.transform.translation.y = 0;
						e.transform.translation.z = 0;

						e.transform.rotation.x = 0;
						e.transform.rotation.y = 0;
						e.transform.rotation.z = 0;
						e.transform.rotation.w = 0;
					}
					else
					{
						Debug.Log("Transform not found, returning null INFO:\nID: " + currQNode.frame_id);
						return null;
					}
                }
				qTransforms.Enqueue(e);

            }
			//Add up positions and rotations for new reference position
			TransformStamped before = qTransforms.Dequeue();
			Debug.Log(qTransforms.Count);
			Messages.geometry_msgs.Transform newTransform = new Messages.geometry_msgs.Transform();
			newTransform.translation = new Messages.geometry_msgs.Vector3();
			newTransform.rotation = new Messages.geometry_msgs.Quaternion();
			bool isFirst = true;
			while(qTransforms.Count > 0)
            {
				TransformStamped after = qTransforms.Dequeue();
                if (isFirst)
                {
					//math with before and after

					newTransform = AddTwoTransformsReverse(after.transform, before.transform);

					isFirst = false;
                }
                else
				{ 	
					newTransform = AddTwoTransformsReverse(after.transform, before.transform);
				}
            }

			//return :)
			ts.transform = newTransform;

			return ts;
		}
    }

	private Messages.geometry_msgs.Transform AddTwoTransforms(Messages.geometry_msgs.Transform a, Messages.geometry_msgs.Transform b)
    {
		Messages.geometry_msgs.Transform c = new Messages.geometry_msgs.Transform();
		c.translation = new Messages.geometry_msgs.Vector3();
		c.translation.x = a.translation.x + b.translation.x;
		c.translation.y = a.translation.y + b.translation.y;
		c.translation.z = a.translation.z + b.translation.z;

		UnityEngine.Quaternion aQ = new UnityEngine.Quaternion();
		UnityEngine.Quaternion bQ= new UnityEngine.Quaternion();
		aQ.x = (float)a.rotation.x;
		aQ.y = (float)a.rotation.y;
		aQ.z = (float)a.rotation.z;
		aQ.w = (float)a.rotation.w;

		bQ.x = (float)b.rotation.x;
		bQ.y = (float)b.rotation.y;
		bQ.z = (float)b.rotation.z;
		bQ.w = (float)b.rotation.w;

		UnityEngine.Quaternion cQ = bQ * aQ;

		c.rotation = new Messages.geometry_msgs.Quaternion();

		c.rotation.x = cQ.x;
		c.rotation.y = cQ.y;
		c.rotation.z = cQ.z;
		c.rotation.w = cQ.w;
		return c;
    }
	private Messages.geometry_msgs.Transform AddTwoTransformsReverse(Messages.geometry_msgs.Transform a, Messages.geometry_msgs.Transform b)
    {
		Messages.geometry_msgs.Transform c = new Messages.geometry_msgs.Transform();
		c.translation = new Messages.geometry_msgs.Vector3();
		Debug.Log("a " + (a.translation == null).ToString());
		Debug.Log("b " + (b.translation == null).ToString());
		Debug.Log("c " + (c.translation == null).ToString());

		c.translation.x = (a.translation.x + b.translation.x) * -1;
		c.translation.y = (a.translation.y + b.translation.y) * -1;
		c.translation.z = (a.translation.z + b.translation.z) * -1;

		UnityEngine.Quaternion aQ = new UnityEngine.Quaternion();
		UnityEngine.Quaternion bQ= new UnityEngine.Quaternion();
		aQ.x = (float)a.rotation.x;
		aQ.y = (float)a.rotation.y;
		aQ.z = (float)a.rotation.z;
		aQ.w = (float)a.rotation.w;

		bQ.x = (float)b.rotation.x;
		bQ.y = (float)b.rotation.y;
		bQ.z = (float)b.rotation.z;
		bQ.w = (float)b.rotation.w;

		UnityEngine.Quaternion cQ = aQ * bQ;

		c.rotation = new Messages.geometry_msgs.Quaternion();

		c.rotation.x = cQ.x;
		c.rotation.y = cQ.y;
		c.rotation.z = cQ.z;
		c.rotation.w = cQ.w;
		return c;
    }


	public void RemoveTransform(TransformStamped ts)
    {
		double time = (double)ts.header.stamp.data.sec + ((double)ts.header.stamp.data.nsec / SEC_TO_NSEC);

		if (this.root.frame_id == ts.header.frame_id)
		{
			if (this.root.children.ContainsKey(ts.child_frame_id))
			{
				Node child = this.root.children[ts.child_frame_id];
				lock (child.frames)
				{
					if (child.frames.ContainsKey(time))
					{
						child.frames.Remove(time);
						//Debug.Log("removed");
					}
				}
				return;
			}
		}

		Node nodeWithChildId = Find(this.root, ts.child_frame_id);

		
		if (!(nodeWithChildId == null))
		{
			nodeWithChildId.frames.Remove(time);
			//Debug.Log("removed");
		}

	}
	public void AddTransform(TransformStamped ts)
    {
		double time = (double)ts.header.stamp.data.sec + ((double)ts.header.stamp.data.nsec / SEC_TO_NSEC);

		if(this.root.frame_id == ts.header.frame_id)
        {
            if (this.root.children.ContainsKey(ts.child_frame_id))
            {
				Node child = this.root.children[ts.child_frame_id];
				lock (child.frames)
				{
					if (!child.frames.ContainsKey(time))
					{
						child.frames.Add(time, ts);
					}
				}

            }
            else
            {
				Node child = new Node(ts.child_frame_id,ts.header.frame_id);
				child.frames.Add(time, ts);
				this.root.children.Add(ts.child_frame_id, child);
            }
			return;
        }



		Node nodeWithChildId = Find(this.root, ts.child_frame_id);
		
		if(nodeWithChildId == null)
        {
			Node parentNode = Find(this.root, ts.header.frame_id);

			Node child = new Node(ts.child_frame_id,ts.header.frame_id);
			child.frames.Add(time, ts);
			if(parentNode == null)
            {
				Debug.Log(ts.header.frame_id + " has not been logged yet to store + " + ts.child_frame_id);
				return;
            }
			parentNode.children.Add(ts.child_frame_id, child);

			Debug.Log("Node created");
        }
        else
        {
			if (!nodeWithChildId.frames.ContainsKey(time))
			{
				nodeWithChildId.frames.Add(time, ts);
            }
            else
            {
				nodeWithChildId.frames.Remove(time);
				nodeWithChildId.frames.Add(time, ts);
            }
        }
		
    }
}



