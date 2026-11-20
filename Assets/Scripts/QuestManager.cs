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
    public BaseQuest quest03;

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
                instance.quest03.OnStepChanged(sub);
                break;
        }
    
    } 

    void Update () 
	{
		InternalQuestNumber = ActiveQuestNumber;
        InternalSubNumber = SubQuestNumber;
        Pointer.transform.LookAt(MainMark.transform);
    }
}
