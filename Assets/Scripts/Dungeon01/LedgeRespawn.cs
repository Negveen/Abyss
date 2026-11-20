using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LedgeRespawn : MonoBehaviour
{
    public GameObject fadeIn;
    public GameObject thePlayer;

    void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            thePlayer.transform.position = new Vector3(30, -8, 73);
            fadeIn.GetComponent<Animation>().Play("FadeScreenIn");
        }
    }

}

