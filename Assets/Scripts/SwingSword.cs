using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwingSword : MonoBehaviour
{
    public GameObject TheSword;
	public int SwordStatus;
	public static bool isSwinging = false;
	
    void Update()
    {
        if(Input.GetButtonDown("Fire1") && SwordStatus == 0 && AttackBlocker.BlockSword == 0) 
		{
			StartCoroutine (SwingSwordFunction());
		}
    }
	
	IEnumerator SwingSwordFunction()
	{
		isSwinging = true;
		SwordStatus = 1;
		TheSword.GetComponent<Animation>().Play("ElvenSwordAnim");
		SwordStatus = 2;
		yield return new WaitForSeconds (1.0f);
		SwordStatus = 0;
		isSwinging = false;
	}
}
