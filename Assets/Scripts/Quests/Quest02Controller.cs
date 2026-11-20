using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Quest02Controller : BaseQuest
{
    public GameObject mainMark;
    public GameObject spiderMark;      
    public GameObject returnNpcMark;   
    public GameObject npcExMark;       
   

    public override void OnStepChanged(int subQuest)
    {
        if (subQuest == 0)
        {
            MasterQuest.mainQuestName = "In search of adventure";
            MasterQuest.mainQuestInfo = "";
            if (npcExMark != null) npcExMark.SetActive(true);
        }
        else if (subQuest == 1)
        {
            mainMark.transform.position = spiderMark.transform.position;
            MasterQuest.mainQuestName = "Spider Problem";
            MasterQuest.mainQuestInfo = "Kill the spiders and their boss outside the village.";
            
        }
        else if (subQuest == 2)
        {
            mainMark.transform.position = returnNpcMark.transform.position;
            MasterQuest.mainQuestName = "Spider Problem";
            MasterQuest.mainQuestInfo = "Return to Dave.";
        }
    }
}
