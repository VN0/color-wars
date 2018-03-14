﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UtilityExtensions;
using IC = InControl;

public class PlayerTutorial : MonoBehaviour {
    public IC.InputControlType readyUpButton = IC.InputControlType.Start;
    IC.InputDevice input;
    PlayerMovement playerMovement;
    bool registered;

    // Use this for initialization
    void Start () {
        playerMovement = this.EnsureComponent<PlayerMovement>();
        input = playerMovement.GetInputDevice();
        registered = false;
    }
	
    // Update is called once per frame
    void Update () {
        if (input == null) {
            input = playerMovement.GetInputDevice();
            return;
        }
        else if (!registered) {
            Debug.Log("Registering!");
            ReadyUpManager.instance.RegisterPlayer(this);
            registered = true;
        }
        if (input.GetControl(readyUpButton).WasPressed) {
            ReadyUpManager.instance.RegisterReadyPlayer(this);
        }
    }
}