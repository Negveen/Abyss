using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HealthMonitor : MonoBehaviour
{
	public static int HealthValue;
	public int InternalHealth;
	//public GameObject Heart1;
	//public GameObject Heart2;
	//public GameObject Heart3;
	public GameObject healthBar;

	void Start () 
	{
		HealthValue = 500;
	}	

	void Update () 
	{
		InternalHealth = HealthValue;
		if(HealthValue <= 0) {SceneManager.LoadScene(1);}
		healthBar.GetComponent<RectTransform>().sizeDelta = new Vector2(HealthValue, 30);
		
		/*switch(HealthValue) 
		{
			//case 0: SceneManager.LoadScene(1); break;
			case 1:	Heart1.SetActive (true); Heart2.SetActive (false); Heart3.SetActive (false); break;
			case 2:	Heart2.SetActive (true); Heart3.SetActive (false); break;
			case 3: Heart3.SetActive (true); break;
			default: break;
		} */
		/*
		if (HealthValue == 1) 
		{
			Heart1.SetActive (true);
		}
		if (HealthValue == 2) 
		{
			Heart2.SetActive (true);
		}
		if (HealthValue == 3) 
		{
			Heart3.SetActive (true);
		}*/
	}
}
