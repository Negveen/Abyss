using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterRespawn : MonoBehaviour
{
    public GameObject fadeIn;
    public GameObject thePlayer;

    void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            thePlayer.transform.position = new Vector3(85, 16, 73);
            fadeIn.GetComponent<Animation>().Play("FadeScreenIn");
        }
    }

}

