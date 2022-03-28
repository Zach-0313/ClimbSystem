using System;
using System.Collections;
using UnityEngine;

public class Player_MovementManager : MonoBehaviour
{
    //Events
    public event EventHandler<OnMovementEventArgs> OnGroundMovement;
    public event EventHandler<OnMovementEventArgs> OnFreeClimb;
    public event EventHandler<OnMovementEventArgs> OnEdgeClimb;

    public event EventHandler<OnMovementEventArgs> OnValidateClimb;
    public class OnMovementEventArgs : EventArgs
    {
        public Rigidbody playerBody;
        public ClimbableObject currentClimbObject;
        public Vector3 velocity, rightAxis, forwardAxis, contactNormal, connectionVelocity, ledgePoint;
        public Transform InputSpace, TargetLedge;
        public Vector2 movementInput, unnormalized_movementInput;
        public float gravityScale;
        public ClimbableObject.ClimbTypes searchFor;
        public bool lookForNew;
        public int v;
    }

    [SerializeField]
    Transform playerInputSpace = default;
    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f;
    [SerializeField, Range(0, 90)]
    float maxGroundAngle = 25f;
    [SerializeField, Range(90, 170)]
    float maxClimbAngle = 140f;
    [SerializeField, Range(0f, 100f)]
    float maxSnapSpeed = 100f;
    [SerializeField]
    float groundCheckDistance;
    [SerializeField]
    Material canClimbMaterial;
    public Rigidbody body, connectedBody, previousConnectedBody;
    Vector2 playerInput, unnormalized_playerInput;
    public Vector3 velocity, connectionVelocity;
    public Vector3 connectionWorldPosition, connectionLocalPosition;
    Vector3 upAxis, rightAxis, forwardAxis;
    public Vector3 contactNormal, climbNormal, lastClimbNormal;
    [SerializeField] int groundContactCount, climbContactCount;
    bool OnGround => groundContactCount > 0;
    bool OnSurface => isGrounded || isClimbing;
    public bool isClimbing, isGrounded, isFreeClimbing, isLedgeClimbing, CanTransition, FoundLedge;
    bool Climbing => climbContactCount > 0 && stepsSinceLastJump > 2;
    [SerializeField] float minGroundDotProduct, minClimbDotProduct;
    public float gravityScale;
    [SerializeField] int stepsSinceLastGrounded, stepsSinceLastJump;
    [SerializeField] bool wantsJump, wantsClimbing, wantsJumpOffWall;
    [SerializeField] InputHandler InputHandler;
    [SerializeField] ClimbDetector climbDetector;
    RaycastHit hit;
    public ClimbableObject CurrentClimbingSource;


    [SerializeField] StaminaSystem staminaSystem;

