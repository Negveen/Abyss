using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;

public class Quest001Buttons : MonoBehaviour 
{
	public GameObject ThePlayer;
	public GameObject NoticeCam;
	public GameObject UIQuest;
	public GameObject ExMark;
	public GameObject TheNotice;
	public GameObject NoticeTrigger;
	public GameObject MiniMap;

    public void AcceptQuest () 
	{
		MiniMap.SetActive(true);        
        ThePlayer.SetActive (true);
		NoticeCam.SetActive (false);
		UIQuest.SetActive (false);
		ExMark.SetActive (false);
		TheNotice.SetActive (false);
		NoticeTrigger.SetActive (false);
        QuestManager.SetQuestState(1, 1);
          
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

	public void DeclineQuest() 
	{
		MiniMap.SetActive(true);
		ThePlayer.SetActive (true);
		NoticeCam.SetActive (false);
		UIQuest.SetActive (false);		
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}
}
