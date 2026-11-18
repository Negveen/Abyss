using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeartCollect : MonoBehaviour 
{
	public int RotateSpeed;
	public AudioSource CollectSound;
	public GameObject ThisHeart;

	void Update () 
	{
		transform.Rotate (0, RotateSpeed, 0, Space.World);
	}

	void OnTriggerEnter () 
	{
		if (HealthMonitor.HealthValue == 300) 
		{
			//tyt nihyia net, ididte nahui
		}
		if (HealthMonitor.HealthValue > 280 && HealthMonitor.HealthValue < 300) 
		{
			CollectSound.Play ();
			HealthMonitor.HealthValue = 300;
			ThisHeart.SetActive (false);
		}
		if (HealthMonitor.HealthValue <= 280)
		{
			CollectSound.Play ();
			HealthMonitor.HealthValue += 20;
			ThisHeart.SetActive (false);
		}
	}
}
