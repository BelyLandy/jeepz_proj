using UnityEngine;

public interface IHitscanDamageReceiver25D
{
    void ReceiveHitscanDamage(int damage, Vector3 hitPoint, Vector3 hitDirection, GameObject instigator);
}
