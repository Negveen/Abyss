using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Q001Objective01 : MonoBehaviour
{
	public GameObject TheObjective;

	void OnTriggerEnter() 
	{
        QuestManager.SetQuestState(1, 2);
        GetComponent<BoxCollider>().enabled = false;
        TheObjective.GetComponent<ObjectiveCompleteAnim>().PlayComplete();
    }

}
