using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Q001 : MonoBehaviour
{
	public float TheDistance;
	public GameObject ActionDisplay;
	public GameObject ActionText;
	public GameObject LeftGate;
	public GameObject RightGate;
	public GameObject SmallSpider;
	public GameObject BossSpider;
	public AudioSource gateOpen;
	
	void Update () 
	{
		TheDistance = PlayerCasting.DistanceFromTarget;
	}

	void OnMouseOver () 
	{
		if (TheDistance <= 3) 
		{
			AttackBlocker.BlockSword = 1;
			ActionText.GetComponent<Text> ().text = "Open Gate";
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
				gateOpen.Play();
				LeftGate.GetComponent<Animation>().Play("LeftGateAnim");
				RightGate.GetComponent<Animation>().Play("RightGateAnim");
				SmallSpider.SetActive(true);
				BossSpider.SetActive(true);
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
