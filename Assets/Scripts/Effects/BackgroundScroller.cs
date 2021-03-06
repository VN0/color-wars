﻿using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundScroller : MonoBehaviour
{
    public float rotationRate = 0.0f;
    public float loopTime = 0.0f;
    public float scrollMagnitude = 50.0f;

    private new SpriteRenderer renderer;
    private Vector3 origin;

    private void Start()
    {
        renderer = GetComponent<SpriteRenderer>();
        origin = transform.position;
        GameManager.NotificationManager.CallOnMessage(Message.ScoreChanged, HandleScoreChanged);
    }

    private void HandleScoreChanged()
    {
        TeamManager winningTeam = GameManager.Instance.GetWinningTeam();
        if (winningTeam != null)
        {
            SetBackground(winningTeam.resources);
        }
        else
        {
            SetBackground(GameManager.Instance.neutralResources);
        }
    }

    private void Update()
    {
        float x = scrollMagnitude * Mathf.Sin(2 * Mathf.PI * Time.time / loopTime);
        float y = scrollMagnitude * Mathf.Cos(2 * Mathf.PI * Time.time / loopTime);

        transform.position = origin + new Vector3(x, y, 0);
        transform.Rotate(new Vector3(0, 0, rotationRate * Time.deltaTime));
    }

    private void SetBackground(TeamResourceManager resource)
    {
        if (renderer != null)
        {
            renderer.sprite = resource.background;
        }
    }
}
