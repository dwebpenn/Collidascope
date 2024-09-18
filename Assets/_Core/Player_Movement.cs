using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using TMPro;


public class Player_Movement : MonoBehaviour
{
    [SerializeField] private Rigidbody RB;
    //[SerializeField] private Health playerHealth;

    public float forwardSpeed = 25f;
    //public float baseLineSpeed = 3f;
   //private float activeForwardSpeed;
    //private float forwardAcceleration = 2.5f;

    private float currentThrust = 1f;
    public float thrustMax = 2f;
    public float thrustAccel = 0.5f;

    public float pitchTilt = 20f;
    public float yawTilt = 20f;


    public float rotationPower = 5.0f;
    [SerializeField] private CinemachineVirtualCamera forwardCamera;
    [SerializeField] private CinemachineVirtualCamera backwardCamera;

    public Camera mainCam;

    public GameObject playerShip;
    private Vector3 playerShipInitRotOffset;

    public float pitchChangeSpeed = 4f;
    private float initPitchSpeed;
    public float pitchSpeedMultiplier = 0.8f;   //limits pitch mobility when going fast

    public float yawSpeed = 40f;
    private float initYawSpeed;
    public float yawSpeedMultiplier = 1.2f;

    //private PlayerInputActions playerInputActions;
    public Vector2 moveInput;
    public Vector2 camInput;

    public float engineSpeed = 1f;
    public AudioSource airplaneSFX;

    private float thrustIterator;
    private bool isThrusting = false;

    public float tiltRotationSpeed = 2.0f;
    private bool isTiltingLeft = false;
    private bool isTiltingRight = false;
    float targetZRotation;


    [SerializeField] private Transform shootingSpawnLocation;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletForce;

    [SerializeField] private AudioSource lightBulletSFX;

    [SerializeField] private ParticleSystem exhaustVFX;
    [SerializeField] private ParticleSystem speedVFX;
    public float vfxMultiplier = 2.0f;

    private bool isEngineStalling = false;

    public void InitializePlayerInput()
    {
        //playerInputActions = new PlayerInputActions();
        //playerInputActions.Player.Enable();
        //playerInputActions.Player.Thrust.started += IncreaseThrust;
        //playerInputActions.Player.Thrust.canceled += DecreaseThrust;

        //playerInputActions.Player.RightTilt.started += EnableRightTilt;
        //playerInputActions.Player.RightTilt.canceled += DisableRightTilt;
        //playerInputActions.Player.LeftTilt.started += EnableLeftTilt;
        //playerInputActions.Player.LeftTilt.canceled += DisableLeftTilt;


        //playerInputActions.Player.LightAttack.started += LightAttack;



        //playerInputActions.Player.BackCameraToggle.started += EnableBackCamera;
        //playerInputActions.Player.BackCameraToggle.canceled += DisableBackCamera;


        //playerInputActions.Player.EngineKill.started += EngineKill;
        //playerInputActions.Player.EngineKill.canceled += EngineStart;

        forwardCamera.enabled = true;
    }

    void InitializePlayerShip()
    {
        playerShipInitRotOffset = playerShip.gameObject.transform.localRotation.eulerAngles;     //used for player ship rotation, to make sure movement rotation is based off initial transform rotation
        airplaneSFX.Play();
        airplaneSFX.loop = true;

        initYawSpeed = yawSpeed;
        initPitchSpeed = pitchChangeSpeed;
    }

    // Start is called before the first frame update
    void Start()
    {

            InitializePlayerInput();
            InitializePlayerShip();
        
        


    }
    private void Update()
    {

        //moveInput = playerInputActions.Player.Move.ReadValue<Vector2>();
        airplaneSFX.pitch = engineSpeed;
        UpdateShipState();
        UpdateVFX();

        //UpdateCamera();
        UpdateDebug();
    }

    private void UpdateShipState()
    {
        if (isTiltingLeft) //move current rotation towards fully tilted left
            targetZRotation = Mathf.MoveTowards(targetZRotation, playerShipInitRotOffset.z + 90f, tiltRotationSpeed);
        else if (isTiltingRight) //move current rotation towards fully tilted right
            targetZRotation = Mathf.MoveTowards(targetZRotation, playerShipInitRotOffset.z - 90f, tiltRotationSpeed);
        else //lerp back to center
            targetZRotation = Mathf.MoveTowards(targetZRotation, playerShipInitRotOffset.z, tiltRotationSpeed);

        playerShip.transform.localRotation = Quaternion.Euler(transform.localRotation.x + moveInput.y * pitchTilt + playerShipInitRotOffset.x,
            transform.localRotation.y + moveInput.x * yawTilt + playerShipInitRotOffset.y,
            targetZRotation);
    }

    // Update is called once per frame
    void FixedUpdate()
    {

            UpdateMovement();
    }

