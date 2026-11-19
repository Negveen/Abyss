using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Quest01Controller : BaseQuest
{
    public GameObject mainMark;
    public GameObject objective01Mark;
    public GameObject objective02Mark;
    public GameObject objective03Mark;
    public override void OnStepChanged(int subQuest)
    {
        MasterQuest.mainQuestName = "My First Weapon";
        if (subQuest == 1)
        {
            mainMark.transform.position = objective01Mark.transform.position;
            MasterQuest.mainQuestInfo = "Reach the clearing in the wood.";
        }
        else if (subQuest == 2)
        {
            mainMark.transform.position = objective02Mark.transform.position;
            MasterQuest.mainQuestInfo = "Open the chest and take the sword.";
        }
        else if (subQuest == 3)
        {
            mainMark.transform.position = objective03Mark.transform.position;
            MasterQuest.mainQuestInfo = "Return to the notice board.";
        }
    }
}
