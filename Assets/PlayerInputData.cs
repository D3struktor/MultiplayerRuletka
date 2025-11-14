using Fusion;
using UnityEngine;

public struct PlayerInputData : INetworkInput {
  public Vector2 Move;
  public NetworkBool JumpPressed;
  public NetworkBool JumpHeld;
  public NetworkBool AttackPressed;
}
