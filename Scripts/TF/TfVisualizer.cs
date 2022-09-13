using System;
using System.Collections.Generic;
using XmlRpc_Wrapper;
using gm = Messages.geometry_msgs;
using Messages.tf;
using tf.net;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Ros_CSharp;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
[InitializeOnLoad]
#endif
public class TfVisualizer : MonoBehaviour
{
    public TfVisualizer()
    {
    }

    public ROSCore rosmaster;
    private NodeHandle nh = null;
    private Subscriber<Messages.tf.tfMessage> tfsub, tfstaticsub;
    private Text textmaybe;
    private Queue<Messages.tf.tfMessage> transforms = new Queue<Messages.tf.tfMessage>();

    private volatile int lasthz = 0;
    private volatile int count = 0;
    //private DateTime last = DateTime.Now;

    private Transform Template;
    private Transform Root;

    public string FixedFrame;
    public bool show_labels;
    public bool show_lines;
    public bool show_axes;
    public float axis_scale = 1.0f;
    private float _axis_scale = 1.0f;

    private Dictionary<string, Transform> tree = new Dictionary<string, Transform>();
    private Dictionary<string, List<Transform>> framesMissingParent = new Dictionary<string, List<Transform>>();//string parent Transform child

    public static void hideChildrenInHierarchy(Transform trans)
    {
        for (int i = 0; i < trans.childCount; i++)
            trans.GetChild(i).hideFlags |= HideFlags.HideInHierarchy;
    }

    // Use this for initialization
    void Start()
    {
        if (transform.childCount == 0)
            throw new Exception("Unable to locate the template TFFrame for the TFTree");
        TfTreeManager.Instance.SetTFVisualizer(this);
        Template = transform.GetChild(0);
        Root = (Transform)Instantiate(Template);
        Root.SetParent(transform);
        Template.gameObject.SetActive(false);
        hideChildrenInHierarchy(Template);
        Template.hideFlags |= HideFlags.HideAndDontSave;
#if UNITY_EDITOR
        ObjectNames.SetNameSmart(Root, FixedFrame);
#endif
        Root.GetComponentInChildren<TextMesh>(true).text = FixedFrame;
        tree[FixedFrame] = Root;
        hideChildrenInHierarchy(Root);

        nh = rosmaster.getNodeHandle();

        tfstaticsub = nh.subscribe<Messages.tf.tfMessage>("/tf_static", 0, tf_callback);
        tfsub = nh.subscribe<Messages.tf.tfMessage>("/tf", 0, tf_callback);
    }

    private void tf_callback(tfMessage msg)
    {
        lock (transforms)
        {
            transforms.Enqueue(msg);
            //DateTime now = DateTime.Now;
        }
    }

    private bool IsVisible(string child_frame_id)
    {
        if (tree.ContainsKey(child_frame_id))
        {
            return tree[child_frame_id].GetComponent<TfIsVisible>().isVisible;
        }
        return true;
    }

    // Update is called once per frame
    void Update()
    {
        //only handle one message per frame per update
        Dictionary<string, gm.TransformStamped> tfs = new Dictionary<string, gm.TransformStamped>();
        lock (transforms)
        {
            while (transforms.Count > 0)
            {
                Messages.tf.tfMessage tm = transforms.Dequeue();
                foreach (gm.TransformStamped t in tm.transforms)
                {
                    tfs[t.child_frame_id] = t;
                }
            }
        }

        emTransform[] tfz = Array.ConvertAll<gm.TransformStamped, emTransform>(tfs.Values.ToArray(), (a) => new emTransform(a));
        foreach (emTransform tf in tfz)
        {
            if (!tf.frame_id.StartsWith("/"))
                tf.frame_id = "/" + tf.frame_id;
            if (!tf.child_frame_id.StartsWith("/"))
                tf.child_frame_id = "/" + tf.child_frame_id;

            if (IsVisible(tf.child_frame_id))
            {
                Vector3 pos = tf.UnityPosition.Value;
                Quaternion rot = tf.UnityRotation.Value;
                lock (tree)
                {
                    if (!tree.ContainsKey(tf.child_frame_id))
                    {
                        Transform value1;
                        bool hasparentFrame = tree.TryGetValue(tf.frame_id, out value1);
                        if (hasparentFrame)
                        {
                            Template.SetParent(value1);
                        }

                        Transform newframe = (Transform)Instantiate(Template, Template.localPosition, Template.localRotation);
                        hideChildrenInHierarchy(newframe);
#if UNITY_EDITOR
                        ObjectNames.SetNameSmart(newframe, tf.child_frame_id);
#endif
                        tree[tf.child_frame_id] = newframe;
                        tree[tf.child_frame_id].gameObject.GetComponentInChildren<TextMesh>(true).text = tf.child_frame_id;

                        if (framesMissingParent.ContainsKey(tf.child_frame_id))
                        {
                            List<Transform> frames;
                            if (framesMissingParent.TryGetValue(tf.child_frame_id, out frames))
                            {
                                foreach (Transform frame in frames)
                                {
                                    frame.SetParent(tree[tf.child_frame_id], false);
                                }
                                framesMissingParent.Remove(tf.child_frame_id);
                            }
                        }

                        if (!hasparentFrame)
                        {
                            Debug.LogWarning(string.Format("[TFVisualizer][Update]: The parent ({0}) of {1} is not in the tree yet!", tf.frame_id, tf.child_frame_id));

                            List<Transform> frames;
                            if (framesMissingParent.TryGetValue(tf.frame_id, out frames))
                            {
                                frames.Add(tree[tf.child_frame_id]);
                            }
                            else {
                                frames = new List<Transform>();
                                frames.Add(tree[tf.child_frame_id]);
                                framesMissingParent.Add(tf.frame_id, frames);
                            }
                        }
                    }

                    Transform value;
                    if (tree.TryGetValue(tf.frame_id, out value))
                    {
                        value.gameObject.SetActive(true);
                        tree[tf.child_frame_id].SetParent(value, false);
                    }
                    if (value != null && !value.gameObject.activeInHierarchy)
                        value.gameObject.SetActive(true);
                    tree[tf.child_frame_id].gameObject.SetActive(true);
                    tree[tf.child_frame_id].localPosition = pos;
                    tree[tf.child_frame_id].localRotation = rot;
                    tree[tf.child_frame_id].GetChild(0).localScale = new Vector3(axis_scale, axis_scale, axis_scale);
                }
            }
            else
            {
                tree[tf.child_frame_id].gameObject.SetActive(false);
            }
        }

        AxesHider.update(show_axes);
        LabelCorrector.update(show_labels);
        TransformLineConnector.update(show_lines);
        Root.GetChild(0).localScale = new Vector3(axis_scale, axis_scale, axis_scale);
    }
    public bool queryTransforms(string tfName, out Transform val)
    {
        lock (tree)
            return tree.TryGetValue(tfName, out val);
    }
}
