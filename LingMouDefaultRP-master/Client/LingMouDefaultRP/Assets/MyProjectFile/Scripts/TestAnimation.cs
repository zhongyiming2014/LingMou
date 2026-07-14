using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestAnimation : MonoBehaviour
{
    private Animator ani;
    void Start()
    {
        ani = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            ani.SetInteger("p", 0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ani.SetInteger("p", 1);
        }
        else if(Input.GetKeyDown(KeyCode.Alpha2))
        {
            ani.SetInteger("p", 2);
        }
        //else if (Input.GetKeyDown(KeyCode.Alpha3))
        //{
        //    ani.SetInteger("p", 3);
        //}
        //else if (Input.GetKeyDown(KeyCode.Alpha4))
        //{
        //    ani.SetInteger("p", 4);
        //}
    }
}
