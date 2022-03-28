using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
public class InputHandler : MonoBehaviour
{
    Vector2 playerMovementInput;
    bool singleJump, doubleJump;
    bool grabClimb;
    bool debugPanelExists;
    [SerializeField] float doublePressTime = .35f;
    float lastClick;

    public event EventHandler<OnPlayerInputEventArgs> OnPlayerInput;


    public class OnPlayerInputEventArgs : EventArgs
    {
        public Vector2 playerMovement, unnormalized_playerMovement;
        public bool wantClimb, wantGrab;
        public bool wantJump;
        public bool wantJump2; //when jump is double pressed
    }
    void LateUpdate()
    {
        OnPlayerInputEventArgs playerInputData = new OnPlayerInputEventArgs();
        playerInputData.playerMovement = playerMovementInput.normalized;
        playerInputData.unnormalized_playerMovement = playerMovementInput;

        playerInputData.wantClimb = playerInputData.wantGrab = grabClimb;
        playerInputData.wantJump = singleJump;
        playerInputData.wantJump2 = doubleJump;

        OnPlayerInput?.Invoke(this, playerInputData);
        //singleJump = doubleJump = false;

    }
    public void OnMovement(InputValue context)
    {
        playerMovementInput = context.Get<Vector2>();
    }
    public void OnJump(InputValue context)
    {
        checkForJumps(context.isPressed);
    }
    public void OnReloadScene(InputValue context)
    {
        if (context.isPressed) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    public void OnCloseGame(InputValue context)
    {
        if (context.isPressed) Application.Quit();
    }
    void checkForJumps(bool type)
    {
        float timeSinceClick = Time.time - lastClick;
        if (timeSinceClick <= doublePressTime)
        {
            doubleJump = type;
            //singleJump = false;
            Debug.Log("double Jumped ");

        }

            singleJump = type;
            Debug.Log("single Jumped ");

        
        lastClick = Time.time;
    }
    public void OnCrouchGrab(InputValue context)
    {
        grabClimb = context.isPressed;
    }
}
