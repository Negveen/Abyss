using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InflictDamage : MonoBehaviour
{
    public int DamageAmount = 5;
	public float TargetDistance;
	public float AllowedRange = 2.7f;
	public AudioSource swingSword;
	
	void Update () 
	{
		if (Input.GetButtonDown ("Fire1")&& !SwingSword.isSwinging) 
		{
			RaycastHit hit;
			if (Physics.Raycast (transform.position, transform.TransformDirection (Vector3.forward), out hit)) 
			{
				TargetDistance = hit.distance;
				swingSword.Play();
				if (TargetDistance <= AllowedRange) 
				{
					hit.transform.SendMessage ("DeductPoints", DamageAmount, SendMessageOptions.DontRequireReceiver);
				}
			}
		}		
	}
}
