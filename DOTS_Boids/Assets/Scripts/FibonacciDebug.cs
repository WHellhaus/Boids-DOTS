using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FibonacciDebug : MonoBehaviour
{
    [Range(0f, 1f)]
    public float turnFraction = 0f;
    [Range(0f, 1f)]
    public float pow = 0.5f;
    public int startNum = 0;

    public int numPoints = 0;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void OnDrawGizmos()
    {
        for (int i=0; i < numPoints; i++)
        {
            //float dst = Mathf.Pow((float)i / (numPoints-1f), pow);
            float t = i / (numPoints - 1f);
            float inclination = Mathf.Acos(1 - 2 * t);
            float azimuth = 2 * Mathf.PI * turnFraction * i;
            //float angle = 2 * Mathf.PI * turnFraction * i;

            float x = Mathf.Sin(inclination) * Mathf.Cos(azimuth);
            float y = Mathf.Sin(inclination) * Mathf.Sin(azimuth);
            float z = Mathf.Cos(inclination);
            if (i <= (numPoints/4)*3)
            {
                PlotPoint(x, y, z, Color.blue);
            }
            
        }
    }

    void PlotPoint(float x, float y, float z, Color c)
    {
        Gizmos.color = c;
        Gizmos.DrawSphere(new Vector3(x, y, z), 0.01f);
    }
}
