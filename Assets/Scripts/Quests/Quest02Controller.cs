using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Quest02Controller : BaseQuest
{
    public GameObject mainMark;
    public GameObject objective01Mark; 
    public GameObject objective02Mark; 
    public GameObject objective03Mark;
    public QuestPopupUI questPopupUI;

    public SpiderBossEnemy bossSpider;
    public SpiderEnemy[] smallSpiders;
    public GameObject oldNPC;
    public GameObject newNPC;
    public GameObject npcExMark;
    public GameObject TheObjective;

    public override void OnStepChanged(int subQuest)
    {
        if (subQuest == 0)
        {
            MasterQuest.mainQuestName = "In search of adventure";
            MasterQuest.mainQuestInfo = "";
        }
        else if (subQuest == 1)
        {
            mainMark.transform.position = objective01Mark.transform.position;
            MasterQuest.mainQuestName = "Spider Problem";
            MasterQuest.mainQuestInfo = "Reach the logging camp and open the gate.";
            if (questPopupUI != null)
                questPopupUI.ShowPopup("Spider Problem", "Reach the logging camp", "Kill the spider boss", "Return to Dave");
        }
        else if (subQuest == 2)
        {
            mainMark.transform.position = objective02Mark.transform.position;
            MasterQuest.mainQuestName = "Spider Problem";
            MasterQuest.mainQuestInfo = "Kill the spiders and their boss.";
        }
        else if (subQuest == 3)
        {
            mainMark.transform.position = objective03Mark.transform.position;
            MasterQuest.mainQuestName = "Spider Problem";
            MasterQuest.mainQuestInfo = "Return to Dave.";
        }
    }
    public void CheckAllSpidersDead()
    {
        if (QuestManager.ActiveQuestNumber != 2 || QuestManager.SubQuestNumber != 2) return;
        if (bossSpider == null || bossSpider.SpiderStatus != 6) return;
        foreach (SpiderEnemy s in smallSpiders)
        {
            if (s != null && s.SpiderStatus != 6) return;
        }
        if (oldNPC != null) oldNPC.SetActive(false);
        if (newNPC != null) newNPC.SetActive(true);
        if (npcExMark != null) npcExMark.SetActive(true);

        QuestManager.SetQuestState(2, 3);
        TheObjective.GetComponent<ObjectiveCompleteAnim>().PlayComplete();
    }
}
