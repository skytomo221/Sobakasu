
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Test : UdonSharpBehaviour
{
    public override void Interact()
    {
        Debug.Log("Hello, world!");
    }
}
