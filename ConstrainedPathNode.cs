using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConstrainedPathNode : ScriptableObject 
{
    public Transform previous_Node, next_Node, this_Node;
    public bool cap_Start, cap_End;
    public int index;
    public ConstrainedPathNode(Transform previous, Transform next, Transform self, bool start, bool end, int num)
    {
        previous_Node = previous;
        next_Node = next;
        this_Node = self;
        cap_Start = start;
        cap_End = end;
        index = num;
    }
}
