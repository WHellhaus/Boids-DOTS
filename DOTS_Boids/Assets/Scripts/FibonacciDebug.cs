using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class FibonacciDebug : MonoBehaviour
{
    [Range(0f, 1f)]
    public float turnFraction = 0f;
    [Range(0f, 1f)]
    public float pow = 0.5f;
    public int startNum = 0;
    public int numPoints = 0;
    [Range(0, 300)]
    public int numDrawn = 300;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void OnDrawGizmos()
    {
        for (int i = 0; i < numPoints; i++)
        {
            //float dst = Mathf.Pow((float)i / (numPoints-1f), pow);
            float goldenRatio = (1 + math.sqrt(5)) / 2;
            float t = (float) i / numPoints;
            float inclination = math.acos(1 - 2 * t);
            float azimuth = 2 * math.PI * goldenRatio * i;
            //float angle = 2 * Mathf.PI * turnFraction * i;

            float x = math.sin(inclination) * math.cos(azimuth);
            float y = math.sin(inclination) * math.sin(azimuth);
            float z = math.cos(inclination);
            if(i < numDrawn)
                PlotPoint(transform.position.x+x, transform.position.y + y, transform.position.z + z, Color.blue);
        }
        
    }

    private IEnumerator Pause(int p)
    {
        Time.timeScale = 0.1f;
        float pauseEndTime = Time.realtimeSinceStartup + 1;
        while (Time.realtimeSinceStartup < pauseEndTime)
        {
            yield return 0;
        }
        Time.timeScale = 1;
    }

    void PlotPoint(float x, float y, float z, Color c)
    {
        Gizmos.color = c;
        Gizmos.DrawSphere(new Vector3(x, y, z), 0.01f);
    }
}
