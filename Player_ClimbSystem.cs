/*
The gist of how the climbing system works is that when activated via Events, 2 points on the climb surface are gathered.
An initial point is the point on the climb surface's mesh thats directly infront of the player, this is effectively the player's position on the mesh.
A second position is found by taking a raycast in the player's forward direction and rotating it based on the joystick input. 
A point is measured 5 units from the player in the direction of the line, this point is the origin of a linecast towards the initial point, what this line hits is the next player position.
*/

using System.Collections;
using UnityEngine;

public class Player_ClimbSystem : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)]
    float maxClimbAcceleration = 10f;
    [SerializeField, Range(0f, 100f)]
    float maxClimbSpeed = 10f;
    [SerializeField, Range(0f, 100f)]
    float surfaceHoldScale = 10f;
    public Rigidbody player;
    float xIn, yIn;
    [SerializeField] Vector2 input;
    [SerializeField] Player_MovementManager movementManager;
    public LineRenderer LineR;
    void OnEnable()
    {
        movementManager.OnFreeClimb += FreeClimb;
        movementManager.OnEdgeClimb += EdgeClimb;
    }
    public bool onLedge, onCorner;
    [SerializeField] ClimbableObject source;
    public void EdgeClimb(object sender, Player_MovementManager.OnMovementEventArgs movementData)
    {
        if (source != movementData.currentClimbObject)
        {
            onLedge = false;
            source = movementData.currentClimbObject;
        }

        Vector3 onLedgePoint = onLedge ? transform.position : movementData.ledgePoint;
        if (!onLedge)
        {
            player.MovePosition(Vector3.Lerp(transform.position, source.MoveAlongLedge(movementData.unnormalized_movementInput.x, onLedgePoint, maxClimbSpeed), maxClimbAcceleration * Time.deltaTime));
            onLedge = true;
        }
        else
        {

            player.MovePosition(source.MoveAlongLedge(movementData.unnormalized_movementInput.x, onLedgePoint, maxClimbSpeed));
            transform.forward = Vector3.Lerp(transform.forward, -source.closestNode.this_Node.right.normalized, 10 * Time.deltaTime);
        }
    }

    void OnDisable()
    {
        movementManager.OnFreeClimb -= FreeClimb;
        movementManager.OnEdgeClimb -= EdgeClimb;

    }
    public int LastVertex, CurrentVertex, triHit, moveToTri, fixPos;
    public Vector3 deltaVertexPos, currentVertexPos, lastVertexPos;
    Mesh mesh, mesh1;
    Transform connectedTransform, connectedTransform1;
    const float PI = Mathf.PI;
    Vector3 currentOffset, nextOffset, offsetCurrent, offsetNext, dir1, lastDirection, direction;
    Vector3 CurrentPos()
    {
        if (!mesh || !connectedTransform || (triHit == 0)) return transform.position;

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Vector3 p0 = vertices[triangles[triHit * 3 + 0]];
        Vector3 p1 = vertices[triangles[triHit * 3 + 1]];
        Vector3 p2 = vertices[triangles[triHit * 3 + 2]];
        p0 = connectedTransform.TransformPoint(p0);
        p1 = connectedTransform.TransformPoint(p1);
        p2 = connectedTransform.TransformPoint(p2);
        Debug.DrawLine(p0, p1, Color.red, .125f);
        Debug.DrawLine(p1, p2, Color.red, .125f);
        Debug.DrawLine(p2, p0, Color.red, .125f);
        return (p0 + p1 + p2) / 3;
    }
    Vector3 NextPos()
    {
        if (!mesh1 || !connectedTransform1 || (moveToTri == 0)) return transform.position;
        Vector3[] vertices1 = mesh1.vertices;
        int[] triangles1 = mesh1.triangles;
        Vector3 p3 = vertices1[triangles1[moveToTri * 3 + 0]];
        Vector3 p4 = vertices1[triangles1[moveToTri * 3 + 1]];
        Vector3 p5 = vertices1[triangles1[moveToTri * 3 + 2]];
        p4 = connectedTransform1.TransformPoint(p4);
        p5 = connectedTransform1.TransformPoint(p5);
        p3 = connectedTransform1.TransformPoint(p3);
        Debug.DrawLine(p3, p4, Color.blue, .125f);
        Debug.DrawLine(p4, p5, Color.blue, .125f);
        Debug.DrawLine(p5, p3, Color.blue, .125f);

        return (p3 + p4 + p5) / 3;
    }
    [SerializeField] bool SurfaceFound;
    [SerializeField] Vector3 CornerCheckPos;
    void AccountForMovement(Player_MovementManager.OnMovementEventArgs movementData)
    {
        dir1 = transform.forward;
        if (Physics.Raycast(transform.position, dir1.normalized, out RaycastHit hit, 2f) && hit.collider)
        {

            hit.collider.transform.TryGetComponent<ClimbableObject>(out source);
            movementManager.FoundLedge = source.ClimbType == ClimbableObject.ClimbTypes.EdgeClimb && movementManager.CanTransition;
            if (movementManager.FoundLedge)
            {
                movementManager.CurrentClimbingSource = source;
            }

            MeshCollider meshCollider = hit.collider as MeshCollider;
            if (meshCollider == null || meshCollider.sharedMesh == null)
                return;
            mesh = meshCollider.sharedMesh;
            connectedTransform = hit.collider.transform;
            triHit = hit.triangleIndex;





            currentOffset = hit.point - CurrentPos();

            offsetCurrent = CurrentPos() + currentOffset;
            //if (!source.isMoving) transform.forward = Vector3.Lerp(transform.forward, -((hit.normal + movementData.contactNormal) / 2), maxClimbAcceleration * Time.deltaTime);
            //else transform.forward = -hit.normal;
            transform.forward = Vector3.Lerp(transform.forward, ((-hit.normal + transform.forward) / 2), 20 * Time.deltaTime);
        }
        SurfaceFound = hit.collider;
        float input_y, input_x;
        if (Vector3.Dot(Camera.main.transform.up, transform.up) >= 0.1)
        {
            input_y = -movementData.unnormalized_movementInput.y;
        }
        else
        {
            input_y = movementData.unnormalized_movementInput.y;
        }
        if (Vector3.Dot(Camera.main.transform.right, transform.right) >= 0)
        {
            input_x = movementData.unnormalized_movementInput.x;
        }
        else
        {
            input_x = -movementData.unnormalized_movementInput.x;
        }
        xIn = PI * (input_x / 2f);
        yIn = PI * ((input_y / 2f) + 0.5f);
        input = new Vector2(Mathf.Sin(xIn), Mathf.Cos(yIn));
        var dir = new Vector3(Mathf.Sin(xIn) / 2, Mathf.Cos(yIn) / 2, 1);

        direction = transform.TransformDirection(dir) * Mathf.Rad2Deg;
        CornerCheckPos = transform.TransformDirection(new Vector3((input.x > 0 ? 1 : -1), (-input_y), hit.distance + .25f));
        CornerCheckPos += transform.position;
        //Physics.Raycast(transform.position, direction.normalized, out RaycastHit moveTo, 10f)
        if (Physics.Linecast(transform.position, transform.position + 5 * direction.normalized, out RaycastHit moveTo) && moveTo.collider && SurfaceFound)
        {
            onCorner = false;
            if (Vector3.Distance(hit.point, moveTo.point) > 0 && input.magnitude > 0.1f)
            {
                MeshCollider meshCollider1 = moveTo.collider as MeshCollider;
                if (meshCollider1 == null || meshCollider1.sharedMesh == null)
                    return;

                moveToTri = moveTo.triangleIndex;
                mesh1 = meshCollider1.sharedMesh;

                connectedTransform1 = moveTo.collider.transform;
                nextOffset = moveTo.point - NextPos();

                offsetNext = NextPos() + nextOffset;

                deltaVertexPos = currentOffset;
                fixPos = 0;
                lastDirection = direction;
            }
            else
            {
                lastDirection = direction;

                Debug.Log("notEnough distance/stationary");
                fixPos = 1;
            }
        }
        else if (Physics.Raycast(CornerCheckPos, transform.right.normalized * (input.x > 0 ? -1 : 1), out RaycastHit moveTo2, 2f))
        {
            Debug.Log("corner found");
            if (Vector3.Distance(hit.point, moveTo2.point) > 0 && input.magnitude > 0.1f)
            {
                onCorner = true;
                MeshCollider meshCollider1 = moveTo2.collider as MeshCollider;
                if (meshCollider1 == null || meshCollider1.sharedMesh == null)
                    return;

                moveToTri = moveTo2.triangleIndex;
                mesh1 = meshCollider1.sharedMesh;

                connectedTransform1 = moveTo2.collider.transform;
                nextOffset = moveTo2.point - NextPos();

                offsetNext = NextPos() + nextOffset;

                deltaVertexPos = currentOffset;
                fixPos = 0;
                lastDirection = direction;
                if (!NavigatingCorner) StartCoroutine(MoveAroundCorner(transform.position, offsetNext - -moveTo2.normal.normalized, -moveTo2.normal));

                //transform.forward = -moveTo2.normal;
            }
            else
            {
                lastDirection = direction;

                Debug.Log("notEnough distance/stationary");
                fixPos = 1;
            }
        }
        else
        {
            Debug.Log("no new position");
            if (SurfaceFound) fixPos = 2;
            else fixPos = -1;

        }

        LineR.SetPosition(1, hit.point);
        LineR.SetPosition(2, moveTo.point);

    }
    public static int GetClosestVertex(RaycastHit aHit, int[] aTriangles)
    {
        var b = aHit.barycentricCoordinate;
        int index = aHit.triangleIndex * 3;
        if (aTriangles == null || index < 0 || index + 2 >= aTriangles.Length)
            return -1;
        if (b.x > b.y)
        {
            if (b.x > b.z)
                return aTriangles[index]; // x
            else
                return aTriangles[index + 2]; // z
        }
        else if (b.y > b.z)
            return aTriangles[index + 1]; // y
        else
            return aTriangles[index + 2]; // z
    }
    public void FreeClimb(object sender, Player_MovementManager.OnMovementEventArgs movementData)
    {
        onLedge = false;
        AccountForMovement(movementData);
    }
    Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal)
    {
        return (direction - normal * Vector3.Dot(direction, normal)).normalized;
    }
    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(CurrentPos(), .25f);
        Gizmos.DrawSphere(NextPos(), .125f);
        Gizmos.DrawLine(CurrentPos(), NextPos());
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(offsetCurrent, .25f);
        Gizmos.DrawSphere(offsetNext, .125f);
        Gizmos.DrawLine(offsetCurrent, offsetNext);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, dir1);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, direction);
        Gizmos.DrawSphere(CornerCheckPos, .25f);

    }
    public void ClearData()
    {
        LastVertex = CurrentVertex = triHit = moveToTri = 0;
        deltaVertexPos = currentVertexPos = lastVertexPos = currentOffset = nextOffset = offsetCurrent = offsetNext = dir1 = lastDirection = direction = Vector3.zero;
        mesh = mesh1 = null;
        connectedTransform = connectedTransform1 = null;
    }
    void LateUpdate()
    {
        if (movementManager.isFreeClimbing)
        {
            if (source.isMoving && fixPos == 0)
            {
                player.MovePosition(Vector3.Lerp((CurrentPos() + deltaVertexPos) - transform.forward.normalized, offsetNext - transform.forward.normalized, maxClimbSpeed / Vector3.Distance((CurrentPos() + deltaVertexPos) - transform.forward.normalized, offsetNext) * .5f * Time.deltaTime));
            }
            // 0 = move between points, 1 = try to be stationary, 2 = position checks failed so remain stationary 
            if (fixPos == 0 && !source.isMoving)
            {
                if (!onCorner)
                {
                    player.MovePosition(Vector3.Lerp(offsetCurrent - transform.forward.normalized, offsetNext - transform.forward.normalized, maxClimbSpeed / Vector3.Distance(offsetCurrent - transform.forward.normalized, offsetNext) * Time.deltaTime));
                }
            }
            else if (fixPos == 1) player.MovePosition((CurrentPos() + deltaVertexPos) - transform.forward.normalized);
            else if (fixPos == 2) player.MovePosition((CurrentPos() + deltaVertexPos) - transform.forward.normalized);

            LineR.SetPosition(0, transform.position);
        }
        if (movementManager.isGrounded && !movementManager.isClimbing)
        {
            ClearData();
        }
    }
    bool NavigatingCorner;
    IEnumerator MoveAroundCorner(Vector3 start, Vector3 end, Vector3 normal)
    {
        NavigatingCorner = true;
        Vector3 startDir = transform.forward;
        Vector3 newSpot;
        float progress = 0;
        while (Vector3.Distance(transform.position, end) > 0.1f)
        {
            newSpot = Vector3.Lerp(start, end, progress);
            transform.forward = Vector3.Slerp(startDir, normal, progress);
            player.transform.position = newSpot;
            progress += 8 * Time.deltaTime;
            yield return new WaitForSecondsRealtime(Time.deltaTime);
        }
        player.transform.position = end;
        yield return new WaitForSeconds(.5f);
        NavigatingCorner = false;
    }
}
