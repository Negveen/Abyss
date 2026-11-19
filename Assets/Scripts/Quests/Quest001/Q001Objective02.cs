using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Q001Objective02 : MonoBehaviour
{
    public float TheDistance;
	public GameObject TreasureChest;
	public GameObject ActionDisplay;
	public GameObject ActionText;
	public GameObject TheObjective;
	public GameObject TakeSword;

    void Update()
    {
        TheDistance = PlayerCasting.DistanceFromTarget;		
    }
	
	void OnMouseOver () 
	{
		if (TheDistance <= 3) 
		{
			ActionText.GetComponent<Text> ().text = "Open Chest";
			ActionText.SetActive (true);
			ActionDisplay.SetActive (true);
		}
		if (Input.GetButtonDown ("Action")) 
		{
			if (TheDistance <= 3) 
			{
				this.GetComponent<BoxCollider> ().enabled = false;
				TreasureChest.GetComponent<Animation> ().Play ("Q01ChestOpen");
				TakeSword.SetActive (true);
				ActionText.SetActive (false);
				ActionDisplay.SetActive (false);
                TheObjective.GetComponent<ObjectiveCompleteAnim>().PlayComplete();
            }
		}
	}

	void OnMouseExit () 
	{
		ActionDisplay.SetActive (false);
		ActionText.SetActive (false);
	}
}
