﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityExtensions;

using IC = InControl;

public class BallCarrier : MonoBehaviour {

    public float coolDownTime = .1f;
    public Ball ball { private set; get;}
    public float ballTurnSpeed = 10f;
    public bool chargedBallStuns = false;
    public bool slowMoOnCarry = true;
    public float aimAssistThreshold = 20f;
    public float aimAssistLerpAmount = .5f;
    public float goalAimAssistOffset = 1f;
    public float delayBetweenSnaps = .2f;
    public float snapEpsilon = 5f;
    public float snapLerpStrength = .5f;

    float ballOffsetFromCenter = .5f;
    IPlayerMovement playerMovement;
    PlayerStateManager stateManager;
    Coroutine carryBallCoroutine;
    bool isCoolingDown = false;
    LaserGuide laserGuide;
    GameObject teammate;
    Player player;
    GameObject goal;
    Rigidbody2D rb2d;
    GameObject snapToObject;
    float snapDelay = 0f;
    Vector2 stickAngleWhenSnapped;

    const float ballOffsetMultiplier = 1.07f;

    public bool IsCarryingBall() {
        return ball != null;
    }

    void Start() {
        snapToObject = null;
        player = GetComponent<Player>();
        playerMovement = GetComponent<IPlayerMovement>();
        stateManager = GetComponent<PlayerStateManager>();
        rb2d = GetComponent<Rigidbody2D>();
        if (playerMovement != null && stateManager != null) {
            stateManager.CallOnStateEnter(State.Posession, playerMovement.FreezePlayer);
            stateManager.CallOnStateExit(State.Posession, playerMovement.UnFreezePlayer);
        }
        laserGuide = this.GetComponent<LaserGuide>();
        this.FrameDelayCall(() => {GetGoal(); GetTeammate();}, 2);
    }

    // This function is called when the BallCarrier initially gains possession
    // of the ball
    public void StartCarryingBall(Ball ball) {
        CalculateOffset(ball);
        if (slowMoOnCarry) {
            GameModel.instance.SlowMo();
        }
        ball.charged = false;
        Utility.TutEvent("BallPickup", this);
        laserGuide?.DrawLaser();
        var player = GetComponent<Player>();
        var lastPlayer = ball.lastOwner?.GetComponent<Player>();
        if (player != null && lastPlayer != null) {
            if (player.team == lastPlayer.team && player != lastPlayer) {
                Utility.TutEvent("PassSwitch", player);
                Utility.TutEvent("PassSwitch", lastPlayer);
            }
        }
        carryBallCoroutine = StartCoroutine(CarryBall(ball));
    }

