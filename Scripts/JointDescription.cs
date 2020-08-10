using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JointDescription : MonoBehaviour {
    public enum JointType {
        revolute,
        continuous,
        prismatic,
        fixed_joint,
        floating,
        planar
    };

    [System.Serializable]
    public struct Origin {
        public Vector3 xyz;
        public Vector3 rpy;
    }

    public string joint_name;
    public JointType type;
    public Origin origin;
    public string parent_link;
    public string child_link;
    public Vector3 axis;

    //TODO: Add other fields
}
