using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour 
{
	public static int ActiveQuestNumber;
	public int InternalQuestNumber;

    public GameObject MainMark;
    public GameObject Pointer;
    public static int SubQuestNumber;
    public int InternalSubNumber;
    public BaseQuest quest01;
    public BaseQuest quest02;

    private static QuestManager instance;
    
    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        SetQuestState(ActiveQuestNumber, SubQuestNumber);
    }

    public static void SetQuestState(int quest, int sub)
    {

        ActiveQuestNumber = quest;
        SubQuestNumber = sub;
        instance.Pointer.SetActive(sub != 0);
        switch (quest)
        {
            case 0:
                MasterQuest.mainQuestName = "No Active Quest";
                MasterQuest.mainQuestInfo = "Check the notice board in the village.";
                break;
            case 1:
                instance.quest01.OnStepChanged(sub);
                break;
            case 2:
                instance.quest02.OnStepChanged(sub);
                break;
            case 3:
                MasterQuest.mainQuestName = "The Cave";
                MasterQuest.mainQuestInfo = "Explore the cave outside the village.";
                break;
        }
    
    }
   /* public static void UpdateQuestText()
    {
        switch (ActiveQuestNumber)
        {
            case 0:
                MasterQuest.mainQuestName = "No Active Quest";
                MasterQuest.mainQuestInfo = "Check the notice board in the village.";
                break;
            case 1:
                MasterQuest.mainQuestName = "My First Weapon";
                if (SubQuestNumber == 1)
                    MasterQuest.mainQuestInfo = "Reach the clearing in the wood.";
                else if (SubQuestNumber == 2)
                    MasterQuest.mainQuestInfo = "Open the chest and take the sword.";
                else if (SubQuestNumber == 3)
                    MasterQuest.mainQuestInfo = "Return to the notice board.";
                break;
            case 2:
                if (SubQuestNumber == 0)
                {
                    MasterQuest.mainQuestName = "Explore the Village";
                    MasterQuest.mainQuestInfo = "Talk to the villager.";
                }
                else if (SubQuestNumber == 4)
                {
                    MasterQuest.mainQuestName = "Spider Problem";
                    MasterQuest.mainQuestInfo = "Return to the villager.";
                }
                else
                {
                    MasterQuest.mainQuestName = "Spider Problem";
                    MasterQuest.mainQuestInfo = "Kill the spiders and their boss outside the village.";
                }
                break;
            case 3:
                MasterQuest.mainQuestName = "The Cave";
                MasterQuest.mainQuestInfo = "Explore the cave outside the village.";
                break;
        }
    }*/

    void Update () 
	{
		InternalQuestNumber = ActiveQuestNumber;
        InternalSubNumber = SubQuestNumber;
        Pointer.transform.LookAt(MainMark.transform);
    }
}