    private void UpdateMovement()
    {
        if (isThrusting)
        {
            if (thrustIterator <= 1f) //accelerate up to max thrust
                thrustIterator += Time.fixedDeltaTime * thrustAccel;
            currentThrust = Mathf.Lerp(currentThrust, thrustMax, thrustIterator);
        }
        else
        {
            if (thrustIterator >= 0f) //deccelerate from thrust
                thrustIterator -= Time.fixedDeltaTime * thrustAccel;
            else
                thrustIterator = 0f;
            currentThrust = Mathf.Lerp(currentThrust, 1, Time.fixedDeltaTime * thrustAccel * 3f);
        }

        Vector3 forward = transform.forward * forwardSpeed * Time.fixedDeltaTime * currentThrust;
        //Vector3 forward = transform.forward * forwardSpeed * Time.fixedDeltaTime;

        //if (isEngineStalling)
        //    forward = Vector3.zero;

        transform.eulerAngles = new Vector3(transform.eulerAngles.x + moveInput.y * pitchChangeSpeed, transform.eulerAngles.y + moveInput.x * yawSpeed, transform.eulerAngles.z);
        //Vector3 newRot = new Vector3(transform.eulerAngles.x + moveInput.y * pitchChangeSpeed * Time.fixedDeltaTime, transform.eulerAngles.y + moveInput.x * yawSpeed * Time.fixedDeltaTime, transform.eulerAngles.z);
        
        //RB.rotation = Quaternion.Euler(newRot);

        //TODO: Lerp position to move input to create a speed-of-rotation effect that can be informed by speed of the ship

        //engineSpeed = forward.magnitude ;    //used for dynamic audio
        engineSpeed = Mathf.InverseLerp(0f, 50f, forward.magnitude);
        //engineSpeed = RB.velocity.magnitude;    //used for dynamic audio
        //Vector3 newMoveVector = forward;// + right;
        //RB.MovePosition(transform.position + forward);

        //RB.AddForce(forward, ForceMode.Impulse);
        RB.velocity = forward;

        //RB.position = transform.position + forward;
        //RB.position = transform.position + newMoveVector;

        //Debug.Log("current thrust: " + currentThrust.ToString());
    }

    private void UpdateCamera()
    {
        //camInput = playerInputActions.Player.Camera.ReadValue<Vector2>();



        //followTarget.rotation *= Quaternion.AngleAxis(camInput.x * rotationPower, Vector3.up);
        //followTarget.rotation *= Quaternion.AngleAxis(camInput.y * rotationPower, Vector3.right);





        ////recenter
        //if (Mathf.Abs(camInput.x) < 0.1 && Mathf.Abs(camInput.y) < 0.1)
        //{
        //    followTarget.transform.DORotate(Vector3.zero, 0.1f);
        //}
        //else
        //{
        //    ////clamp
        //    //var angles = followTarget.transform.localEulerAngles;
        //    //angles.z = 0;
        //    //var angle = followTarget.transform.localEulerAngles.x;

        //    //if (angle > 180 && angle < 340)
        //    //{
        //    //    angles.x = 340;
        //    //}
        //    //else if (angle < 180 && angle > 40)
        //    //{
        //    //    angles.x = 40;
        //    //}
        //    //followTarget.transform.localEulerAngles = angles;
        //}


    }

    private void UpdateVFX()
    {
        //Make this much better
        speedVFX.playbackSpeed = engineSpeed * vfxMultiplier;
        exhaustVFX.playbackSpeed = engineSpeed * vfxMultiplier;
    }


    void LightShoot()
    {
       // GameObject bullet = GameObject.Instantiate(bulletPrefab, shootingSpawnLocation.position, shootingSpawnLocation.rotation);
        //bullet.GetComponent<Rigidbody>().AddForce(shootingSpawnLocation.forward * bulletForce, ForceMode.Impulse);
        //bullet.GetComponent<Bullet>().Shoot();

        lightBulletSFX.Play();
    }

    
    public void ResetShip()
    {
        playerShip.transform.rotation = Quaternion.Euler(playerShipInitRotOffset);
        moveInput = Vector2.zero;
        currentThrust = 0;
    }


    //MOVEMENT CONTROLS
    public void IncreaseThrust(InputAction.CallbackContext context)
    {
        isThrusting = true;
        pitchChangeSpeed *= pitchSpeedMultiplier;
    }

    public void DecreaseThrust(InputAction.CallbackContext context)
    {
        isThrusting = false;
        pitchChangeSpeed = initPitchSpeed;
    }

    private void EnableLeftTilt(InputAction.CallbackContext context)
    {
        isTiltingLeft = true; //increase yaw speed in direction of tilt
        yawSpeed *= yawSpeedMultiplier;
    }
    private void DisableLeftTilt(InputAction.CallbackContext context)
    {
        isTiltingLeft = false;
        yawSpeed = initYawSpeed;
    }

    private void EnableRightTilt(InputAction.CallbackContext context)
    {
        isTiltingRight = true;
        yawSpeed *= yawSpeedMultiplier;
    }
    private void DisableRightTilt(InputAction.CallbackContext context)
    {
        isTiltingRight = false;
        yawSpeed = initYawSpeed;
    }

    //ATTACK CONTROLS
    public void LightAttack(InputAction.CallbackContext context)
    {
        LightShoot();
    }

    private void EnableBackCamera(InputAction.CallbackContext context)
    {
        backwardCamera.enabled = true;
        forwardCamera.enabled = false;
    }

    private void DisableBackCamera(InputAction.CallbackContext context)
    {
        backwardCamera.enabled = false;
        forwardCamera.enabled = true;
    }

    private void EngineKill(InputAction.CallbackContext context)
    {
        isEngineStalling = true;
        RB.useGravity = true;
    }

    private void EngineStart(InputAction.CallbackContext context)
    {
        isEngineStalling = false;
        RB.useGravity = false;
    }


    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI isThrustingText;
    [SerializeField] private TextMeshProUGUI thrustText;

    void UpdateDebug()
    {
        speedText.text = engineSpeed.ToString();
        isThrustingText.text = isThrusting.ToString();
        thrustText.text = currentThrust.ToString();
    }
}
