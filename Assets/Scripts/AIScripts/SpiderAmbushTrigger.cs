using System.Collections;
using UnityEngine;

public class SpiderAmbushTrigger : MonoBehaviour
{
    public GameObject spider;
    public GameObject spiderModel; // TheEnemy (где висит Animation)
    public Transform exitPoint;    // пустой объект, куда паук выйдет из-за деревьев
    public float exitSpeed = 7f;

    void OnTriggerEnter()
    {
        GetComponent<BoxCollider>().enabled = false;
        StartCoroutine(AmbushRoutine());
    }

   
    IEnumerator AmbushRoutine()
    {
        SpiderAI ai = spider.GetComponent<SpiderAI>();
        if (ai != null) ai.enabled = false;

        Animation anim = spiderModel.GetComponent<Animation>();
        anim["walk"].speed = 2f;

        while (Vector3.Distance(new Vector3(spider.transform.position.x, 0, spider.transform.position.z),
                                new Vector3(exitPoint.position.x, 0, exitPoint.position.z)) > 0.2f)
        {
            anim.Play("walk");
            Vector3 lookPos = new Vector3(exitPoint.position.x, spider.transform.position.y, exitPoint.position.z);
            spider.transform.LookAt(lookPos);

            Vector3 target = new Vector3(exitPoint.position.x, spider.transform.position.y, exitPoint.position.z);
            spider.transform.position = Vector3.MoveTowards(spider.transform.position, target, exitSpeed * Time.deltaTime);
            yield return null;
        }

        anim["walk"].speed = 1f;
        if (ai != null) ai.enabled = true;
    }
    
}