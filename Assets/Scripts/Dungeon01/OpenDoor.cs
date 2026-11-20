using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OpenDoor : MonoBehaviour
{
	public float TheDistance;
	public GameObject ActionDisplay;
	public GameObject ActionText;
	public GameObject Door;

    public AudioSource DoorOpen;


    void Update () 
	{
		TheDistance = PlayerCasting.DistanceFromTarget;
	}

	void OnMouseOver () 
	{
		if (TheDistance <= 3) 
		{
			AttackBlocker.BlockSword = 1;
			ActionText.GetComponent<Text> ().text = "Open Door";
			ActionDisplay.SetActive (true);
			ActionText.SetActive (true);
		}

		if (Input.GetButtonDown ("Action")) 
		{
			if (TheDistance <= 3) 
			{
                GetComponent<BoxCollider>().enabled = false;
				AttackBlocker.BlockSword = 2;
				ActionDisplay.SetActive (false);
				ActionText.SetActive (false);
				ActionText.GetComponent<Text> ().text = "";
				DoorOpen.Play();
				Door.GetComponent<Animation>().Play("Door1");				
            }
		}
	}

	void OnMouseExit() 
	{
		AttackBlocker.BlockSword = 0;
		ActionDisplay.SetActive (false);
		ActionText.SetActive (false);
	}
}
