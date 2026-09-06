
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Pi : UdonSharpBehaviour
{
    void Start()
    {
        Debug.Log("Pi: " + 3.14159265358979f);
        Debug.Log("Pi: " + 3.14159265358979d);
    }
}
