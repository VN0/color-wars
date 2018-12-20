﻿using Photon.Pun;
using UnityEngine;

/// <summary>
/// Base class that all player states inherit from that provides an "interface" way to
/// serialize and deserialize
/// </summary>
public abstract class PlayerStateInformation
{
    public abstract void Deserialize(PhotonStream stream, PhotonMessageInfo info);
    public abstract void Serialize(PhotonStream stream, PhotonMessageInfo info);
}

public class DashInformation : PlayerStateInformation
{
    public Vector2 StartPosition { get; set; }
    public Vector2 Direction { get; set; }
    public float Strength { get; set; }

    public override void Deserialize(PhotonStream stream, PhotonMessageInfo info)
    {
        StartPosition = (Vector3)stream.ReceiveNext();
        Direction = (Vector3)stream.ReceiveNext();
        Strength = (float)stream.ReceiveNext();
    }

    public override void Serialize(PhotonStream stream, PhotonMessageInfo info)
    {
        stream.SendNext(StartPosition);
        stream.SendNext(Direction);
        stream.SendNext(Strength);
    }
}

public class StealBallInformation : PlayerStateInformation
{
    /// <summary>
    /// Network player id of the victim
    /// </summary>
    public int Victim { get; set; }

    public override void Deserialize(PhotonStream stream, PhotonMessageInfo info)
    {

        Victim = (int)stream.ReceiveNext();
    }

    public override void Serialize(PhotonStream stream, PhotonMessageInfo info)
    {
        stream.SendNext(Victim);
    }
}

public class ShootBallInformation : PlayerStateInformation
{
    public Vector2 BallStartPosition { get; set; }
    public Vector2 Direction { get; set; }
    public float Strength { get; set; }

    public override void Deserialize(PhotonStream stream, PhotonMessageInfo info)
    {
        BallStartPosition = (Vector3)stream.ReceiveNext();
        Direction = (Vector3)stream.ReceiveNext();
        Strength = (float)stream.ReceiveNext();
    }

    public override void Serialize(PhotonStream stream, PhotonMessageInfo info)
    {
        stream.SendNext(BallStartPosition);
        stream.SendNext(Direction);
        stream.SendNext(Strength);
    }
}

public class StunInformation : PlayerStateInformation
{
    public Vector2 StartPosition { get; set; }
    public Vector2 Direction { get; set; }
    public float Strength { get; set; }

    public override void Deserialize(PhotonStream stream, PhotonMessageInfo info)
    {
        StartPosition = (Vector3)stream.ReceiveNext();
        Direction = (Vector3)stream.ReceiveNext();
        Strength = (float)stream.ReceiveNext();
    }

    public override void Serialize(PhotonStream stream, PhotonMessageInfo info)
    {
        stream.SendNext(StartPosition);
        stream.SendNext(Direction);
        stream.SendNext(Strength);
    }
}