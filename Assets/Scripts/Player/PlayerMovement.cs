﻿using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityExtensions;


public class PlayerMovement : MonoBehaviour
{
    /// <summary>
    /// There are some states where we don't want other players to be able to push around the
    /// local player, so we make them kinematic for those states
    /// </summary>
    private HashSet<State> kinematicStates = new HashSet<State>
    {
        // TODO dkonik: It kind of seems like there are a few more states that would want this,
        // like charge ChargeShot, maybe actual Dash? But they didn't at the time, so I didn't add them.
        // However, if it makes sense, add them
        State.ChargeDash,
        State.Possession
    };

    /// <summary>
    /// States where the player is only able to rotate
    /// </summary>
    private HashSet<State> rotateOnlyStates = new HashSet<State>
    {
        State.ChargeDash,
        State.Possession,
        State.ChargeShot,
    };

    public Vector2 CurrentPosition
    {
        get { return transform.position; }
    }

    public Quaternion CurrentRotation
    {
        get { return transform.rotation; }
    }

    public float CurrentRigidBodyRotation
    {
        get { return rb2d.rotation; }
    }

    public Vector2 CurrentVelocity
    {
        get { return rb2d.velocity; }
    }

    /// <summary>
    /// Since we fucked up and put the players' forward vector
    /// to their right (I think that was me), this is just a way to get the forward vector that
    /// makes more sense. Also, if we decide to change it, this will
    /// at least make it easier to change (in one spot rather than 20).
    /// </summary>
    public Vector2 Forward
    {
        get { return transform.right; }
    }

    public float movementSpeed;
    public float rotationSpeed = 1080;
    public float maxAwayFromBallAngle = 10f;

    public bool instantRotation { get; set; } = true;

    private Rigidbody2D rb2d;
    private Coroutine playerMovementCoroutine = null;
    private PlayerStateManager stateManager;
    private float aimAssistCooldownRemaining = 0f;
    private GameObject aimAssistTarget;
    private Vector2 lastDirection = Vector2.zero;
    private Vector2 stickAngleWhenSnapped;
    private GameObject goal;
    private GameObject teammate;
    private Player player;
    private bool eventSent = false;
    private BallCarrier ballCarrier;

    [Header("Parameters")]
    [SerializeField]
    private float aimAssistThreshold = 20f;
    [SerializeField]
    private float aimAssistLerpAmount = .5f;
    [SerializeField]
    private float goalAimAssistOffset = 1f;
    [SerializeField]
    private float delayBetweenSnaps = .2f;
    [SerializeField]
    private float aimAssistEpsilon = 3.5f;
    [SerializeField]
    private float aimAssistLerpStrength = .2f;
    [SerializeField]
    private float minBallForceRotationTime = 0.1f;

    private void StartNormalMovement()
    {
        if (playerMovementCoroutine != null)
        {
            StopCoroutine(playerMovementCoroutine);
        }

        playerMovementCoroutine = StartCoroutine(Move());
    }

    private IEnumerator RotateOnly(bool snapToGameObjects)
    {
        aimAssistTarget = null;
        Vector2 startingPosition = rb2d.position;
        while (true)
        {
            if (snapToGameObjects)
            {
                RotateWithAimAssist();
            } else
            {
                RotatePlayer();
            }

            // Lock the player in place
            rb2d.position = startingPosition;
            yield return null;
        }
    }

    private void StartRotateOnly(bool snapToGameObjects)
    {
        StopAllMovementCoroutines(true);

        playerMovementCoroutine = StartCoroutine(RotateOnly(snapToGameObjects));
    }

    private void StopAllMovementCoroutines(bool zeroOutVelocity = false)
    {
        if (playerMovementCoroutine != null)
        {
            StopCoroutine(playerMovementCoroutine);
            playerMovementCoroutine = null;

            if (zeroOutVelocity)
            {
                rb2d.velocity = Vector2.zero;
            }
        }
    }

