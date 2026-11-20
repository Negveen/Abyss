using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SpiderBossAI : MonoBehaviour
{
	public GameObject TheDestination;
	NavMeshAgent TheAgent;
    public SpiderEnemy smallSpider;

    void Start()
    {
        TheAgent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (smallSpider != null && smallSpider.SpiderStatus != 6)
        {
            TheAgent.isStopped = true;
            Vector3 lookPos = new Vector3(TheDestination.transform.position.x, transform.position.y, TheDestination.transform.position.z);
            transform.LookAt(lookPos);
            return;
        }
        TheAgent.isStopped = false;
        TheAgent.SetDestination(TheDestination.transform.position);
    }
}
