using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QuestPopupUI : MonoBehaviour
{
    public GameObject activeQuestBox;
    public GameObject objective01;
    public GameObject objective02;
    public GameObject objective03;
    private Coroutine popupRoutine;
    public void ShowPopup(string title, string obj1, string obj2 = "", string obj3 = "")
    {
        if (popupRoutine != null) StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(PopupRoutine(title, obj1, obj2, obj3));
    }
    private IEnumerator PopupRoutine(string title, string obj1, string obj2, string obj3)
    {
        activeQuestBox.SetActive(false);
        objective01.SetActive(false);
        objective02.SetActive(false);
        objective03.SetActive(false);
        activeQuestBox.GetComponent<Text>().text = title;
        yield return new WaitForSeconds(0.5f);
        activeQuestBox.SetActive(true);
        if (!string.IsNullOrEmpty(obj1))
        {
            objective01.GetComponent<Text>().text = obj1;
            yield return new WaitForSeconds(1f);
            objective01.SetActive(true);
        }
        if (!string.IsNullOrEmpty(obj2))
        {
            objective02.GetComponent<Text>().text = obj2;
            yield return new WaitForSeconds(0.5f);
            objective02.SetActive(true);
        }
        if (!string.IsNullOrEmpty(obj3))
        {
            objective03.GetComponent<Text>().text = obj3;
            yield return new WaitForSeconds(0.5f);
            objective03.SetActive(true);
        }
        yield return new WaitForSeconds(9f);
        activeQuestBox.SetActive(false);
        objective01.SetActive(false);
        objective02.SetActive(false);
        objective03.SetActive(false);
        popupRoutine = null;
    }
}
