using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ObjectiveCompleteAnim : MonoBehaviour
{
    public QuestPopupUI questPopupUI;
    public float delayBeforeShrink = 0.8f;
    public float shrinkSpeed = 1.5f;
    public Color completeColor = Color.green;

    private Coroutine animRoutine;
    private Text txt;
    private Color originalColor;

    void Awake()
    {
        txt = GetComponent<Text>();
        if (txt != null) originalColor = txt.color;
    }

    void OnDisable()
    {
        transform.localScale = Vector3.one;
        if (txt != null) txt.color = originalColor;
        animRoutine = null;
    }

    public void PlayComplete()
    {
        if (questPopupUI != null && questPopupUI.IsShowing)
        {
            questPopupUI.RequestComplete(this);
            return;
        }

        StartCompleteNow();
    }

    public void StartCompleteNow()
    {
        gameObject.SetActive(true);
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(ShrinkRoutine());
    }

    private IEnumerator ShrinkRoutine()
    {
        transform.localScale = Vector3.one;
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