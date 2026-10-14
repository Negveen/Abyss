using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoblinWalkAI : MonoBehaviour
{
    public int Xpos;
    public int Zpos;
    public GameObject NPCDest;

	void Start () 
	{
        Xpos = Random.Range(439, 442);
        Zpos = Random.Range(431, 381);
        NPCDest.transform.position = new Vector3(Xpos, 5f, Zpos);
        StartCoroutine(RunRandomWalk());
	}	

	void Update () 
	{
        transform.LookAt(NPCDest.transform);
        transform.position = Vector3.MoveTowards(transform.position, NPCDest.transform.position, 0.02f);
	}

    IEnumerator RunRandomWalk ()
    {
        yield return new WaitForSeconds(5);
		Xpos = Random.Range(439, 442);
        Zpos = Random.Range(431, 381);
        NPCDest.transform.position = new Vector3(Xpos, 5f, Zpos);
        StartCoroutine(RunRandomWalk());
    }
}
