using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // Убираем из сцены шарик спауна
        Transform child1 = transform.GetChild(0);
        child1.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