    private void RotatePlayer()
    {
        Vector2 direction = lastDirection;
        if (direction != Vector2.zero)
        {
            // Only do if nonzero, otherwise [SignedAngle] returns 90 degrees
            // and player snaps to up direction
            if (instantRotation)
            {
                rb2d.rotation = Vector2.SignedAngle(Vector2.right, direction);
            }
            else
            {
                float maxAngleChange = Vector2.SignedAngle(Forward, direction);
                float sign = Mathf.Sign(maxAngleChange);
                float speedChange = rotationSpeed * Time.deltaTime;
                float actualChange = sign * Mathf.Min(Mathf.Abs(maxAngleChange), speedChange);
                float finalRotation = rb2d.rotation + actualChange;
                if (finalRotation <= 0)
                {
                    finalRotation = 360 - Mathf.Repeat(-finalRotation, 360);
                }
                finalRotation = Mathf.Repeat(finalRotation, 360);
                if (GameManager.PossessionManager.PossessingPlayer == player &&
                    (Time.time - ballCarrier.timeCarryStarted) >= minBallForceRotationTime)
                {
                    Ball ball = ballCarrier.Ball;
                    Vector3 ballDirection = (ball.transform.position - transform.position).normalized;
                    Vector3 unitFinal = Quaternion.AngleAxis(finalRotation, Vector3.forward) * Vector2.right;
                    float angleDifference = Vector2.SignedAngle(ballDirection, unitFinal);
                    if (Mathf.Abs(angleDifference) >= maxAwayFromBallAngle)
                    {
                        finalRotation =
                            Vector2.SignedAngle(Vector2.right, ballDirection)
                            + Mathf.Sign(angleDifference) * maxAwayFromBallAngle;
                    }
                }
                if (finalRotation <= 0)
                {
                    finalRotation = 360 - Mathf.Repeat(-finalRotation, 360);
                }
                finalRotation = Mathf.Repeat(finalRotation, 360);
                rb2d.rotation = finalRotation;
            }
        }
    }

    private void AimAssistTowardsTarget()
    {
        Vector3 vector = (aimAssistTarget.transform.position - transform.position).normalized;
        rb2d.rotation = Vector2.SignedAngle(Vector2.right, Vector2.Lerp(Forward, vector, aimAssistLerpStrength));
    }

    /// <summary>
    /// Tries to snap to the goal or teammate if it can, otherwise just rotates
    /// </summary>
    private void RotateWithAimAssist()
    {
        // If we are cooling down still, dont snap
        if (aimAssistCooldownRemaining > 0f)
        {
            RotatePlayer();
            return;
        }

        if (aimAssistTarget != null)
        {
            Vector3 vector = (aimAssistTarget.transform.position - transform.position).normalized;
            if (lastDirection == Vector2.zero ||
                Mathf.Abs(Vector2.Angle(vector, lastDirection)) < aimAssistThreshold ||
                Mathf.Abs(Vector2.Angle(stickAngleWhenSnapped, lastDirection)) < aimAssistEpsilon)
            {
                AimAssistTowardsTarget();
            }
            else
            {
                aimAssistCooldownRemaining = delayBetweenSnaps;
                aimAssistTarget = null;
                RotatePlayer();
            }
        }
        else
        {
            if (lastDirection == Vector2.zero)
            {
                RotatePlayer();
                return;
            }

            Vector2? goalVector = null;
            Vector2? teammateVector = null;
            if (goal != null)
            {
                goalVector = ((goal.transform.position + Vector3.up) - transform.position).normalized;
            }
            if (teammate != null)
            {
                teammateVector = (teammate.transform.position - transform.position).normalized;
            }

            if (goalVector.HasValue &&
                    Mathf.Abs(Vector2.Angle(Forward, goalVector.Value)) < aimAssistThreshold &&
                    GameManager.PossessionManager.CurrentTeam == player.Team &&
                    GameManager.PossessionManager.IsCharged)
            {
                aimAssistTarget = goal;
                stickAngleWhenSnapped = lastDirection;
                AimAssistTowardsTarget();
            }
            else if (teammateVector.HasValue &&
                         Mathf.Abs(Vector2.Angle(Forward, teammateVector.Value)) < aimAssistThreshold)
            {
                aimAssistTarget = teammate;
                stickAngleWhenSnapped = lastDirection;
                AimAssistTowardsTarget();
            }
            else
            {
                RotatePlayer();
            }
        }
    }

    private IEnumerator Move()
    {
        float startTime = Time.time;
        yield return new WaitForFixedUpdate();

        while (true)
        {
            if (eventSent) {
                  rb2d.velocity = movementSpeed * lastDirection;
                  eventSent = false;
              }

            // TODO dkonik: Remove this code. This is for the tutorial.
            if (lastDirection.magnitude > 0.1f)
            {
                if (Time.time - startTime > 0.75f)
                {
                    GameManager.NotificationManager.NotifyStringEvent("MoveTutorial", this.gameObject);
                }
            }
            else
            {
                startTime = Time.time;
            }

            RotatePlayer();
            yield return null;
        }
    }

