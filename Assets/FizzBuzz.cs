
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class FizzBuzz : UdonSharpBehaviour
{
    void Start()
    {

    }

    int count = 0;

    public override void Interact()
    {
        count++;
        if (count % 3 == 0 && count % 5 == 0)
        {
            Debug.Log("FizzBuzz");
        }
        else if (count % 3 == 0)
        {
            Debug.Log("Fizz");
        }
        else if (count % 5 == 0)
        {
            Debug.Log("Buzz");
        }
        else
        {
            Debug.Log(count);
        }
    }
}
