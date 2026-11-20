using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class SpiderBossAttack : MonoBehaviour
{
    public GameObject TheEnemy;
    public GameObject ThePlayer;
    public float AttackRange = 3.5f;
    public int DealingDamage;
    public float HitDelay = 0.35f;     
    public float RecoveryDelay = 1.0f;
    public bool isIntroPlaying = false;

    private SpiderBossEnemy bossStats;
    private NavMeshAgent agent;

    void Start()
    {
        bossStats = GetComponent<SpiderBossEnemy>();
        agent = GetComponent<NavMeshAgent>();
    }
    public void PlayIntro(string animName, float duration)
    {
        StartCoroutine(IntroRoutine(animName, duration));
    }

    void Update()
    {
        if (bossStats != null && bossStats.SpiderStatus == 6) return;
        if (isIntroPlaying) return;
        float dist = Vector3.Distance(transform.position, ThePlayer.transform.position);

        if (dist <= AttackRange)
        {
            if (DealingDamage == 0)
            {
                TheEnemy.GetComponent<Animation>().Play("attack2");
                StartCoroutine(TakingDamage());
            }
        }
        else if (DealingDamage == 0)
        {
            if (agent != null && !agent.isStopped && agent.velocity.sqrMagnitude > 0.1f)
                TheEnemy.GetComponent<Animation>().Play("walk");
            else
                TheEnemy.GetComponent<Animation>().Play("idle");
        }
    }

    IEnumerator TakingDamage()
    {
        DealingDamage = 2;
        yield return new WaitForSeconds(HitDelay);
        if (bossStats == null || bossStats.SpiderStatus != 6)
        {
            float dist = Vector3.Distance(transform.position, ThePlayer.transform.position);
            if (dist <= AttackRange + 0.5f)
            {
                HealthMonitor.HealthValue -= 100;
            }
        }
        yield return new WaitForSeconds(RecoveryDelay);
        DealingDamage = 0;
    }
    IEnumerator IntroRoutine(string animName, float duration)
    {
        yield return new WaitForSeconds(1f);
        isIntroPlaying = true;
        TheEnemy.GetComponent<Animation>().Play(animName);
        yield return new WaitForSeconds(duration);
        isIntroPlaying = false;
    }
}