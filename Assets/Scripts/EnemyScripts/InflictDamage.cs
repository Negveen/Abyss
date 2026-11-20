using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InflictDamage : MonoBehaviour
{
    public int DamageAmount = 5;
	public float TargetDistance;
	public float AllowedRange = 2.7f;
	public AudioSource swingSword;
    public float AttackCooldown = 1.0f;
    private bool canAttack = true;
    void Update () 
	{
        if (Input.GetButtonDown("Fire1") && canAttack && AttackBlocker.BlockSword == 0)
        {
            StartCoroutine(AttackRoutine());
        }
        /*
        if (Input.GetButtonDown ("Fire1") && !SwingSword.isSwinging) 
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
		}	*/
    }
    IEnumerator AttackRoutine()
    {
        canAttack = false;
        if (swingSword != null) swingSword.Play();
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            TargetDistance = hit.distance;
            if (TargetDistance <= AllowedRange)
            {
                hit.transform.SendMessage("DeductPoints", DamageAmount, SendMessageOptions.DontRequireReceiver);
            }
        }
        yield return new WaitForSeconds(AttackCooldown);
        canAttack = true;
    }
}
