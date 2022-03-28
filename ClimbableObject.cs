using System.Collections;
using UnityEngine;

public class ClimbableObject : MonoBehaviour
{
    public enum ClimbTypes { FreeClimb, EdgeClimb, LadderClimb, PoleClimb, MonkeyBars };
    public ClimbTypes ClimbType;
    public bool isMoving;
    [SerializeField] private bool EventOnUse;

    public Vector2 AnchorOffset;
    public Transform startPoint, endPoint;
    public float currentProgress, LedgeLength;
    [SerializeField] Transform[] LedgeMarkers;
    [SerializeField] ConstrainedPathNode[] Nodes;
    public ConstrainedPathNode closestNode;
    bool canChangeDir = true;
    void Awake()
    {
        if (ClimbType == ClimbTypes.EdgeClimb)
        {
            if (LedgeMarkers.Length <= 1)
            {
                Debug.LogWarning("Ledge " + transform.name + " lacks the sufficent number of tracking points...");
                return;
            }
            Nodes = new ConstrainedPathNode[LedgeMarkers.Length];
            float totalLength = 0;
            for (int i = 0; i < LedgeMarkers.Length - 1; i++)
            {

                Nodes[i] = new ConstrainedPathNode(i != 0 ? LedgeMarkers[i - 1] : null, i != LedgeMarkers.Length - 1 ? LedgeMarkers[i + 1] : null, LedgeMarkers[i], i == 0, i == LedgeMarkers.Length - 1, i);


                totalLength += Vector3.Distance(LedgeMarkers[i].position, LedgeMarkers[i + 1].position);
                LedgeMarkers[i].LookAt(LedgeMarkers[i + 1], Vector3.up);
            }
            Nodes[LedgeMarkers.Length - 1] = new ConstrainedPathNode(LedgeMarkers[LedgeMarkers.Length > 2 ? LedgeMarkers.Length - 2 : LedgeMarkers.Length - 1], null, LedgeMarkers[LedgeMarkers.Length - 1], false, true, LedgeMarkers.Length - 1);

            LedgeLength = totalLength;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (AnchorOffset.magnitude <= 0) return;
        Gizmos.color = Color.red;
        Vector3 point1, point2, point3;
        point1 = transform.position;
        point2 = point1 + new Vector3(0, AnchorOffset.y, 0);
        point3 = point2 + new Vector3(AnchorOffset.x, 0, 0);
        Gizmos.DrawLine(transform.position, point1);
        Gizmos.DrawLine(point1, point2);
        Gizmos.DrawLine(point2, point3);
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(point3, 0.25f);
    }
    public Vector3 MoveAlongLedge(float delta, Vector3 pos, float speed)
    {
        float distance;
        if (!closestNode)
        {
            int result = -1; float shortestDistance = Mathf.Infinity;
            Debug.LogError("node array length = " + Nodes.Length);
            for (int x = 0; x < Nodes.Length - 1; x++)
            {
                float d = Vector3.Distance(pos, Nodes[x].this_Node.position);
                if (d < shortestDistance)
                {
                    shortestDistance = d;
                    result = x;
                }
            }
            closestNode = (ConstrainedPathNode)Nodes.GetValue(result);
        }
        if (closestNode.cap_End)
        {
            startPoint = closestNode.previous_Node;
            endPoint = closestNode.this_Node;
        }
        else
        {
            startPoint = closestNode.this_Node;
            endPoint = closestNode.next_Node;
        }
        distance = Vector3.Distance(startPoint.position, endPoint.position);
        Vector3 carrage = GetClosestPointOnFiniteLine(pos, startPoint.position, endPoint.position);
        Debug.DrawRay(carrage, Vector3.up);
        currentProgress = Vector3.Distance(startPoint.position, carrage) / distance;
        Vector3 anchorMovement = Vector3.Lerp(startPoint.position, endPoint.position, (currentProgress + (delta * (speed / distance)) * Time.deltaTime));
        if (canChangeDir)
        {

            if (currentProgress <= 0.01 && delta < 0)
            {
                if (!closestNode.cap_Start)
                {
                    closestNode = Nodes[closestNode.index - 1];
                    startPoint = closestNode.this_Node;
                    endPoint = closestNode.next_Node;
                    canChangeDir = false;
                }

            }
            else if (currentProgress >= 0.99 && delta > 0)
            {
                if (!Nodes[closestNode.index + 1].cap_End)
                {
                    closestNode = Nodes[closestNode.index + 1];
                    startPoint = closestNode.this_Node;
                    endPoint = closestNode.next_Node;
                    canChangeDir = false;
                }

            }

        }
        else if (!canChangeDir) 
        {
            StartCoroutine(LedgeDirChangeCooldown(Time.deltaTime));
        } 

        anchorMovement += closestNode.this_Node.transform.right * AnchorOffset.x;
        anchorMovement += closestNode.this_Node.transform.up * AnchorOffset.y;
        return anchorMovement;
    }
    IEnumerator LedgeDirChangeCooldown(float time)
    {
        yield return new WaitForSeconds(time);
        canChangeDir = true;
    }
    void OnDrawGizmos()
    {
        if (null == startPoint || endPoint == null) return;
        Gizmos.DrawSphere(startPoint.position, .125f);
        Gizmos.DrawSphere(endPoint.position, .125f);
        Gizmos.DrawLine(startPoint.position, endPoint.position);
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(Vector3.Lerp(startPoint.position, endPoint.position, currentProgress), .2f);


    }
    Vector3 GetClosestPointOnFiniteLine(Vector3 point, Vector3 line_start, Vector3 line_end)
    {
        Vector3 line_direction = line_end - line_start;
        float line_length = line_direction.magnitude;
        line_direction.Normalize();
        float project_length = Mathf.Clamp(Vector3.Dot(point - line_start, line_direction), 0f, line_length);
        return line_start + line_direction * project_length;
    }
}

