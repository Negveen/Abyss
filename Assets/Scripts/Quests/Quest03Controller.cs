using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Quest03Controller : BaseQuest
{
    public GameObject mainMark;
    public GameObject objective01Mark; // Cave/CaveEnterance
    public QuestPopupUI questPopupUI;
    public override void OnStepChanged(int subQuest)
    {
        if (subQuest == 1)
        {
            mainMark.transform.position = objective01Mark.transform.position;
            MasterQuest.mainQuestName = "The Cave";
            MasterQuest.mainQuestInfo = "Reach the cave outside the village.";
            if (questPopupUI != null)
                questPopupUI.ShowPopup("The Cave", "Reach the cave", "Explore the cave", "Escape the cave");
        }
    }
}