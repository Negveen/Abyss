using System.Collections;
using UnityEngine;

public class SpiderAI : MonoBehaviour
{
    public GameObject ThePlayer;
    public float AllowedRange = 40f;
    public float AttackRange = 2.5f;
    public GameObject TheEnemy;
    public float MoveSpeed = 3f;
    public int DealingDamage;

    private Rigidbody rb;
    private SpiderEnemy spiderStats;
    private bool isChasing;
    public float HitDelay = 0.25f;  
    public float RecoveryDelay = 0.65f; 

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        spiderStats = GetComponent<SpiderEnemy>();
    }

    void Update()
    {
        if (spiderStats != null && spiderStats.SpiderStatus == 6)
        {
            isChasing = false;
            return;
        }

        Vector3 lookTarget = new Vector3(ThePlayer.transform.position.x, transform.position.y, ThePlayer.transform.position.z);
        transform.LookAt(lookTarget);

        float dist = Vector3.Distance(transform.position, ThePlayer.transform.position);

        if (dist <= AttackRange)
        {
            isChasing = false;
            if (DealingDamage == 0)
            {
                TheEnemy.GetComponent<Animation>().Play("attack");
                StartCoroutine(TakingDamage());
            }
        }
        else if (dist <= AllowedRange)
        {
            isChasing = true;
            TheEnemy.GetComponent<Animation>().Play("walk");
        }
        else
        {
            isChasing = false;
            TheEnemy.GetComponent<Animation>().Play("idle");
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // ќбнул€ем горизонтальный импульс, чтобы паук не отлетал как м€ч при столкновени€х
        rb.velocity = new Vector3(0f, rb.velocity.y, 0f);

        if (!isChasing) return;

        Vector3 target = new Vector3(ThePlayer.transform.position.x, rb.position.y, ThePlayer.transform.position.z);
        Vector3 nextPos = Vector3.MoveTowards(rb.position, target, MoveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(nextPos);
    }

    IEnumerator TakingDamage()
    {
        DealingDamage = 2;
        yield return new WaitForSeconds(HitDelay);
        if (spiderStats == null || spiderStats.SpiderStatus != 6)
        {
            float dist = Vector3.Distance(transform.position, ThePlayer.transform.position);
            if (dist <= AttackRange + 0.5f)
            {
                HealthMonitor.HealthValue -= 60;
            }
        }
        yield return new WaitForSeconds(RecoveryDelay);
        DealingDamage = 0;
    }
}