    // Use this for initialization
    private void Start()
    {
        rb2d = this.EnsureComponent<Rigidbody2D>();
        stateManager = this.EnsureComponent<PlayerStateManager>();
        player = this.EnsureComponent<Player>();

        // TODO dkonik: Remove this dependency on ball carrier
        ballCarrier = this.EnsureComponent<BallCarrier>();
        GameManager.NotificationManager.CallOnMessageWithSender(
            Message.PlayerStick, playerPair =>
            {
                Tuple<Vector2, GameObject> pair = playerPair as Tuple<Vector2, GameObject>;
                GameObject player = pair?.Item2;
                if (pair != null && this != null && player == this.gameObject)
                {
                    eventSent = true;
                    lastDirection = pair.Item1;
                }
            });
        stateManager.OnStateChange += HandleNewPlayerState;

        goal = GameObject.FindObjectOfType<GoalAimPoint>()?.gameObject;
        this.FrameDelayCall(() =>
        {
            TeamManager team = player.Team;

            if (team == null)
            {
                return;
            }
            foreach (Player teammate in team.teamMembers)
            {
                if (teammate != player)
                {
                    this.teammate = teammate.gameObject;
                }
            }
        }, 2);

        // Subscribe to events
        GameManager.NotificationManager.CallOnMessage(Message.SlowMoEntered, HandleSlowMo);
        GameManager.NotificationManager.CallOnMessage(Message.SlowMoExited, HandleSlowMo);
    }

    private void HandleSlowMo()
    {
        instantRotation = !GameManager.SlowMoManager.IsSlowMo;
    }

    private void DoStunMovement()
    {
        StunInformation info = stateManager.CurrentStateInformation_Exn<StunInformation>();

        StopAllMovementCoroutines(false);

        // If we for some reason transition to the stun state *after*
        // we were supposed to have finished with the stun state, just put
        // the player where they would have ended up
        float timeTravelledSoFar = (float)(PhotonNetwork.Time - info.EventTimeStamp);
        if (timeTravelledSoFar > info.Duration)
        {
            timeTravelledSoFar = info.Duration;
        }

        // Calculate the start position based on the time that has elapsed since
        // the message was sent
        rb2d.position = info.StartPosition + timeTravelledSoFar * info.Velocity;
        rb2d.velocity = info.Velocity;
    }

    private void DoLayTronWall()
    {
        // TODO dkonik: Previously, we were setting this every frame while the player was
        // laying the tron wall, do we still need to do this.
        TronWallInformation info = stateManager.CurrentStateInformation_Exn<TronWallInformation>();

        StopAllMovementCoroutines(false);

        float timeTravelledSoFar = (float)(PhotonNetwork.Time - info.EventTimeStamp);
        Vector2 layingVelocity = PlayerTronMechanic.layingSpeedMovementSpeedRatio * movementSpeed * info.Direction;
        rb2d.position = info.StartPosition + timeTravelledSoFar * layingVelocity;
        rb2d.velocity = layingVelocity;
    }

    private void DoDash()
    {
        StopAllMovementCoroutines(true);
        // TODO dkonik: We are not using start position. But maybe we want to
        DashInformation info = stateManager.CurrentStateInformation_Exn<DashInformation>();
        rb2d.velocity = info.Velocity;
    }

    private void HandleNewPlayerState(State oldState, State newState)
    {
        // Handle enabling/disabling kinematic on the player
        if (kinematicStates.Contains(newState))
        {
            // For some states, we don't want other players being able to push the player around
            // so we make them kinematic
            rb2d.isKinematic = true;
        } else if (kinematicStates.Contains(oldState))
        {
            // If we were kinematic last state, make not kinematic
            rb2d.isKinematic = false;
        }

        // Handle starting proper coroutines
        if (newState == State.NormalMovement)
        {
            StartNormalMovement();
        } else if (rotateOnlyStates.Contains(newState))
        {
            // Only snap on possession states
            StartRotateOnly(newState == State.Possession || newState == State.ChargeShot);
        } else if (newState == State.FrozenAfterGoal)
        {
            StopAllMovementCoroutines(true);
        } else if (newState == State.Stun)
        {
            DoStunMovement();
        } else if (newState == State.LayTronWall)
        {
            DoLayTronWall();
        } else if (newState == State.Dash)
        {
            DoDash();
        } else if (newState == State.StartOfMatch)
        {
            StopAllMovementCoroutines(true);
            rb2d.position = player.initialPosition;
            rb2d.rotation = player.initialRotation;
            StartRotateOnly(false);
        }
    }
}
