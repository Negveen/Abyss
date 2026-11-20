using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;

public class NPC001 : MonoBehaviour 
{
	public float TheDistance;
	public GameObject ActionDisplay;
	public GameObject ActionText;
	public GameObject ThePlayer;
	public GameObject TextBox;
	public GameObject NPCName;
	public GameObject NPCText;
	public GameObject GateOpen;
    public GameObject NPCExMark;
	public GameObject Spider;
	public GameObject AmbushTrigger;

    void Update () 
	{
		TheDistance = PlayerCasting.DistanceFromTarget;
	}

	void OnMouseOver () 
	{
		if (TheDistance <= 3) 
		{
			AttackBlocker.BlockSword = 1;
			ActionText.GetComponent<Text> ().text = "Talk";
			ActionDisplay.SetActive (true);
			ActionText.SetActive (true);
		}

		if (Input.GetButtonDown ("Action")) 
		{
			if (TheDistance <= 3) 
			{
				AttackBlocker.BlockSword = 2;
				Screen.lockCursor = false;
				Cursor.visible = true;
				ActionDisplay.SetActive (false);
				ActionText.SetActive (false);
				//ThePlayer.SetActive (false);
				StartCoroutine (NPC001Active ());
			}
		}
	}

	void OnMouseExit() 
	{
		AttackBlocker.BlockSword = 0;
		ActionDisplay.SetActive (false);
		ActionText.SetActive (false);
	}

	IEnumerator NPC001Active () 
	{
        if (QuestManager.ActiveQuestNumber == 2 && QuestManager.SubQuestNumber == 0)
        {
            QuestManager.SetQuestState(2, 1);
            TextBox.SetActive (true);
			NPCName.GetComponent<Text> ().text = "Dave";
			NPCName.SetActive (true);
			NPCText.GetComponent<Text>().text = "We have spider problem. Can you go outside the village, kill the spiders and their boss. Here is the key.";
			GateOpen.SetActive(true);
			NPCText.SetActive(true);
			Spider.SetActive(true);
			AmbushTrigger.SetActive(true);
			yield return new WaitForSeconds(5.5f);

			NPCName.SetActive(false);
			NPCText.SetActive(false);
			TextBox.SetActive(false);
            NPCExMark.SetActive(false);
          
        }
		else 
		{
			TextBox.SetActive (true);
			NPCName.GetComponent<Text> ().text = "Dave";
			NPCName.SetActive (true);
			NPCText.GetComponent<Text>().text = "Hello friend, I may have a quest for you if you wish to accept it. Please come back later on this afternoon.";
			NPCText.SetActive(true);
			yield return new WaitForSeconds(5.5f);
			NPCName.SetActive(false);
			NPCText.SetActive(false);
			TextBox.SetActive(false);
		}		
	}

}

