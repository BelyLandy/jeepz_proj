using System.Collections;
using UnityEngine;

public class BezierFollow : MonoBehaviour
{
    [SerializeField]
    private Transform[] routes;

    private int routeToGo;
    private float tParam;
    private Vector2 objectPosition;
    
    [SerializeField]
    private float speedModifier = 0.3f;

    private bool coroutineAllowed = true;
    
    private Vector2 offset;

    void Start()
    {
        routeToGo = 0;
        tParam = 0f;
        coroutineAllowed = true;
        
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            offset = (Vector2)transform.position - (Vector2)bounds.center;
        }
        else
        {
            offset = Vector2.zero;
        }
    }
    
    void Update()
    {
        if (coroutineAllowed)
        {
            StartCoroutine(GoByTheRoute(routeToGo));
        }
    }

    private IEnumerator GoByTheRoute(int routeNum)
    {
        coroutineAllowed = false;

        Vector2 p0 = routes[routeNum].GetChild(0).position;
        Vector2 p1 = routes[routeNum].GetChild(1).position;
        Vector2 p2 = routes[routeNum].GetChild(2).position;
        Vector2 p3 = routes[routeNum].GetChild(3).position;
        
        tParam = 0f;
        while(tParam < 1f)
        {
            tParam += Time.deltaTime * speedModifier;
            
            objectPosition = Mathf.Pow(1 - tParam, 3) * p0 +
                             3 * Mathf.Pow(1 - tParam, 2) * tParam * p1 +
                             3 * (1 - tParam) * Mathf.Pow(tParam, 2) * p2 +
                             Mathf.Pow(tParam, 3) * p3;
            
            transform.position = objectPosition + offset;

            yield return new WaitForEndOfFrame();
        }
        
        routeToGo++;
        if(routeToGo >= routes.Length)
        {
            routeToGo = 0;
        }
        
        coroutineAllowed = true;
    }
}
