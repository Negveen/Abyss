using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Q001Objective01 : MonoBehaviour
{
	public GameObject TheObjective;
	public int CloseObjective;

    void Update()
    {
        if (CloseObjective == 1) 
		{
			if (TheObjective.transform.localScale.y <= 0.0f) 
			{
				CloseObjective = 0;
				TheObjective.SetActive (false);
			} 
			else 
			{
				TheObjective.transform.localScale -= new Vector3 (0.0f, 0.01f, 0.0f);
			}
		}		
	}

	void OnTriggerEnter() 
	{
		QuestManager.SubQuestNumber = 2;
		GetComponent<BoxCollider>().enabled = false;
		StartCoroutine (FinishObjective ());		
	}

	IEnumerator FinishObjective() 
	{
		TheObjective.SetActive (true);
		//TheObjective.transform.localScale = Vector3.one;		
		yield return new WaitForSeconds (1.0f);
		CloseObjective = 1;
	}    
}
