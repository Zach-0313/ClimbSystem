using System;
using UnityEngine;

public class ClimbDetector : MonoBehaviour
{
    [SerializeField] float checkCone, checkStretch;
    [SerializeField] float checkRadius;
    [SerializeField] float maximumCheckDistance;
    [SerializeField] Player_MovementManager manager;
    [SerializeField] InputHandler InputHandler;
    [SerializeField] RaycastHit climbHit;
    [SerializeField] Material climbingMaterial;
    [SerializeField] Vector3 direction, lastDirection;
    const float PI = Mathf.PI;
    [SerializeField] Vector3 lastClimbResult, lastClimbPosition, currentClimbResult, currentClimbPosition;
    public ClimbableObject currentClimbSource, lastClimbSource, evaluating;
    public event EventHandler<ClimbEventArgs> OnClimbDetected;
    public LineRenderer LineR;
    public class ClimbEventArgs : EventArgs
    {
        public Transform climbTarget;
        public Vector3 LedgePoint, climbNormal;
        public bool FreeClimbDetected, LedgeClimbDetected;
        public Rigidbody connectionToRidgidbody;
        public ClimbableObject source;
        public int Vertex;
    }
    void OnEnable()
    {
        manager.OnValidateClimb += DetectClimbing;
    }
    void OnDisable()
    {
        manager.OnValidateClimb -= DetectClimbing;
    }
    void DetectClimbing(object sender, Player_MovementManager.OnMovementEventArgs movementData)
    {
        ClimbCheck(movementData.unnormalized_movementInput.x, movementData.unnormalized_movementInput.y, movementData.playerBody.transform, movementData.searchFor);
    }
    RaycastHit climbRayCheck()
    {
        RaycastHit hit;
        if (manager.FoundLedge)
        {
            Physics.Raycast(transform.position, transform.forward, out hit, maximumCheckDistance);
            return hit;
        }
        if (Physics.SphereCast(transform.position, checkRadius, direction, out hit, maximumCheckDistance))
        {
            return hit;
        }
        else if(Physics.Raycast(transform.position, lastDirection, out hit, maximumCheckDistance))
        {
            return hit;
        }
        else
        {
            Physics.Raycast(transform.position, transform.forward + (direction * .25f), out hit, maximumCheckDistance);
            
            Debug.DrawLine(transform.position, hit.point, Color.blue);
                return hit;
        }


    }
    void ClimbCheck(float x, float y, Transform playerTransform, ClimbableObject.ClimbTypes filter)
    {
        float xIn, yIn;
        xIn = PI * (x / 2f);
        yIn = PI * ((-y / 2f) + 0.5f);

        var dir = new Vector3(Mathf.Sin(xIn) * checkCone / 2, Mathf.Cos(yIn) * checkCone / 2, checkStretch);
        direction = playerTransform.TransformDirection(dir) * Mathf.Rad2Deg;
        climbHit = climbRayCheck();

        if (climbHit.collider)
        {
            if (!climbHit.collider.gameObject.TryGetComponent<ClimbableObject>(out evaluating))
            {
                Debug.Log("Climb Check failed... it was " + climbHit.collider.name);
                return;
            }
        }
        else return;

        ClimbEventArgs climbEventArgs = new ClimbEventArgs();
        climbEventArgs.source = evaluating;
        if (filter != ClimbableObject.ClimbTypes.FreeClimb)
        {
            switch (evaluating.ClimbType)
            {
                case ClimbableObject.ClimbTypes.EdgeClimb:
                    lastClimbSource = currentClimbSource;
                    lastClimbPosition = currentClimbPosition;
                    currentClimbSource = evaluating;
                    currentClimbPosition = climbHit.point;
                    if (filter != evaluating.ClimbType) break;

                    climbEventArgs.climbTarget = evaluating.transform;
                    climbEventArgs.LedgePoint = climbHit.point;
                    climbEventArgs.connectionToRidgidbody = climbHit.rigidbody;
                    climbEventArgs.climbNormal = climbHit.normal;
                    climbEventArgs.LedgeClimbDetected = true;

                    Debug.Log($"Ledge found at checked point = {evaluating.name}");

                    break;
                case ClimbableObject.ClimbTypes.FreeClimb:
                    lastClimbSource = currentClimbSource;
                    lastClimbPosition = currentClimbPosition;
                    currentClimbSource = evaluating;
                    currentClimbPosition = climbHit.point;
                    if (filter != evaluating.ClimbType) break;
                    climbEventArgs.climbTarget = evaluating.transform;
                    climbEventArgs.LedgePoint = climbHit.point;

                    MeshCollider meshc = climbHit.collider as MeshCollider;
                    Mesh mesh = meshc.sharedMesh;
                    int[] triangles = mesh.triangles;
                    climbEventArgs.Vertex = climbHit.triangleIndex;
                    climbEventArgs.connectionToRidgidbody = climbHit.rigidbody;
                    climbEventArgs.climbNormal = climbHit.normal;
                    climbEventArgs.FreeClimbDetected = true;
                    break;
            }
        }
        else
        {
            lastClimbSource = currentClimbSource;
            lastClimbPosition = currentClimbPosition;
            currentClimbSource = evaluating;
            currentClimbPosition = climbHit.point;
            climbEventArgs.climbTarget = evaluating.transform;
            climbEventArgs.LedgePoint = climbHit.point;
            MeshCollider meshc = climbHit.collider as MeshCollider;
            Mesh mesh = meshc.sharedMesh;
            int[] triangles = mesh.triangles;
            climbEventArgs.Vertex = climbHit.triangleIndex;
            climbEventArgs.connectionToRidgidbody = climbHit.rigidbody;
            climbEventArgs.climbNormal = climbHit.normal;
            climbEventArgs.FreeClimbDetected = true;
        }
        lastDirection = direction;
        LineR.SetPosition(0, transform.position);
        LineR.SetPosition(1, climbHit.point);
        if (currentClimbSource.ClimbType == filter || filter == ClimbableObject.ClimbTypes.FreeClimb) OnClimbDetected?.Invoke(this, climbEventArgs);

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
    //void TestForFreeClimb(Player_MovementManager.OnMovementEventArgs movementData, ClimbableObject evaluatedObject)
    //{
    //    //Test Start
    //    ClimbEventArgs climbEventArgs1 = new ClimbEventArgs();
    //    climbEventArgs1.climbTarget = climbHit.transform;
    //    climbEventArgs1.LedgePoint = climbHit.point;
    //    climbEventArgs1.FreeClimbDetected = true;
    //    climbEventArgs1.connectionToRidgidbody = climbHit.rigidbody;
    //    climbEventArgs1.climbNormal = climbHit.normal;

    //    currentClimbSource = evaluatedObject;
    //    OnClimbDetected?.Invoke(this, climbEventArgs1); //initate transition to climbing(freeclimb)
    //    return;
    //    //Test end
    //    foreach (ClimbableObject climbableObject in list)
    //    {
    //        if (climbableObject.ClimbType != ClimbableObject.ClimbTypes.FreeClimb) list.Remove(climbableObject);
    //    }
    //    if (list.Count > 0) evaluatedObject = list[0];
    //    Renderer renderer = evaluatedObject.GetComponent<Renderer>();
    //    Mesh mesh = evaluatedObject.GetComponent<MeshFilter>().mesh;
    //    Debug.DrawLine(transform.position, climbHit.point, Color.yellow, 0.2f);
    //    int index = climbHit.triangleIndex;
    //    int submeshCount = mesh.subMeshCount;
    //    int materialIndex = -1;
    //    for (int i = 0; i < submeshCount; i++)
    //    {
    //        var tris = mesh.GetTriangles(i);
    //        for (var j = 0; j < tris.Length; j++)
    //        {
    //            if (tris[j] == index)
    //            {
    //                materialIndex = i;
    //                break;
    //            }
    //        }
    //        if (materialIndex != -1) break;
    //    }
    //    if (materialIndex != -1)
    //    {
    //        Debug.Log($"Material at checked point = {renderer.materials[materialIndex].name} {(renderer.sharedMaterials[materialIndex] == climbingMaterial)}");
    //        if (renderer.sharedMaterials[materialIndex] == climbingMaterial)
    //        {
    //            Debug.Log("found freeclimb material");
    //            ClimbEventArgs climbEventArgs = new ClimbEventArgs();
    //            climbEventArgs.climbTarget = climbHit.transform;
    //            climbEventArgs.LedgePoint = climbHit.point;
    //            climbEventArgs.FreeClimbDetected = true;
    //            climbEventArgs.connectionToRidgidbody = climbHit.rigidbody;
    //            climbEventArgs.climbNormal = climbHit.normal;
    //            currentClimbSource = evaluatedObject;
    //            OnClimbDetected?.Invoke(this, climbEventArgs); //initate transition to climbing(freeclimb)
    //        }
    //    }
    //}
    //private ClimbableObject MostRelvantClimbChoice(Player_MovementManager.OnMovementEventArgs movementData, List<ClimbableObject> climbableObjects)
    //{
    //    if (climbableObjects.Count > 1)
    //    {
    //        if (movementData.checkFreeclimb)
    //        {
    //            foreach (ClimbableObject c in climbableObjects)
    //            {
    //                if (c.ClimbType != ClimbableObject.ClimbTypes.FreeClimb)
    //                {
    //                    climbableObjects.Remove(c);
    //                }
    //            }
    //        }
    //        else if (!movementData.checkFreeclimb)
    //        {
    //            foreach (ClimbableObject c in climbableObjects)
    //            {
    //                if (c.ClimbType != ClimbableObject.ClimbTypes.EdgeClimb)
    //                {
    //                    climbableObjects.Remove(c);
    //                }
    //            }
    //        }
    //        if (currentClimbSource != climbableObjects[0]) return climbableObjects[0];
    //        else
    //        {
    //            return climbableObjects[1];
    //        }

    //    }
    //    else
    //    {
    //        return climbableObjects[0];
    //    }
    //}

    void OnDrawGizmos()
    {
        Color climbCheckColor = currentClimbSource ? Color.white : Color.blue;
        climbCheckColor.a = .125f;
        Gizmos.color = climbCheckColor;
        Gizmos.DrawLine(transform.position, lastClimbPosition);
        Gizmos.DrawLine(transform.position, currentClimbPosition);

        Gizmos.DrawSphere(currentClimbPosition, checkRadius);
    }
}
