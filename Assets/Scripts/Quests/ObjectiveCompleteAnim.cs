using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObjectiveCompleteAnim : MonoBehaviour
{
    public float delayBeforeShrink = 0.8f;
    public float shrinkSpeed = 1.5f;
    public Color completeColor = Color.green;
    private Coroutine animRoutine;
    public void PlayComplete()
    {
        gameObject.SetActive(true);
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(ShrinkRoutine());
    }
    private IEnumerator ShrinkRoutine()
    {
        transform.localScale = Vector3.one;
        Text txt = GetComponent<Text>();
        Color originalColor = txt != null ? txt.color : Color.white;
        if (txt != null) txt.color = completeColor;
        yield return new WaitForSeconds(delayBeforeShrink);
        Vector3 scale = transform.localScale;
        while (scale.y > 0f)
        {
            scale.y = Mathf.MoveTowards(scale.y, 0f, shrinkSpeed * Time.deltaTime);
            transform.localScale = scale;
            yield return null;
        }
        if (txt != null) txt.color = originalColor;
        transform.localScale = Vector3.one;
        gameObject.SetActive(false);
        animRoutine = null;
    }
}
