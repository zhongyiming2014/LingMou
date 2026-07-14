using UnityEngine;
using UnityEngine.UI;

public class GazeManager : MonoBehaviour
{
    public float activationTime = 3.0f;
    public Image crosshair;
    public NetworkManager networkManager;
    public RobotController robotController;

    public Transform cameraPivot;
    public float rotateSpeed = 100f;

    private float timer;
    private GameObject currentGazedObject;
    private GameObject lastGazedObject;
    private bool isLocked;
    private Camera gazeCamera;

    private void Start()
    {
        gazeCamera = GetComponent<Camera>();
    }

    private void Update()
    {
        HandleCameraRotation();

        if (isLocked)
        {
            return;
        }

        if (gazeCamera == null)
        {
            gazeCamera = GetComponent<Camera>();
            if (gazeCamera == null)
            {
                return;
            }
        }

        Ray ray = gazeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0.0f));
        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            ResetGaze();
            return;
        }

        if (!hit.collider.CompareTag("Crate"))
        {
            ResetGaze();
            return;
        }

        GameObject hitObj = hit.collider.gameObject;
        if (currentGazedObject != hitObj)
        {
            ResetGaze();
            currentGazedObject = hitObj;
        }

        timer += Time.deltaTime;
        if (crosshair != null)
        {
            crosshair.color = Color.Lerp(Color.white, Color.red, Mathf.Clamp01(timer / activationTime));
        }

        if (timer >= activationTime)
        {
            TriggerAction(hitObj);
        }
    }

    private void HandleCameraRotation()
    {
        if (cameraPivot == null || !Input.GetMouseButton(1))
        {
            return;
        }

        float h = Input.GetAxis("Mouse X");
        float v = Input.GetAxis("Mouse Y");
        cameraPivot.RotateAround(cameraPivot.position, Vector3.up, h * rotateSpeed * Time.deltaTime);
        transform.RotateAround(cameraPivot.position, transform.right, -v * rotateSpeed * Time.deltaTime);
    }

    private void ResetGaze()
    {
        timer = 0.0f;
        if (crosshair != null)
        {
            crosshair.color = Color.white;
        }

        ClearSelection(lastGazedObject);
        lastGazedObject = null;
        currentGazedObject = null;
    }

    private void TriggerAction(GameObject targetCrate)
    {
        if (networkManager == null || robotController == null || targetCrate == null)
        {
            Debug.LogError("Gaze action cannot start because required references are missing.");
            UnlockAfterFailure(targetCrate);
            return;
        }

        Debug.Log("Gaze target locked. Sending action request...");
        isLocked = true;
        lastGazedObject = targetCrate;
        MarkSelected(targetCrate);

        StartCoroutine(networkManager.SendRequest(
            robotController.transform.position,
            robotController.transform.forward,
            targetCrate.transform.position,
            actions =>
            {
                bool accepted = robotController.ExecuteActions(actions, targetCrate);
                if (!accepted)
                {
                    UnlockAfterFailure(targetCrate);
                }
            },
            error =>
            {
                Debug.LogError("Action request failed: " + error);
                UnlockAfterFailure(targetCrate);
            }));
    }

    private void MarkSelected(GameObject target)
    {
        Renderer rend = target != null ? target.GetComponent<Renderer>() : null;
        if (rend == null)
        {
            return;
        }

        rend.material.SetFloat("_IsSelected", 1.0f);
        rend.material.SetColor("_RimColor", Color.red);
    }

    private void ClearSelection(GameObject target)
    {
        Renderer rend = target != null ? target.GetComponent<Renderer>() : null;
        if (rend != null)
        {
            rend.material.SetFloat("_IsSelected", 0.0f);
        }
    }

    private void UnlockAfterFailure(GameObject target)
    {
        ClearSelection(target);
        isLocked = false;
        timer = 0.0f;
        currentGazedObject = null;
        lastGazedObject = null;
        if (crosshair != null)
        {
            crosshair.color = Color.white;
        }
    }
}