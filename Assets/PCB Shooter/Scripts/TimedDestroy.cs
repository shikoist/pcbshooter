using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimedDestroy : MonoBehaviour
{
    float timer;
    public float timerRate = 2.0f;

    // Start is called before the first frame update
    void Start()
    {
        timer = Time.time + timerRate;
    }

    // Update is called once per frame
    void Update()
    {
        if (timer < Time.time) {
            Destroy(gameObject);
        }
    }
}
