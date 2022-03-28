using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    Transform playerInputSpace = default;
    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10f, maxClimbSpeed = 4f;
    [SerializeField, Range(0f, 100f)]
    float maxAcceleration = 10f, maxAirAcceleration = 1f, maxClimbAcceleration = 40f;
    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f;
    [SerializeField, Range(0, 90)]
    float maxGroundAngle = 25f;
    [SerializeField, Range(90, 170)]
    float maxClimbAngle = 140f;
    [SerializeField, Range(0f, 100f)]
    float maxSnapSpeed = 100f;
    [SerializeField]
    Material normalMaterial = default, climbingMaterial = default;
    [SerializeField]
    float groundCheckDistance;
    [SerializeField]
    Material canClimbMaterial;
    public Rigidbody body, connectedBody, previousConnectedBody;
    public Vector2 playerInput;
    public Vector3 velocity, connectionVelocity;
    Vector3 connectionWorldPosition, connectionLocalPosition;
    Vector3 upAxis, rightAxis, forwardAxis;
    public bool desiredJump, desiresClimbing;
    Vector3 contactNormal, climbNormal, lastClimbNormal;
    public int groundContactCount, climbContactCount;
    public bool OnGround => groundContactCount > 0;
    public bool isGrounded, isClimbing;
    [SerializeField]
    public bool Climbing => climbContactCount > 0 && stepsSinceLastJump > 2;
    public float minGroundDotProduct, minClimbDotProduct, gravityScale;
    public int stepsSinceLastGrounded, stepsSinceLastJump;
    public MeshRenderer meshRenderer;

    void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        minClimbDotProduct = Mathf.Cos(maxClimbAngle * Mathf.Deg2Rad);
    }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        OnValidate();
    }

    void Update()
    {
        upAxis = Vector3.up;
        isGrounded = OnGround;
        isClimbing = Climbing;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);

        if (playerInputSpace)
        {
            rightAxis = ProjectDirectionOnPlane(playerInputSpace.right, upAxis);
            forwardAxis = ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);
        }
        else
        {
            rightAxis = ProjectDirectionOnPlane(Vector3.right, upAxis);
            forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);
        }

        desiredJump |= Input.GetKeyDown(KeyCode.Space);
        desiresClimbing = Input.GetKey(KeyCode.C);

        meshRenderer.material = Climbing ? climbingMaterial : normalMaterial;
    }

    void FixedUpdate()
    {
        Vector3 gravity = Vector3.down * gravityScale;
        UpdateState();
        AdjustVelocity();

        if (desiredJump)
        {
            desiredJump = false;
            Jump(gravity);
        }
        if (Climbing)
        {
            velocity -= contactNormal * ((maxClimbAcceleration + gravityScale) * Time.deltaTime);//pushes player into the wall
        }
        else if (OnGround && velocity.sqrMagnitude < 0.01f)//if stationary
        {
            velocity += contactNormal * (Vector3.Dot(gravity, contactNormal));//when stationary the player will be held in place with the force of gravity
        }
        else if (desiresClimbing && OnGround)
        {
            velocity += (gravity - contactNormal * (maxClimbAcceleration)) * Time.deltaTime;
        }
        else
        {
            velocity += gravity * Time.deltaTime;//falling
        }
        body.velocity = velocity;
        HandleRotation();
        ClearState();
    }

    void ClearState()
    {
        groundContactCount = climbContactCount = 0;
        contactNormal = climbNormal = Vector3.zero;
        connectionVelocity = Vector3.zero;
        previousConnectedBody = connectedBody;
        connectedBody = null;
    }

    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.velocity;
        if (CheckClimbing() || OnGround || SnapToGround())
        {
            stepsSinceLastGrounded = 0;
            if (groundContactCount > 1)
            {
                contactNormal.Normalize();
            }
        }
        else
        {
            contactNormal = upAxis;
        }

        if (connectedBody)
        {
            if (connectedBody.isKinematic || connectedBody.mass >= body.mass)
            {
                UpdateConnectionState();
            }
        }
    }

    void UpdateConnectionState()
    {
        if (connectedBody == previousConnectedBody)
        {
            Vector3 connectionMovement = connectedBody.transform.TransformPoint(connectionLocalPosition) - connectionWorldPosition;
            connectionVelocity = connectionMovement / Time.deltaTime;
        }
        connectionWorldPosition = body.position;
        connectionLocalPosition = connectedBody.transform.InverseTransformPoint(connectionWorldPosition);
    }

    bool CheckClimbing()
    {
        if (Climbing)
        {
            if (climbContactCount > 1)
            {
                climbNormal.Normalize();
                float upDot = Vector3.Dot(upAxis, climbNormal);
                if (upDot >= minGroundDotProduct)
                {
                    climbNormal = lastClimbNormal;
                }
            }
            groundContactCount = 1;
            contactNormal = climbNormal;
            return true;
        }
        return false;
    }

    bool SnapToGround()
    {
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2)
        {
            return false;
        }
        float speed = velocity.magnitude;
        if (speed > maxSnapSpeed)
        {
            return false;
        }
        if (!Physics.Raycast(body.position, -upAxis, out RaycastHit hit, groundCheckDistance))//if something is NOT detected below the player
        {
            return false;
        }

        float upDot = Vector3.Dot(upAxis, hit.normal);
        if (upDot < minGroundDotProduct)//if "ground" is within the steepness threshold(max ground angle)
        {
            return false;
        }
        groundContactCount = 1;
        contactNormal = hit.normal;
        float dot = Vector3.Dot(velocity, hit.normal);
        if (dot > 0f)
        {
            velocity = (velocity - hit.normal * dot).normalized * speed;//presses player into surface
        }
        connectedBody = hit.rigidbody;
        return true;
    }
    void AdjustVelocity()
    {
        float acceleration, speed;
        Vector3 xAxis, zAxis;
        if (Climbing)
        {
            acceleration = maxClimbAcceleration;
            speed = maxClimbSpeed;
            xAxis = Vector3.Cross(contactNormal, upAxis);
            zAxis = upAxis;
        }
        else
        {
            acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
            speed = OnGround && desiresClimbing ? maxClimbSpeed : maxSpeed;
            xAxis = rightAxis;
            zAxis = forwardAxis;
        }
        xAxis = ProjectDirectionOnPlane(xAxis, contactNormal);
        zAxis = ProjectDirectionOnPlane(zAxis, contactNormal);

        Vector3 relativeVelocity = velocity - connectionVelocity;
        float currentX = Vector3.Dot(relativeVelocity, xAxis);
        float currentZ = Vector3.Dot(relativeVelocity, zAxis);

        float maxSpeedChange = acceleration*acceleration * Time.fixedDeltaTime;

        float newX = Mathf.MoveTowards(currentX, playerInput.x * speed, maxSpeedChange);
        float newZ = Mathf.MoveTowards(currentZ, playerInput.y * speed, maxSpeedChange);

        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }
    void HandleRotation()
    {
        Vector3 velo = velocity;
        velo.y = 0;
        if (Climbing)
        {
            transform.LookAt(transform.position - contactNormal.normalized * 3, Vector3.up);
        }
        else
        {
            transform.LookAt(transform.position + velo.normalized * 3, Vector3.up);
        }
    }
    void JumpAlongWall()
    {

    }

    void Jump(Vector3 gravity)
    {
        Vector3 jumpDirection;
        if (OnGround)
        {
            jumpDirection = contactNormal;
        }
        else if (Climbing)
        {
            jumpDirection = contactNormal;
        }
        else
        {
            return;
        }

        stepsSinceLastJump = 0;
        float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);
        jumpDirection = (jumpDirection + upAxis).normalized;
        float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
        if (alignedSpeed > 0f)
        {
            jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
        }
        velocity += jumpDirection * jumpSpeed;
    }

    void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    void EvaluateCollision(Collision collision)
    {
        float minDot = minGroundDotProduct;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            float upDot = Vector3.Dot(upAxis, normal);
            if (upDot >= minDot)
            {
                groundContactCount += 1;
                contactNormal += normal;
                connectedBody = collision.rigidbody;
            }
            else
            {
                if (desiresClimbing && upDot >= minClimbDotProduct)
                {
                    climbContactCount += 1;
                    climbNormal += normal;
                    lastClimbNormal = normal;
                    connectedBody = collision.rigidbody;
                }
            }
        }
    }

    Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal)
    {
        return (direction - normal * Vector3.Dot(direction, normal)).normalized;
    }
}
