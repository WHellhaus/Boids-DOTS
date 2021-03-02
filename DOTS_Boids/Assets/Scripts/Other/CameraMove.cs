using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMove : MonoBehaviour
{

    public float speed = 10.0f;
    public Color skyboxColor;
    public Color fogColor;
    private float translation;
    private float straffe;
    private float vert;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // Input.GetAxis() is used to get the user's input
        // You can furthor set it on Unity. (Edit, Project Settings, Input)
        translation = Input.GetAxis("Vertical") * speed * Time.deltaTime;
        straffe = Input.GetAxis("Horizontal") * speed * Time.deltaTime;

        float up = Input.GetKey(KeyCode.Space) ? 1 : 0;
        float down = Input.GetKey(KeyCode.LeftShift) ? -1 : 0;
        vert = (up + down) * speed * Time.deltaTime;

        transform.Translate(straffe, vert, translation);
    }

    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log("entered");
        if (other.gameObject.layer.Equals(0))
        {
            RenderSettings.fog = true;
            Camera.main.backgroundColor = fogColor;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        //Debug.Log("exited");
        if (other.gameObject.layer.Equals(0))
        {
            Camera.main.backgroundColor = skyboxColor;
            RenderSettings.fog = false;
        }
    }
}
