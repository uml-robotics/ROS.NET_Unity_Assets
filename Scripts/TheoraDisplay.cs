using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TheoraDisplay : MonoBehaviour {

    public TheoraSubscriber theoraSubscriber;

    void Start () {
        if(theoraSubscriber == null)
            Debug.LogError("[TheoraDisplay][Start]: Please add a TheoraSubscriber to this object!");
        setTexture(theoraSubscriber.getTexture());
    }

    private void setTexture(Texture2D texture) {
        if (gameObject.GetComponent<RawImage>() != null)
        {
            gameObject.GetComponent<RawImage>().texture = texture;
        }
        else
        {
            gameObject.GetComponent<MeshRenderer>().material.mainTexture = texture;
        }
    }
}
