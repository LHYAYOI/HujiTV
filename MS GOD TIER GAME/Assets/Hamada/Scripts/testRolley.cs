using Unity.Mathematics;
using UnityEngine;

public class testRolley : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position += transform.forward * Time.deltaTime * 5;

        transform.rotation = transform.rotation * Quaternion.Euler(0, 100 * Time.deltaTime, 0);

        //transform.Rotate(new float3(0, 1, 0), 10 * Time.deltaTime);

    }
}
