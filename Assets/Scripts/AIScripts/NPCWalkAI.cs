using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCWalkAI : MonoBehaviour
{
	private Animator mAnimator;
    public int Xpos;
    public int Zpos;
    public GameObject NPCDest;
	private bool isWalking = false;

	void Start () 
	{
		mAnimator = gameObject.GetComponent<Animator>();
        Xpos = Random.Range(202, 144);
        Zpos = Random.Range(107, 122);
        NPCDest.transform.position = new Vector3(Xpos, 5f, Zpos);
        StartCoroutine(RunRandomWalk());
	}	

	void Update () 
	{
		if (isWalking)
        {
            if (UnityEngine.Vector3.Distance(transform.position, NPCDest.transform.position) > 0.5f)
            {
                transform.LookAt(NPCDest.transform);
                transform.position = UnityEngine.Vector3.MoveTowards(transform.position, NPCDest.transform.position, 0.02f * Time.timeScale);
                mAnimator.SetBool("Walk", true);
            }
            else
            {
                isWalking = false;
                mAnimator.SetBool("Walk", false);
            }
        }
		/*
        transform.LookAt(NPCDest.transform);
        transform.position = Vector3.MoveTowards(transform.position, NPCDest.transform.position, 0.02f*Time.timeScale);
		if (iswalking == false)
		{ 
			mAnimator.SetBool("Walk", false); 
		}
		if (iswalking == true)
		{ 
			mAnimator.SetBool("Walk", true); 
		}*/
	}
	
    IEnumerator RunRandomWalk ()
    {
		isWalking = true;
        yield return new WaitForSeconds(5);
		Xpos = Random.Range(202, 144);
        Zpos = Random.Range(107, 122);
        NPCDest.transform.position = new Vector3(Xpos, 5f, Zpos);
        StartCoroutine(RunRandomWalk());
		// iswalking = false;
    }
}
