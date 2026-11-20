using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GlobalXP : MonoBehaviour
{
	public static int CurrentXP;
	public int InternalXP;
	
	public float healthBarPos;
	public float xPos;
	public Image xpBar;
    public int maxXP = 100;

    void Update () 
	{
		InternalXP = CurrentXP;
        if (xpBar != null && maxXP > 0)
        {
            float targetFill = Mathf.Clamp01((float)CurrentXP / maxXP);
            xpBar.fillAmount = targetFill;
        }
        /*xPos = InternalXP / 2;
		healthBarPos = 675;
		xpBar.GetComponent<RectTransform>().sizeDelta = new Vector2(InternalXP, 30);
		xpBar.transform.position = new Vector2(healthBarPos + xPos - 10,15);*/
    }
}
