using System.Collections;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    private const string ActionMoveForward = "MOVE_FORWARD";
    private const string ActionTurnLeft = "TURN_LEFT";
    private const string ActionTurnRight = "TURN_RIGHT";
    private const string ActionPickUp = "PICK_UP";

    private const string StateIdle = "Idle";
    private const string StateWalking = "Walking";
    private const string StateLeftTurn = "Left Turn";
    private const string StateRightTurn = "Right Turn";
    private const string StatePickUp = "get";
    private const string StateCarrying = "Carrying";

    public GameObject youtong;
    public float moveStep = 1.0f;
    public float moveDuration = 0.75f;
    public float turnDuration = 0.80f;
    public float animationBlendTime = 0.14f;
    public float actionSettleTime = 0.02f;

    [Header("Pickup Timing")]
    public float pickUpAttachDelay = 2.15f;
    public float pickUpFinishDelay = 1.45f;
    public float pickUpAnimationSpeed = 1.35f;
    public bool returnToIdleAfterPickup = false;

    [Header("Carry Pose")]
    public float carryForwardOffset = 0.82f;
    public float carryHeight = 1.02f;
    public float carryUpOffset = 0.0f;
    public float carrySideOffset = 0.0f;
    public Vector3 carryRotationOffset = Vector3.zero;
    public float carryPositionSmoothTime = 0.55f;
    public float carryRotationLerpSpeed = 12.0f;

    public Transform lefthandTransform;
    public Transform righthandTransform;

    public bool IsBusy => isBusy;

    private bool isBusy;
    private Animator animator;
    private GameObject currentTarget;
    private GameObject carriedCrate;
    private Vector3 carriedCrateVelocity;

    private void Start()
    {
        animator = GetComponent<Animator>();
        ResetAnimatorFlags();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            string[] testActions = { ActionMoveForward, ActionMoveForward, ActionMoveForward, ActionMoveForward, ActionTurnRight, ActionMoveForward, ActionPickUp };
            ExecuteActions(testActions, youtong);
        }
    }

    private void LateUpdate()
    {
        UpdateCarriedCratePose();
    }

    // Called by an animation event on the pickup clip. The coroutine also has a fallback attach.
    public void MyEventFunction()
    {
        AttachCrateToCarryPose(currentTarget);
    }

    public bool ExecuteActions(string[] actions, GameObject targetCrate)
    {
        if (isBusy)
        {
            Debug.LogWarning("Robot is already executing actions; ignoring the new action list.");
            return false;
        }

        if (actions == null || actions.Length == 0)
        {
            Debug.LogWarning("Robot received an empty action list.");
            return false;
        }

        if (targetCrate == null)
        {
            Debug.LogWarning("Robot received actions without a target crate.");
            return false;
        }

        currentTarget = targetCrate;
        StartCoroutine(ProcessActionQueue(actions, targetCrate));
        return true;
    }

    private IEnumerator ProcessActionQueue(string[] actions, GameObject targetCrate)
    {
        isBusy = true;
        ResetAnimatorFlags();

        for (int index = 0; index < actions.Length; index++)
        {
            string action = NormalizeAction(actions[index]);
            if (string.IsNullOrEmpty(action))
            {
                continue;
            }

            if (action == ActionMoveForward)
            {
                int moveCount = CountConsecutiveMoves(actions, index);
                string nextAction = GetActionAt(actions, index + moveCount);
                Debug.Log($"Executing {moveCount} continuous forward step(s).");
                yield return StartCoroutine(MoveForward(moveCount, nextAction));
                index += moveCount - 1;
                continue;
            }

            string followingAction = GetActionAt(actions, index + 1);
            Debug.Log("Executing action: " + action);

            switch (action)
            {
                case ActionTurnLeft:
                    yield return StartCoroutine(Turn(-90.0f, "left", StateLeftTurn, followingAction));
                    break;
                case ActionTurnRight:
                    yield return StartCoroutine(Turn(90.0f, "right", StateRightTurn, followingAction));
                    break;
                case ActionPickUp:
                    yield return StartCoroutine(PickUp(targetCrate));
                    break;
                default:
                    Debug.LogWarning("Unknown robot action skipped: " + action);
                    break;
            }
        }

        ResetDirectionalFlags();
        SetAnimatorBool("get", false);

        if (carriedCrate != null && !returnToIdleAfterPickup)
        {
            EnterCarryPose();
        }
        else
        {
            SetAnimatorBool("carry", false);
            CrossFadeIfPossible(StateIdle);
        }

        isBusy = false;
    }

    private IEnumerator MoveForward(int stepCount, string nextAction)
    {
        if (stepCount <= 0)
        {
            yield break;
        }

        SetAnimatorBool("w", true);
        CrossFadeIfPossible(StateWalking);

        for (int step = 0; step < stepCount; step++)
        {
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + transform.forward * moveStep;
            yield return StartCoroutine(InterpolatePosition(startPos, targetPos, moveDuration));
            transform.position = targetPos;
        }

        SetAnimatorBool("w", false);

        if (nextAction == ActionTurnLeft || nextAction == ActionTurnRight)
        {
            yield return new WaitForSeconds(actionSettleTime);
        }
        else if (nextAction == ActionPickUp)
        {
            CrossFadeIfPossible(StateIdle);
            yield return new WaitForSeconds(actionSettleTime);
        }
        else
        {
            CrossFadeIfPossible(StateIdle);
        }
    }

    private IEnumerator Turn(float yawDegrees, string boolParameter, string stateName, string nextAction)
    {
        ResetDirectionalFlags();
        SetAnimatorBool(boolParameter, true);
        CrossFadeIfPossible(stateName);

        Quaternion startRot = transform.rotation;
        Quaternion targetRot = startRot * Quaternion.Euler(0.0f, yawDegrees, 0.0f);
        yield return StartCoroutine(InterpolateRotation(startRot, targetRot, turnDuration));
        transform.rotation = targetRot;

        SetAnimatorBool(boolParameter, false);

        if (nextAction == ActionMoveForward)
        {
            CrossFadeIfPossible(StateWalking);
        }
        else if (nextAction != ActionTurnLeft && nextAction != ActionTurnRight && nextAction != ActionPickUp)
        {
            CrossFadeIfPossible(StateIdle);
        }

        yield return new WaitForSeconds(actionSettleTime);
    }

    private IEnumerator PickUp(GameObject crate)
    {
        ResetDirectionalFlags();
        SetAnimatorBool("w", false);
        SetAnimatorBool("carry", false);
        SetAnimatorBool("get", true);
        CrossFadeIfPossible(StatePickUp);

        float previousAnimatorSpeed = animator != null ? animator.speed : 1.0f;
        if (animator != null)
        {
            animator.speed = pickUpAnimationSpeed;
        }

        yield return new WaitForSeconds(pickUpAttachDelay);
        AttachCrateToCarryPose(crate);

        yield return new WaitForSeconds(pickUpFinishDelay);
        SetAnimatorBool("get", false);

        if (animator != null)
        {
            animator.speed = previousAnimatorSpeed;
        }

        if (returnToIdleAfterPickup)
        {
            SetAnimatorBool("carry", false);
            CrossFadeIfPossible(StateIdle);
        }
        else
        {
            EnterCarryPose();
        }
    }

    private IEnumerator InterpolatePosition(Vector3 startPos, Vector3 targetPos, float duration)
    {
        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0.0f, 1.0f, t);
            transform.position = Vector3.LerpUnclamped(startPos, targetPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator InterpolateRotation(Quaternion startRot, Quaternion targetRot, float duration)
    {
        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0.0f, 1.0f, t);
            transform.rotation = Quaternion.SlerpUnclamped(startRot, targetRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void AttachCrateToCarryPose(GameObject crate)
    {
        if (crate == null)
        {
            return;
        }

        Rigidbody crateBody = crate.GetComponent<Rigidbody>();
        if (crateBody != null)
        {
            crateBody.isKinematic = true;
            crateBody.velocity = Vector3.zero;
            crateBody.angularVelocity = Vector3.zero;
        }

        crate.transform.SetParent(null, true);
        carriedCrate = crate;
        carriedCrateVelocity = Vector3.zero;
    }

    private void EnterCarryPose()
    {
        SetAnimatorBool("carry", true);
        CrossFadeIfPossible(StateCarrying, animationBlendTime * 1.5f);
    }

    private void UpdateCarriedCratePose(bool snap = false)
    {
        if (carriedCrate == null)
        {
            return;
        }

        Vector3 targetPosition = GetCarryAnchorPosition();
        Quaternion targetRotation = GetCarryAnchorRotation();

        if (snap || !Application.isPlaying)
        {
            carriedCrate.transform.position = targetPosition;
            carriedCrate.transform.rotation = targetRotation;
            return;
        }

        float smoothTime = Mathf.Max(0.01f, carryPositionSmoothTime);
        carriedCrate.transform.position = Vector3.SmoothDamp(
            carriedCrate.transform.position,
            targetPosition,
            ref carriedCrateVelocity,
            smoothTime);

        float rotationStep = Mathf.Clamp01(Time.deltaTime * carryRotationLerpSpeed);
        carriedCrate.transform.rotation = Quaternion.Slerp(carriedCrate.transform.rotation, targetRotation, rotationStep);
    }

    private Vector3 GetCarryAnchorPosition()
    {
        return transform.position
            + transform.forward * carryForwardOffset
            + transform.right * carrySideOffset
            + Vector3.up * (carryHeight + carryUpOffset);
    }

    private Quaternion GetCarryAnchorRotation()
    {
        return Quaternion.LookRotation(transform.forward, Vector3.up) * Quaternion.Euler(carryRotationOffset);
    }

    private int CountConsecutiveMoves(string[] actions, int startIndex)
    {
        int count = 0;
        for (int i = startIndex; i < actions.Length; i++)
        {
            if (NormalizeAction(actions[i]) != ActionMoveForward)
            {
                break;
            }
            count++;
        }
        return count;
    }

    private string GetActionAt(string[] actions, int index)
    {
        if (actions == null || index < 0 || index >= actions.Length)
        {
            return null;
        }
        return NormalizeAction(actions[index]);
    }

    private string NormalizeAction(string action)
    {
        return string.IsNullOrWhiteSpace(action) ? null : action.Trim().ToUpperInvariant();
    }

    private void ResetAnimatorFlags()
    {
        ResetDirectionalFlags();
        SetAnimatorBool("get", false);
        SetAnimatorBool("carry", carriedCrate != null && !returnToIdleAfterPickup);
    }

    private void ResetDirectionalFlags()
    {
        SetAnimatorBool("w", false);
        SetAnimatorBool("left", false);
        SetAnimatorBool("right", false);
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (animator == null)
        {
            return;
        }

        if (HasAnimatorParameter(parameterName))
        {
            animator.SetBool(parameterName, value);
        }
    }

    private bool HasAnimatorParameter(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private void CrossFadeIfPossible(string stateName)
    {
        CrossFadeIfPossible(stateName, animationBlendTime);
    }

    private void CrossFadeIfPossible(string stateName, float blendTime)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (animator.HasState(0, stateHash))
        {
            animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0.0f, blendTime));
        }
    }
}

