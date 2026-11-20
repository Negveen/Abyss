using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiderEnemy : MonoBehaviour
{
	public int EnemyHealth = 10;
	public GameObject TheSpider;
	public int SpiderStatus;
	public int BaseXP = 10;
	public int CalculatedXP;
	public SpiderAI SpiderAIScript;
	public static int GlobalSpider;
    public Quest02Controller quest02;

    void Start()
	{
		SpiderAIScript = GetComponent<SpiderAI>();
	}
	
	void DeductPoints (int DamageAmount) 
	{
		EnemyHealth -= DamageAmount;
	}

	void Update () 
	{
		GlobalSpider = SpiderStatus;
		if (EnemyHealth <= 0) 
		{
			if (SpiderStatus == 0)
			{				
				StartCoroutine (DeathSpider ());
			}
		}
	}

	IEnumerator DeathSpider () 
	{
        Rigidbody rb = GetComponent<Rigidbody>();
       
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
        
        SpiderAIScript.enabled = false;
		SpiderStatus = 6;
        quest02.CheckAllSpidersDead();
        CalculatedXP = BaseXP*GlobalLevel.CurrentLevel;
		GlobalXP.CurrentXP += CalculatedXP;
		yield return new WaitForSeconds (0.5f);
		TheSpider.GetComponent<Animation>().Play("die");		
	}
}
