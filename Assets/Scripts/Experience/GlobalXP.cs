using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalXP : MonoBehaviour
{
	public static int CurrentXP;
	public int InternalXP;
	
	public float healthBarPos;
	public float xPos;
	public GameObject xpBar;

	void Update () 
	{
		InternalXP = CurrentXP;
		xPos = InternalXP / 2;
		healthBarPos = 675;
		xpBar.GetComponent<RectTransform>().sizeDelta = new Vector2(InternalXP, 30);
		xpBar.transform.position = new Vector2(healthBarPos + xPos - 10,491);
	}
}
