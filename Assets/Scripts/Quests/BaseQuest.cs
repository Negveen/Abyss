using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseQuest : MonoBehaviour
{
    public abstract void OnStepChanged(int subQuest);
}
