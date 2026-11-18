using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OpenWorldToDungeon : MonoBehaviour
{  
	public float TheDistance;
	public GameObject ActionDisplay;
	public GameObject ActionText;
	public GameObject ThePlayer;
	public GameObject fadeOut;
	

    void Update()
    {
        TheDistance = PlayerCasting.DistanceFromTarget;
    }
	
	void OnMouseOver () 
	{
		if (TheDistance <= 3) 
		{
			//AttackBlocker.BlockSword = 1;
			ActionDisplay.SetActive (true);
			ActionText.SetActive (true);
			ActionText.GetComponent<Text>().text = "Enter Dungeon";
		}
		
		if (Input.GetButtonDown ("Action")) 
		{
			if (TheDistance <= 3) 
			{
				fadeOut.SetActive(true);
				StartCoroutine(TransferScene());
			}
		}
	}
	
	void OnMouseExit() 
	{
		ActionDisplay.SetActive (false);
		ActionText.SetActive (false);
	}
	
	IEnumerator TransferScene()
	{
		ActionDisplay.SetActive (false);
		ActionText.SetActive (false);
		yield return new WaitForSeconds(2);
		SceneManager.LoadScene(3);
		//ThePlayer.SetActive (false);
	}	
}
