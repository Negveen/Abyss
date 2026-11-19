using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Quest001Complete : MonoBehaviour 
{
	public float TheDistance;
	public GameObject ActionDisplay;
	public GameObject ActionText;
	public GameObject UIQuest;
	public GameObject ThePlayer;
	public GameObject ExMark;
	public GameObject CompleteTrigger;
    public GameObject NPCExMark;

    void Update () 
	{
		TheDistance = PlayerCasting.DistanceFromTarget;
	}

	void OnMouseOver () 
	{
		if (TheDistance <= 3) 
		{
			ActionDisplay.SetActive (true);
			ActionText.SetActive (true);
			ActionText.GetComponent<Text> ().text = "Complete Quest";
		}

		if (Input.GetButtonDown ("Action")) 
		{
			if (TheDistance <= 3) 
			{
                QuestManager.SetQuestState(2, 0);
                GlobalCash.GoldAmount += 100;
				PlayerPrefs.SetInt("GoldAmountSave", GlobalCash.GoldAmount);				
				ExMark.SetActive (false);
				GlobalXP.CurrentXP += 100;
				ActionDisplay.SetActive (false);
				ActionText.SetActive (false);
				CompleteTrigger.SetActive (false);
                NPCExMark.SetActive(true);
            }
		}
	}

	void OnMouseExit() 
	{
		ActionDisplay.SetActive (false);
		ActionText.SetActive (false);
	}
}