    [SerializeField] Player_GroundedMovement playerGroundedMovement;
    void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        minClimbDotProduct = Mathf.Cos(maxClimbAngle * Mathf.Deg2Rad);
    }
    void OnEnable()
    {
        climbDetector.OnClimbDetected += OnRecieveClimbCheckData;
        InputHandler.OnPlayerInput += Process_PlayerInputData;
        OnValidate(); //ensure that minimums are set properly
    }
    void Start()
    {
        UpdateState();
        OnGroundMovement(this, OnMovementEventData());
    }
    void FixedUpdate()
    {
        UpdateState(); //updates variables and checks for ground
        Vector3 gravity = Vector3.down * gravityScale;
        if (wantsClimbing)
        {
            wantsClimbing = staminaSystem.Stamina > 0;
        }
        if (wantsClimbing && !isClimbing)
        {
            OnValidateClimb?.Invoke(this, OnMovementEventData(ClimbableObject.ClimbTypes.FreeClimb));
        }
        if (isClimbing)
        {
            staminaSystem.DrainStamina(1);
            if (wantsJumpOffWall)
            {
                Jump(gravity);
            }
            if (wantsClimbing)  //if holding the climb button [R1]
            {
                if (isLedgeClimbing && wantsJump)  //On a ledge + pressed the Jump button
                {
                    OnValidateClimb?.Invoke(this, OnMovementEventData(ClimbableObject.ClimbTypes.FreeClimb));
                }
                else if (isLedgeClimbing)  //if on a ledge, there's no need to check for anything
                {
                    OnEdgeClimb?.Invoke(this, OnMovementEventData());  //no need to validate
                }
                if (isFreeClimbing && !wantsJump && FoundLedge && CanTransition)  //if a ledge is detected, and the player is ready to transition
                {
                    OnValidateClimb?.Invoke(this, OnMovementEventData(ClimbableObject.ClimbTypes.EdgeClimb));
                    StartCoroutine(SwitchedClimbState());  //CanTransition is set to false for 1 second, then set back to true
                }
                else if (isFreeClimbing)
                {
                    //OnValidateClimb?.Invoke(this, OnMovementEventData(ClimbableObject.ClimbTypes.FreeClimb));
                    OnFreeClimb.Invoke(this, OnMovementEventData(ClimbableObject.ClimbTypes.FreeClimb));
                }

            }
            else
            {
                Jump(gravity);
            }
        }
        else if (isGrounded)
        {
            if (wantsJump)
            {
                Jump(gravity);
            }
            OnGroundMovement?.Invoke(this, OnMovementEventData());
            climbDetector.currentClimbSource = null;
            isFreeClimbing = isLedgeClimbing = false;
        }
        else
        {
            velocity += Vector3.down * gravityScale * Time.deltaTime;//if not climbing or grounded, then the player is falling
        }
        
        body.velocity = velocity;
    }
    void Jump(Vector3 gravity)
    {
        staminaSystem.DrainStamina(10f);
        Vector3 jumpDirection;
        if (isGrounded)
        {
            jumpDirection = contactNormal;
            OnValidateClimb?.Invoke(this, OnMovementEventData(ClimbableObject.ClimbTypes.FreeClimb)); //check for jumping onto a ledge
        }
        else if (isClimbing)
        {
            isClimbing = isFreeClimbing = isLedgeClimbing = false;
            jumpDirection = contactNormal;
            transform.forward = -transform.forward;
        }
        else
        {
            return;
        }
        float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);
        jumpDirection = (jumpDirection + upAxis).normalized;
        float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
        if (alignedSpeed > 0f)
        {
            jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
        }
        velocity += jumpDirection * jumpSpeed;
    }
    Vector3 lastPoint;
    private void OnRecieveClimbCheckData(object sender, ClimbDetector.ClimbEventArgs climbEventArgs)
    {
        lastPoint = climbEventArgs.LedgePoint;
        CurrentClimbingSource = climbEventArgs.source;
        connectedBody = climbEventArgs.connectionToRidgidbody;
        contactNormal = climbEventArgs.climbNormal;
        if (climbEventArgs.LedgeClimbDetected && !isLedgeClimbing)
        {
            isClimbing = isLedgeClimbing = true;
            isFreeClimbing = false;
            OnMovementEventArgs eventArgs = OnMovementEventData();
            eventArgs.TargetLedge = climbEventArgs.climbTarget;
            eventArgs.ledgePoint = climbEventArgs.LedgePoint;
            OnEdgeClimb?.Invoke(this, eventArgs);
        }
        else if (climbEventArgs.FreeClimbDetected)
        {
            OnMovementEventArgs eventArgs = OnMovementEventData();
            eventArgs.TargetLedge = climbEventArgs.climbTarget;
            eventArgs.ledgePoint = climbEventArgs.LedgePoint;
            eventArgs.v = climbEventArgs.Vertex;
            isClimbing = isFreeClimbing = true;
            isLedgeClimbing = false;
            OnFreeClimb?.Invoke(this, eventArgs);
        }
        else if (isClimbing && !(climbEventArgs.FreeClimbDetected || climbEventArgs.LedgeClimbDetected))
        {
            Debug.Log("Climb not triggering");
            isClimbing = isFreeClimbing = isLedgeClimbing = false;
        }
    }
    OnMovementEventArgs OnMovementEventData()
    {
        Vector3 gravity = Vector3.down * gravityScale;
        upAxis = Vector3.up;
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

        OnMovementEventArgs data = new OnMovementEventArgs();
        data.connectionVelocity = connectionVelocity;
        data.contactNormal = contactNormal;
        data.playerBody = body;
        data.movementInput = playerInput;
        data.unnormalized_movementInput = unnormalized_playerInput;
        data.gravityScale = gravityScale;
        data.InputSpace = playerInputSpace;
        data.rightAxis = rightAxis;
        data.forwardAxis = forwardAxis;
        data.currentClimbObject = CurrentClimbingSource;
        data.velocity = velocity;
        return data;
    }
    OnMovementEventArgs OnMovementEventData(ClimbableObject.ClimbTypes filterFor)
    {
        Vector3 gravity = Vector3.down * gravityScale;
        upAxis = Vector3.up;
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

        OnMovementEventArgs data = new OnMovementEventArgs();
        data.connectionVelocity = connectionVelocity;
        data.contactNormal = contactNormal;
        data.playerBody = body;
        data.movementInput = playerInput;
        data.unnormalized_movementInput = unnormalized_playerInput;
        data.gravityScale = gravityScale;
        data.InputSpace = playerInputSpace;
        data.rightAxis = rightAxis;
        data.forwardAxis = forwardAxis;
        data.currentClimbObject = CurrentClimbingSource;
        data.velocity = velocity;
        data.searchFor = filterFor;
        return data;
    }
    private void Process_PlayerInputData(object sender, InputHandler.OnPlayerInputEventArgs e)
    {
        playerInput = e.playerMovement;
        unnormalized_playerInput = e.unnormalized_playerMovement;
        wantsJumpOffWall = e.wantJump2;
        wantsJump = e.wantJump;
        wantsClimbing = e.wantClimb;
    }

    void OnDisable()
    {
        InputHandler.OnPlayerInput -= Process_PlayerInputData;
        climbDetector.OnClimbDetected -= OnRecieveClimbCheckData;

    }
    void OnCollisionEnter(Collision collision)
    {
        //EvaluateCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        //EvaluateCollision(collision);
    }

    void EvaluateCollision(Collision collision)
    {
        float minDot = minGroundDotProduct;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            float upDot = Vector3.Dot(upAxis, normal);
            if (isClimbing)
            {
                groundContactCount += 1;
                contactNormal = normal;
                connectedBody = collision.rigidbody;
            }
            else
            {
                if (wantsClimbing && upDot >= minClimbDotProduct)
                {
                    climbContactCount += 1;
                    climbNormal = normal;
                    lastClimbNormal = normal;
                    connectedBody = collision.rigidbody;
                }
            }
        }
    }

    bool SnapToGround()
    {
        Debug.Log("Snapping to ground...");
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
    bool CheckClimbing()
    {
        if (isClimbing)
        {

            climbNormal.Normalize();
            float upDot = Vector3.Dot(upAxis, climbNormal);
            if (upDot >= minGroundDotProduct)
            {
                climbNormal = lastClimbNormal;
            }

            groundContactCount = 1;
            contactNormal = climbNormal;
            return true;
        }
        return false;
    }
    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
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
        //isGrounded = OnGround;
        isGrounded = Physics.SphereCast(body.position, .25f, -upAxis, out RaycastHit hit, groundCheckDistance);
        Debug.DrawLine(transform.position, hit.point, Color.cyan, .1f);


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
    void ClearState()
    {
        groundContactCount = climbContactCount = 0;
        contactNormal = climbNormal = Vector3.zero;
        connectionVelocity = Vector3.zero;
        previousConnectedBody = connectedBody;
        connectedBody = null;
    }
    Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal)
    {
        return (direction - normal * Vector3.Dot(direction, normal)).normalized;
    }
    IEnumerator SwitchedClimbState()
    {
        if(CanTransition) CanTransition = false;
        yield return new WaitForSeconds(1f);
        CanTransition = true;
    }
}