    void CalculateOffset(Ball ball) {
        var ballRadius = ball.GetComponent<CircleCollider2D>()?.bounds.extents.x;
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null && ballRadius != null) {
            var spriteExtents = renderer.sprite.bounds.extents.x * transform.localScale.x;
            ballOffsetFromCenter = ballOffsetMultiplier * (spriteExtents + ballRadius.Value);
        }
    }

    void GetTeammate() {
        var team = player.team;
        if (team == null) {
            return;
        }
        foreach (var teammate in team.teamMembers) {
            if (teammate != player) {
                this.teammate = teammate.gameObject;
            }
        }
    }

    void GetGoal() {
        goal = GameObject.FindObjectOfType<Goal>().gameObject;
    }

    void SnapToGameObject() {
        var vector = (snapToObject.transform.position - transform.position).normalized;
        rb2d.rotation = Vector2.SignedAngle(Vector2.right, Vector2.Lerp(transform.right, vector, snapLerpStrength));
    }

    void SnapAimTowardsTargets() {
        if (teammate == null || playerMovement == null || ((PlayerMovement)playerMovement).GetInputDevice() == null) {
            return;
        }
        if (snapDelay > 0f) {
            playerMovement?.RotatePlayer();
            return;
        }
        PlayerMovement pm = (PlayerMovement)playerMovement;
        var stickDirection = new Vector2(pm.GetInputDevice().LeftStickX, pm.GetInputDevice().LeftStickY);
        if (snapToObject != null) {
            var vector = (snapToObject.transform.position - transform.position).normalized;
            if (stickDirection == Vector2.zero ||
                Mathf.Abs(Vector2.Angle(vector, stickDirection)) < aimAssistThreshold ||
                Mathf.Abs(Vector2.Angle(stickAngleWhenSnapped, stickDirection)) < snapEpsilon) {
                SnapToGameObject();
            } else {
                snapDelay = delayBetweenSnaps;
                snapToObject = null;
                playerMovement?.RotatePlayer();
            }
        } else {
            var goalVector = ((goal.transform.position + Vector3.up) - transform.position).normalized;
            var teammateVector = (teammate.transform.position - transform.position).normalized;
            if (Mathf.Abs(Vector2.Angle(transform.right, goalVector)) < aimAssistThreshold) {
                snapToObject = goal;
                stickAngleWhenSnapped = stickDirection;
                SnapToGameObject();
            } else if (Mathf.Abs(Vector2.Angle(transform.right, teammateVector)) < aimAssistThreshold) {
                snapToObject = teammate;
                stickAngleWhenSnapped = stickDirection;
                SnapToGameObject();
            } else {
                playerMovement?.RotatePlayer();
            }
        }
    }

    IEnumerator CarryBall(Ball ball) {
        GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        this.ball = ball;
        ball.owner = this;

        while (true) {
            SnapAimTowardsTargets();
            PlaceBallAtNose();
            snapDelay -= Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    IEnumerator CoolDownTimer() {
        isCoolingDown = true;
        yield return new WaitForSeconds(coolDownTime);
        isCoolingDown = false;
    }

    public void DropBall() {
        if (ball != null) {
            GameModel.instance.ResetSlowMo();
            StopCoroutine(carryBallCoroutine);
            carryBallCoroutine = null;

            // Reset references
            ball.owner = null;
            ball = null;
            laserGuide?.StopDrawingLaser();
            StartCoroutine(CoolDownTimer());
        }
    }

    Vector2 NosePosition(Ball ball) {
        var newPosition = transform.position + transform.right * ballOffsetFromCenter;
        return newPosition;
    }

    void PlaceBallAtNose() {
        if (ball != null) {
            var rigidbody = ball.GetComponent<Rigidbody2D>();
            Vector2 newPosition =
                CircularLerp(ball.transform.position, NosePosition(ball), transform.position,
                             ballOffsetFromCenter, Time.deltaTime, ballTurnSpeed);
            rigidbody.MovePosition(newPosition);
        }
    }

    Vector2 CircularLerp(Vector2 start, Vector2 end, Vector2 center, float radius,
                         float timeDelta, float speed) {
        float angularDistance = timeDelta * speed;
        var centeredStart = start - center;
        var centerToStartDirection = centeredStart.normalized;

        var centeredEndDirection = (end - center).normalized;
        var angle = Vector2.SignedAngle(centerToStartDirection, centeredEndDirection);
        var arcDistance = radius * 2 * Mathf.PI * Mathf.Abs(angle / 360);
        var percentArc = Mathf.Clamp(angularDistance / arcDistance, 0, 1);

        var rotation = Quaternion.AngleAxis(angle * percentArc, Vector3.forward);
        var centeredResult = rotation * centerToStartDirection;
        centeredResult *= radius;
        return (Vector2) centeredResult + center;
    }

    void HandleCollision(GameObject thing) {
        var ball = thing.GetComponent<Ball>();
        if (ball == null || !ball.IsOwnable() || isCoolingDown) {
            return;
        }
        if (stateManager != null) {
            var last_team = ball.lastOwner?.GetComponent<Player>().team;
            var this_team = GetComponent<Player>().team;
            if (chargedBallStuns && ball.charged && last_team != this_team) {
                var stun = GetComponent<PlayerStun>();
                var direction = transform.position - ball.transform.position;
                var knockback = ball.GetComponent<Rigidbody2D>().velocity.magnitude * direction;
                stateManager.AttemptStun(() => stun.StartStun(knockback), stun.StopStunned);
            } else {
                stateManager.AttemptPossession(() => StartCarryingBall(ball), DropBall);
            }
        } else {
            StartCoroutine(CoroutineUtility.RunThenCallback(CarryBall(ball), DropBall));
        }
    }

    public void OnCollisionEnter2D(Collision2D collision) {
        HandleCollision(collision.gameObject);
    }

    public void OnCollisionStay2D(Collision2D collision) {
        HandleCollision(collision.gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (stateManager != null && stateManager.IsInState(State.Dash)) {
            HandleCollision(other.gameObject);
        }
    }
}
