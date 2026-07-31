using UnityEngine;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem;
#endif
using StarterAssets;
using System.Collections;
using GTA3Unity.Vehicles;
using System;
using Unity.VisualScripting;

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace GTA3Unity.Core
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM 
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class PlayerController : PedObject
    {
        public static PlayerController Instance { get; private set; }

        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioSource AudioFootsteps;
        public AudioSource LandingAudio;
        public AudioSource AudioFoley;
        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;
        private string _currentAnimation;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM 
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private Vehicle m_Vehicle;

        private const float _threshold = 0.01f;
        private const float AnimationFadeLength = 0.15f;
        private const string IdleAnimation = "idle_stance";
        private const string WalkAnimation = "walk_player";
        private const string RunAnimation = "run_civi";
        private const string SprintAnimation = "sprint_civi";
        private const string FallAnimation = "FALL_fall";

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
            }
        }

        public void PutInCar(Vehicle vehicle)
        {
            m_Vehicle = vehicle;
            SetPedState(EPedState.Driving);
            PlayAnimation("CAR_sit");
            TeleportPlayer(vehicle.transform.position);
            vehicle.SetDriver(this);
        }

        public void ExitCar()
        {
            SetPedState(EPedState.OnFoot);
            m_Vehicle.ClearDriver();
            TeleportPlayer(m_Vehicle.transform.position + m_Vehicle.transform.up * 2); // TP up for now, in future we want to TP to car door
            m_Vehicle = null;
        }

        public void TeleportPlayer(Vector3 position, Quaternion? rotation = null)
        {
            _controller.enabled = false;
            transform.position = position;
            if(rotation != null)
            {
                transform.rotation = (Quaternion)rotation;
            }
            _controller.enabled = true;
        }


        private void Awake()
        {
            if(Instance == null)
            {
                Instance = this;
            }
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private IEnumerator Start()
        {
            while(FileLoader.Instance == null || !FileLoader.Instance.IsDone)
            {
                yield return null;
            }

            SetModel(1);
            Debug.Assert(m_PedModel != null);
            FileLoader.Instance.PlayPedAnimation(m_PedModel);
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
            Debug.Assert(_controller != null);
            Debug.Assert(_input != null);
#if ENABLE_INPUT_SYSTEM 
            _playerInput = GetComponent<PlayerInput>();
            Debug.Assert(_playerInput != null);
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif


            AssignAnimationIDs();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            TogglePlayerControllable(false);
        }

        public void TogglePlayerControllable(bool isEnabled)
        {
            _controller.enabled = isEnabled;
        }

        private void Update()
        {
            if(!GameManager.Instance.InGame)
            {
                return;
            }

            switch(m_PedState)
            {
                case EPedState.OnFoot:
                    FindVehicles();
                    JumpAndGravity();
                    GroundedCheck();
                    Move();
                    break;
                case EPedState.Driving:
                    if(m_Vehicle == null)
                    {
                        return;
                    }
                    // TODO: Need to replace these legacy Input calls with new InputSystem instead
                    if(Input.GetKeyUp(KeyCode.F))
                    {
                        ExitCar();
                        return;
                    }
                    if(Input.GetKeyUp(KeyCode.B))
                    {
                        m_Vehicle.BlowUp();
                        return;
                    }
                    m_Vehicle.OnInput(_input);
                    break;
                case EPedState.Passenger:
                    break;
            }

        }

        private void FindVehicles()
        {
            if(Input.GetKeyUp(KeyCode.Alpha0))
            {
                // Temp debug spawn random vehicle
                EVehicleClass vehicleClass = (EVehicleClass)UnityEngine.Random.Range(0, (int)EVehicleClass.TotalVehicleClasses);
                VehicleSpawning.SpawnRandomVehicle(vehicleClass, transform.position);
            }
            if(Input.GetKeyUp(KeyCode.F))
            {
                Vehicle[] vehs = GameObject.FindObjectsByType<Vehicle>();
                if(vehs.Length < 1)
                {
                    return;
                }
                float nearestDistance = 20f;
                Vehicle nearestVehicle = null;
                foreach(var vehicle in vehs)
                {
                    float distance = Vector3.Distance(transform.position, vehicle.transform.position);
                    if(distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestVehicle = vehicle;
                    }
                }
                if(nearestVehicle != null)
                {
                    PutInCar(nearestVehicle);
                }
            }
        }

        private void LateUpdate()
        {
            if(!GameManager.Instance.InGame)
            {
                return;
            }

            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);
        }

        private void CameraRotation()
        {
            // if there is an input and camera position is not fixed
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachine will follow this target
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw, 0.0f);
        }

        private void Move()
        {
            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // normalise input direction
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            bool hasMoveInput = _input.move != Vector2.zero;

            // if there is a move input rotate player when the player is moving
            if (hasMoveInput)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                // rotate to face input direction relative to camera position
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }


            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            UpdateMovementAnimation(hasMoveInput, targetSpeed);
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    PlayPlayerAnimation(FallAnimation);
                }

                // if we are not grounded, do not jump
                _input.jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private void UpdateMovementAnimation(bool hasMoveInput, float targetSpeed)
        {
            if (!Grounded)
            {
                PlayPlayerAnimation(FallAnimation);
                return;
            }

            if (!hasMoveInput || targetSpeed <= 0.0f)
            {
                PlayPlayerAnimation(IdleAnimation);
                return;
            }

            if (_input.sprint)
            {
                PlayPlayerAnimation(SprintAnimation);
                return;
            }

            PlayPlayerAnimation(targetSpeed <= MoveSpeed ? WalkAnimation : RunAnimation);
        }

        private void PlayPlayerAnimation(string animationName)
        {
            if (_currentAnimation == animationName)
            {
                return;
            }

            if (base.PlayAnimation(
                animationName,
                AnimationFadeLength,
                WrapMode.Loop,
                IsLocomotionAnimation(animationName)))
            {
                _currentAnimation = animationName;
            }
        }

        private static bool IsLocomotionAnimation(string animationName)
        {
            return animationName == WalkAnimation ||
                animationName == RunAnimation ||
                animationName == SprintAnimation;
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {

                if (AudioFootsteps != null)
                    AudioFootsteps.Play();
                if (AudioFoley != null)
                    AudioFoley.Play();
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (LandingAudio != null)
                    LandingAudio.Play();

            }
        }
    }
}
