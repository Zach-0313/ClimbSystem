using System;
using System.Collections;
using UnityEngine;

public class Player_MovementManager : MonoBehaviour
{
    //Events
    public event EventHandler<OnMovementEventArgs> OnGroundMovement;  // called when the player needs to walk around : Recieved in the Player_GroundMovement script
    public event EventHandler<OnMovementEventArgs> OnFreeClimb;       // called when the player is free climbing : Recieved in the Player_ClimbSystem script
    public event EventHandler<OnMovementEventArgs> OnEdgeClimb;       // called when the player is climbing on a ledge : Recieved in the Player_ClimbSystem script
    public event EventHandler<OnMovementEventArgs> OnValidateClimb;   // called when the climb system checks for a valid climbable surface : Recieved in the ClimbDetector script
    public class OnMovementEventArgs : EventArgs  // EventArgs allow data to be sent/recieved when events are called, this EventArg passes player inputs and situational data to the various movement events
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
    Transform playerInputSpace = default;  // the player will move relitive to this transform's orientation(set this to the camera)
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
    [SerializeField] bool wantsJump, wantsClimbing, wantsJumpOffWall;  //these bools correspond to input information : processed in the Process_PlayerInputData method
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
        //subscribe to events
        climbDetector.OnClimbDetected += OnRecieveClimbCheckData;
        InputHandler.OnPlayerInput += Process_PlayerInputData;  //subscribing to this event will provide input data : calls the Process_PlayerInputData method
        
        OnValidate(); //ensure that minimums are set properly
    }
    void Start()
    {
        UpdateState();
        
        OnGroundMovement(this, OnMovementEventData()); //calling this event will snap the player to the ground when the scene runs
    }
    void FixedUpdate()
    {
        UpdateState(); //updates variables and checks for ground
        Vector3 gravity = Vector3.down * gravityScale;
        if (wantsClimbing)  //if holding the climb button [R1]
        {
            wantsClimbing = staminaSystem.Stamina > 0;
        }
        if (wantsClimbing && !isClimbing)  // if the player is not already on a climbable surface, then check for a climbable surface
        {
            OnValidateClimb?.Invoke(this, OnMovementEventData(ClimbableObject.ClimbTypes.FreeClimb));
        }
        if (isClimbing)  //called every frame while the player is climbing
        {
            staminaSystem.DrainStamina(1); // drain stamina, modifier of 1x
            if (wantsJumpOffWall)
            {
                Jump(gravity);
            }
            if (wantsClimbing)  //if holding the climb button [R1]
            {
                if (isLedgeClimbing && wantsJump)  //On a ledge + pressed the Jump button. tries to transition (Ledge Climb -> Freeclimb)
                {
                    OnValidateClimb?.Invoke(this, OnMovementEventData(ClimbableObject.ClimbTypes.FreeClimb));
                }
                else if (isLedgeClimbing)  //if on a ledge(idle/no other action), there's no need to check for anything
                {
                    OnEdgeClimb?.Invoke(this, OnMovementEventData());  //no need to validate
                }
                if (isFreeClimbing && !wantsJump && FoundLedge && CanTransition)  //if currently Freeclimbing + a ledge is detected + no jump(holding jump will make the player ignore ledges), then try to transition (Freeclimb -> Ledge Climb)
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
            OnGroundMovement?.Invoke(this, OnMovementEventData());  //if grounded, then invoke the OnGroundMovement event + send data from OnMovementData()
            climbDetector.currentClimbSource = null;                //player is on the ground, so clear climb sources
            isFreeClimbing = isLedgeClimbing = false;               //player is on the ground, disable climbing
        }
        else
        {
            velocity += Vector3.down * gravityScale * Time.deltaTime; //if not climbing or grounded, then the player is falling
        }
        
        body.velocity = velocity;  //apply velocity changes made by movement methods
    }
    void Jump(Vector3 gravity)
    {
        staminaSystem.DrainStamina(10f);
        Vector3 jumpDirection;
        if (isGrounded)
        {
            jumpDirection = contactNormal; //jump away from the "ground" surface
            OnValidateClimb?.Invoke(this, OnMovementEventData(ClimbableObject.ClimbTypes.FreeClimb)); //check for Freeclimb surface
        }
        else if (isClimbing)  //Jumping off a wall
        {
            isClimbing = isFreeClimbing = isLedgeClimbing = false;  //disable climbing
            jumpDirection = contactNormal;                          //jump away from wall
            transform.forward = -transform.forward;                 //face away from wall
        }
        else
        {
            return;
        }
        float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);  //this is the formula for jump height
        jumpDirection = (jumpDirection + upAxis).normalized;                //adjust the jump direction to apear more natural
        float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
        if (alignedSpeed > 0f)
        {
            jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
        }
        velocity += jumpDirection * jumpSpeed;
    }
    Vector3 lastPoint;
    /*
     * when checking for a climbable surface, the OnValidateClimb event is called.
     * The climb detectoin system is expensive to calculate, so it is used sparringly.
     * To avoid spikes in frametimes, calling the climb detection is done via event.
     * The climb detector returns its data by calling an event(this is for preformance reasons).
     */
    private void OnRecieveClimbCheckData(object sender, ClimbDetector.ClimbEventArgs climbEventArgs)  //this method is called once the ClimbDetector has finished it's calculations (called via event)
    {
        lastPoint = climbEventArgs.LedgePoint;
        CurrentClimbingSource = climbEventArgs.source;
        connectedBody = climbEventArgs.connectionToRidgidbody;
        contactNormal = climbEventArgs.climbNormal;

        //the following if/else chain reacts to detector results based off the current climb state
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
    OnMovementEventArgs OnMovementEventData() //this method simply compiles relevant player data, because this method returns the OnMovementEventArgs it lets data be sent between systems during event calls
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
    OnMovementEventArgs OnMovementEventData(ClimbableObject.ClimbTypes filterFor) //Same as the above method, but this allows a climbtype(to filter for) to be sent through the OnValidateClimb event.
    {
        OnMovementEventArgs data = OnMovementEventData();
        data.searchFor = filterFor;
        return data;
    }
    private void Process_PlayerInputData(object sender, InputHandler.OnPlayerInputEventArgs e)  //when player input occurs, the InputHandler class calls an event that triggers this method
    {
        //this essentially allows the script to snapshot input data(sets bools), this way it doesn't need to be recollected every time input data is needed
        playerInput = e.playerMovement;
        unnormalized_playerInput = e.unnormalized_playerMovement;
        wantsJumpOffWall = e.wantJump2;
        wantsJump = e.wantJump;
        wantsClimbing = e.wantClimb;
    }

    void OnDisable()
    {
        //events need to be unsubscribed OnDisable, this prevents ram leaks / garbage collector issues
        InputHandler.OnPlayerInput -= Process_PlayerInputData;
        climbDetector.OnClimbDetected -= OnRecieveClimbCheckData;

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
    IEnumerator SwitchedClimbState()  //this courrotine acts as a cooldown between switching climb modes, this prevents the player from getting stuck in feedback loops
    {
        if(CanTransition) CanTransition = false;
        yield return new WaitForSeconds(1f);
        CanTransition = true;
    }
}
