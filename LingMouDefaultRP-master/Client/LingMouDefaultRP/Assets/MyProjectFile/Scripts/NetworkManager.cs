using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;

[Serializable]
public class RequestData
{
    public float[] robot_pos;
    public string robot_face;
    public float[] crate_pos;
}

[Serializable]
public class ResponseData
{
    public string[] actions;
    public string error;
}

public class NetworkManager : MonoBehaviour
{
    private string serverUrl = "http://127.0.0.1:8765/ask_action";

    public IEnumerator SendRequest(
        Vector3 robotPos,
        Vector3 robotForward,
        Vector3 cratePos,
        Action<string[]> callback,
        Action<string> errorCallback = null)
    {
        RequestData data = new RequestData
        {
            robot_pos = new float[] { Mathf.Round(robotPos.x), Mathf.Round(robotPos.z) },
            crate_pos = new float[] { Mathf.Round(cratePos.x), Mathf.Round(cratePos.z) },
            robot_face = GetDirectionName(robotForward)
        };

        string json = JsonUtility.ToJson(data);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using (UnityWebRequest www = new UnityWebRequest(serverUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 120;

            Debug.Log($"Sending robot request: URL={serverUrl}, Body={json}");
            yield return www.SendWebRequest();
            stopwatch.Stop();

            string responseText = www.downloadHandler != null ? www.downloadHandler.text : string.Empty;
            if (www.result != UnityWebRequest.Result.Success)
            {
                string message = $"Network error: {www.error}, HTTP={www.responseCode}, elapsed={stopwatch.Elapsed.TotalSeconds:F2}s, response={responseText}";
                Debug.LogError(message);
                errorCallback?.Invoke(message);
                yield break;
            }

            Debug.Log($"Model response: {responseText}, elapsed={stopwatch.Elapsed.TotalSeconds:F2}s");
            ResponseData response = JsonUtility.FromJson<ResponseData>(responseText);
            if (response == null)
            {
                string message = "Server response could not be parsed.";
                Debug.LogError(message);
                errorCallback?.Invoke(message);
                yield break;
            }

            if (!string.IsNullOrEmpty(response.error))
            {
                string message = "Server returned an error: " + response.error;
                Debug.LogError(message);
                errorCallback?.Invoke(message);
                yield break;
            }

            if (response.actions == null || response.actions.Length == 0)
            {
                string message = "Server returned no robot actions.";
                Debug.LogError(message);
                errorCallback?.Invoke(message);
                yield break;
            }

            callback?.Invoke(response.actions);
        }
    }

    private string GetDirectionName(Vector3 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
        {
            return dir.x > 0 ? "East" : "West";
        }

        return dir.z > 0 ? "North" : "South";
    }
}