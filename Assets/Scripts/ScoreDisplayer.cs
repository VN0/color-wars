﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UtilityExtensions;

public class ScoreDisplayer : MonoBehaviour {

    List<Text> teams;
    Text matchTimeText;
    void Start() {
        teams = new List<Text>() {
            transform.FindComponent<Text>("Team1Text"),
            transform.FindComponent<Text>("Team2Text")
        };
        matchTimeText = transform.FindComponent<Text>("MatchTimeText");
        StartCoroutine(InitScores());
    }

    IEnumerator InitScores() {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        UpdateScores();
    }

    public void StartMatchLengthUpdate(float matchLength) {
        StartCoroutine(UpdateMatchTime(matchLength));
    }

    IEnumerator UpdateMatchTime(float matchLength) {
        yield return new WaitForFixedUpdate();
        float endTime = Time.time + matchLength;
        var end = DateTime.Now.AddSeconds(matchLength);
        while (Time.time < endTime) {
            var now = DateTime.Now;
            var difference = end - now;
            var time_string = difference.ToString(@"mm\:ss");
            matchTimeText.text = string.Format("Time: {0}", time_string);
            yield return new WaitForFixedUpdate();
        }
    }

    public void UpdateScores() {
        for (int i = 0; i < teams.Count && i < GameModel.instance.teams.Length; i++) {
            var text = teams[i];
            var team = GameModel.instance.teams[i];
            text.text = string.Format("Team {0}: {1}", team.teamNumber, team.score);
            text.color = team.teamColor;
        }
    }
}
