using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cutscene01 : MonoBehaviour
{
    public GameObject thePlayer;
    public GameObject cutCam;
    public GameObject cutBars;
    public GameObject miniMap;
    public GameObject fadeIn;
    public GameObject fadeOut;
	public GameObject skeletonObject;
	public GameObject skeletonBoss;

    void OnTriggerEnter(Collider other)
    {
        this.GetComponent<BoxCollider>().enabled = false;
        fadeOut.SetActive(true);
        StartCoroutine(SkeletonCut());
    }

    IEnumerator SkeletonCut()
    {
        yield return new WaitForSeconds(2);
        fadeOut.SetActive(false);
        cutCam.SetActive(true);
        cutBars.SetActive(true);
        thePlayer.SetActive(false);

        miniMap.SetActive(false);
        fadeIn.GetComponent<Animation>().Play("FadeScreenIn");
        yield return new WaitForSeconds(6);
        fadeOut.SetActive(true);
        fadeOut.GetComponent<Animation>().Play("FadeScreenOut");
        yield return new WaitForSeconds(2);
        thePlayer.SetActive(true);
        cutCam.SetActive(false);
        cutBars.SetActive(false);    
        miniMap.SetActive(true);
		skeletonObject.GetComponent<SpiderBossAI>().enabled = true;
		skeletonBoss.GetComponent<Animator>().Play("Skeleton_walk");
        fadeOut.SetActive(false);		
    }
}

