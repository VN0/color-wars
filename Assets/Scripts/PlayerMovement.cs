﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using InControl;


public class PlayerMovement : MonoBehaviour {

    public float movementSpeed;

    Rigidbody2D rb2d;
    InputDevice inputDevice;
    Coroutine playerMovementCoroutine;
    PlayerInputManager playerInput;

    public void StartPlayerMovement()
    {
        playerMovementCoroutine = StartCoroutine(Move());
    }

    public void ParalyzePlayer(float timePeriod)
    {
        StartCoroutine(PausePlayerMovement(timePeriod));
    }

    IEnumerator PausePlayerMovement(float timePeriod)
    {
        StopAllMovement();
        yield return new WaitForSeconds(timePeriod);
        StartPlayerMovement();
    }

    void StopAllMovement()
    {
        StopCoroutine(playerMovementCoroutine);
    }

    // Handles players movement on the game board
    IEnumerator Move ()
    {
        yield return new WaitForFixedUpdate();
        while (true) {
            var direction = new Vector2(inputDevice.LeftStickX, inputDevice.LeftStickY);
            rb2d.velocity = movementSpeed * direction;

            // Only do if nonzero, otherwise [SignedAngle] returns 90 degrees
            // and player snaps to up direction
            if (direction != Vector2.zero) {
                rb2d.rotation = Vector2.SignedAngle(Vector2.right, direction);
            }
            yield return new WaitForFixedUpdate();
        }
    }

    // Use this for initialization
    void Start () {
        rb2d = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInputManager>();

        TryToGetInputDevice();
    }

    void TryToGetInputDevice()
    {
        // TODO dkonik: Set this to whatever the input manager is
        inputDevice = playerInput.GetInputDevice(InputDeviceDisconnectedCallback);
        if (inputDevice != null) {
            StartPlayerMovement();
        }
    }

    void InputDeviceDisconnectedCallback()
    {
        Debug.LogFormat(this, "{0}: Input Device Disconnected", name);
        inputDevice = null;
    }

    void Update()
    {
        if (inputDevice == null) {
            TryToGetInputDevice();
        }
    }
}
