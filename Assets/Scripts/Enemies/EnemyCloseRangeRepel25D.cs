using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyCloseRangeRepel25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyCharacter25D character;
    [SerializeField] private EnemyPerception25D perception;
    [SerializeField] private Collider repelTrigger;
    [SerializeField] private Transform repelOrigin;

    [Header("Timing / Rules")]
    [SerializeField, Min(0f)] private float repelTriggerRange = 1.5f;
    [SerializeField, Min(0f)] private float repelWindupDuration = 0.15f;
    [SerializeField, Min(0f)] private float repelActiveDuration = 0.12f;
    [SerializeField, Min(0f)] private float repelCooldown = 1.25f;
    [SerializeField] private bool requireFacingTargetForRepel = true;
    [SerializeField] private bool requireLineOfSightForRepel = false;

    [Header("Force")]
    [SerializeField, Min(0f)] private float repelHorizontalForce = 10f;
    [SerializeField, Min(0f)] private float repelVerticalLift = 1.5f;

    private bool isPreparingRepel;
    private bool isRepelActive;
    private float prepareRepelEndTime;
    private float repelActiveEndTime;
    private float nextRepelReadyTime;
    private int lastRepelEventVersion;
    private readonly HashSet<GameObject> hitTargetsThisActivation = new HashSet<GameObject>();

    public bool IsPreparingRepel => isPreparingRepel;
    public bool IsRepelActive => isRepelActive;
    public bool IsInRepelFlow => isPreparingRepel || isRepelActive;
    public bool CanStartRepel => Time.time >= nextRepelReadyTime && !isPreparingRepel && !isRepelActive;
    public float RepelCooldownRemaining => Mathf.Max(0f, nextRepelReadyTime - Time.time);
    public int LastRepelEventVersion => lastRepelEventVersion;
    public float RepelTriggerRange => repelTriggerRange;

    private void Reset()
    {
        AutoAssign();
        ClampSettings();
        DisableTrigger();
    }

    private void Awake()
    {
        AutoAssign();
        ClampSettings();
        DisableTrigger();
    }

    private void OnValidate()
    {
        AutoAssign();
        ClampSettings();
        if (!Application.isPlaying)
            DisableTrigger();
    }

    private void OnDisable()
    {
        CancelRepel();
    }

    public bool CanBeginRepel(float distanceToTarget, bool targetVisible, bool hasLineOfSight, int facingSignToTarget)
    {
        if (!targetVisible || perception == null)
            return false;

        if (!CanStartRepel)
            return false;

        if (distanceToTarget > repelTriggerRange)
            return false;

        if (requireLineOfSightForRepel && !hasLineOfSight)
            return false;

        if (requireFacingTargetForRepel && character != null)
        {
            int desiredSign = facingSignToTarget >= 0 ? 1 : -1;
            if (character.FacingSign != desiredSign)
                return false;
        }

        return true;
    }

    public bool TryBeginRepel(float distanceToTarget, bool targetVisible, bool hasLineOfSight, int facingSignToTarget)
    {
        if (!CanBeginRepel(distanceToTarget, targetVisible, hasLineOfSight, facingSignToTarget))
            return false;

        isPreparingRepel = true;
        prepareRepelEndTime = Time.time + repelWindupDuration;
        return true;
    }

    public void TickRepel()
    {
        if (isPreparingRepel && Time.time >= prepareRepelEndTime)
        {
            isPreparingRepel = false;
            isRepelActive = true;
            repelActiveEndTime = Time.time + repelActiveDuration;
            hitTargetsThisActivation.Clear();
            EnableTrigger();
            lastRepelEventVersion++;
        }

        if (isRepelActive && Time.time >= repelActiveEndTime)
        {
            isRepelActive = false;
            DisableTrigger();
            nextRepelReadyTime = Time.time + repelCooldown;
            hitTargetsThisActivation.Clear();
        }
    }

    public void CancelRepel()
    {
        isPreparingRepel = false;
        isRepelActive = false;
        prepareRepelEndTime = 0f;
        repelActiveEndTime = 0f;
        hitTargetsThisActivation.Clear();
        DisableTrigger();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyRepel(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyRepel(other);
    }

    private void TryApplyRepel(Collider other)
    {
        if (!isRepelActive || other == null)
            return;

        HeroHurtbox25D heroHurtbox = other.GetComponent<HeroHurtbox25D>();
        if (heroHurtbox == null)
            heroHurtbox = other.GetComponentInParent<HeroHurtbox25D>();
        if (heroHurtbox == null)
            return;

        GameObject targetObject = heroHurtbox.gameObject;
        if (hitTargetsThisActivation.Contains(targetObject))
            return;

        Vector3 origin = repelOrigin != null ? repelOrigin.position : transform.position;
        Vector3 direction = targetObject.transform.position - origin;
        direction.y = 0f;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            int sign = character != null ? character.FacingSign : 1;
            direction = sign >= 0 ? Vector3.right : Vector3.left;
        }

        direction.Normalize();
        Vector3 velocity = new Vector3(direction.x * repelHorizontalForce, repelVerticalLift, 0f);
        if (TryApplyKnockback(heroHurtbox, other, direction, velocity))
            hitTargetsThisActivation.Add(targetObject);
    }

    private static bool TryApplyKnockback(HeroHurtbox25D hurtbox, Collider hitCollider, Vector3 direction, Vector3 velocity)
    {
        Component[] components = hurtbox.GetComponentsInParent<Component>(true);
        if (TryInvoke(components, "ApplyGenericKnockback", new[] { typeof(Vector3), typeof(float) }, velocity, 0f))
            return true;
        if (TryInvoke(components, "ApplyKnockbackFromHit", new[] { typeof(Vector3), typeof(float), typeof(float), typeof(float) }, direction, Mathf.Abs(velocity.x), Mathf.Max(0f, velocity.y), 0f))
            return true;
        if (TryInvoke(components, "ApplyGenericKnockback", new[] { typeof(Vector3) }, velocity))
            return true;

        Rigidbody heroRigidbody = hitCollider != null ? hitCollider.attachedRigidbody : null;
        if (heroRigidbody == null)
            heroRigidbody = hurtbox.GetComponentInParent<Rigidbody>();
        if (heroRigidbody != null)
        {
            Vector3 linearVelocity = heroRigidbody.linearVelocity;
            linearVelocity.x = velocity.x;
            linearVelocity.y = Mathf.Max(linearVelocity.y, velocity.y);
            linearVelocity.z = 0f;
            heroRigidbody.linearVelocity = linearVelocity;
            return true;
        }

        return false;
    }

    private static bool TryInvoke(Component[] components, string methodName, Type[] signature, params object[] args)
    {
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, signature, null);
            if (method == null)
                continue;

            object result = method.Invoke(component, args);
            if (method.ReturnType == typeof(bool))
                return result is bool b && b;
            return true;
        }

        return false;
    }

    private void EnableTrigger()
    {
        if (repelTrigger != null)
            repelTrigger.enabled = true;
    }

    private void DisableTrigger()
    {
        if (repelTrigger != null)
            repelTrigger.enabled = false;
    }

    private void AutoAssign()
    {
        if (character == null)
            character = GetComponent<EnemyCharacter25D>();
        if (perception == null)
            perception = GetComponent<EnemyPerception25D>();
        if (repelOrigin == null)
            repelOrigin = transform;
        if (repelTrigger == null)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger)
                {
                    repelTrigger = colliders[i];
                    break;
                }
            }
        }
    }

    private void ClampSettings()
    {
        repelTriggerRange = Mathf.Max(0f, repelTriggerRange);
        repelWindupDuration = Mathf.Max(0f, repelWindupDuration);
        repelActiveDuration = Mathf.Max(0f, repelActiveDuration);
        repelCooldown = Mathf.Max(0f, repelCooldown);
        repelHorizontalForce = Mathf.Max(0f, repelHorizontalForce);
        repelVerticalLift = Mathf.Max(0f, repelVerticalLift);
    }
}
