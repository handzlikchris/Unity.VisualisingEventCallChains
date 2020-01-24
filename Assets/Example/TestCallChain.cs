using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

public class TestCallChain : MonoBehaviour
{
    [Serializable]
    public class UnityEvent : UnityEvent<int>
    {

    }

    public UnityEvent Event = new UnityEvent();

    public bool RunCallChain;

    void Start()
    {
        if (RunCallChain)
        {
            StartCoroutine(ExecuteEvent());
        }
    }

    public void InvokeEvent(int number)
    {
        Thread.Sleep(10);
        Debug.Log($"{name} - {nameof(InvokeEvent)}: {number}");
        Event?.Invoke(number);
    }

    public void PrintChainEnd(int number)
    {
        Thread.Sleep(10);
        Debug.Log($"{name} - Chain End");
    }

    IEnumerator ExecuteEvent()
    {
        while (true)
        {
            if (!RunCallChain) yield break;

            InvokeEvent(Time.frameCount);
            yield return new WaitForSeconds(1);
        }
    }
}
