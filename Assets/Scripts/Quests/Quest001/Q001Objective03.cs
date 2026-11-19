using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Q001Objective03 : MonoBehaviour 
{
	public float TheDistance;
	public GameObject FakeSword;
	public GameObject RealSword;
	public GameObject ActionText;
	public GameObject ActionDisplay;
	public GameObject TheObjective;
	public GameObject ChestBlock;
	public GameObject QuestComplete;
	public GameObject ExMark;

	void Update () 
	{
		TheDistance = PlayerCasting.DistanceFromTarget;		
	}

	void OnMouseOver () 
	{
		if (TheDistance <= 3) 
		{
			ActionText.GetComponent<Text> ().text = "Take Sword";
			ActionText.SetActive (true);
			ActionDisplay.SetActive (true);
		}

		if (Input.GetButtonDown ("Action")) 
		{
			if (TheDistance <= 3) 
			{
                TheObjective.GetComponent<ObjectiveCompleteAnim>().PlayComplete();
                QuestManager.SetQuestState(1, 3);
                this.GetComponent<BoxCollider> ().enabled = false;
				FakeSword.SetActive (false);
				RealSword.SetActive (true);
				ChestBlock.SetActive (true);
				ActionText.SetActive (false);
				ActionDisplay.SetActive (false);
				ExMark.SetActive(true);
				QuestComplete.SetActive(true);               
            }
		}
	}

	void OnMouseExit () 
	{
		ActionDisplay.SetActive (false);
		ActionText.SetActive (false);
	}
}
