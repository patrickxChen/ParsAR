using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using GoogleTextToSpeech.Scripts.Data;
using GoogleTextToSpeech.Scripts;


[System.Serializable]
public class UnityAndGeminiKey
{
    public string key;
}

[System.Serializable]
public class Response
{
    public Candidate[] candidates;
}

public class ChatRequest
{
    public Content[] contents;
}

[System.Serializable]
public class Candidate
{
    public Content content;
}

[System.Serializable]
public class Content
{
    public string role; 
    public Part[] parts;
}

[System.Serializable]
public class Part
{
    public string text;
}


public class UnityAndGeminiV3: MonoBehaviour
{
    [Header("Gemini API Password")]
    public string apiKey; 
    private string apiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent"; // Edit it and choose your prefer model


    [Header("NPC Function")]
    [SerializeField] private TextToSpeechManager googleServices;
    [Header("System prompt for the model")]
    [TextArea(2,6)] public string systemPrompt = "You are an NPC in a game. Reply helpfully to the user, do not repeat the user's exact words back, and keep responses concise.";
    private Content[] chatHistory;

    [Header("Lifecycle")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    private static UnityAndGeminiV3 instance;

    [Header("Request Throttling")]
    [SerializeField] private bool dropIfBusy = false;
    [SerializeField] private int maxRetries = 3;
    [SerializeField] private float baseRetryDelaySeconds = 1.0f;
    [SerializeField] private float maxRetryDelaySeconds = 6.0f;
    [SerializeField] private int maxHistoryContents = 6;
    private bool requestInFlight;
    private string pendingMessage;

    private void SanitizeRoles(List<Content> contents)
    {
        if (contents == null) return;
        foreach (var c in contents)
        {
            if (c == null) continue;
            if (string.Equals(c.role, "model", StringComparison.OrdinalIgnoreCase))
            {
                c.role = "model";
            }
            else
            {
                // Force any other role (including null/system) to user
                c.role = "user";
            }
        }
    }

    private void Awake()
    {
        Debug.Log("UnityAndGeminiV3: Awake on " + gameObject.name);

        if (instance != null && instance != this)
        {
            Debug.LogWarning("UnityAndGeminiV3: Duplicate instance detected; destroying this one.");
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (dontDestroyOnLoad)
        {
            var root = transform.root != null ? transform.root.gameObject : gameObject;
            DontDestroyOnLoad(root);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
        Debug.Log("UnityAndGeminiV3: OnDestroy on " + gameObject.name);
    }

    private void OnEnable()
    {
        Debug.Log("UnityAndGeminiV3: OnEnable on " + gameObject.name);
    }

    void Start()
    {
        Debug.Log("UnityAndGeminiV3: Start on " + gameObject.name);
        // Gemini API only accepts roles: user, model
        chatHistory = new Content[] { };
    }

    private bool IsEcho(string reply, string userText)
    {
        if (string.IsNullOrWhiteSpace(reply) || string.IsNullOrWhiteSpace(userText)) return false;
        var r = reply.Trim();
        var u = userText.Trim();

        if (string.Equals(r, u, StringComparison.OrdinalIgnoreCase)) return true;
        if (r.IndexOf(u, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (u.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0) return true;

        // simple word-overlap heuristic
        var uWords = u.Split(new char[] {' ', '\t', '\n', '\r', '.', ',', '!', '?'}, StringSplitOptions.RemoveEmptyEntries);
        var rWords = r.Split(new char[] {' ', '\t', '\n', '\r', '.', ',', '!', '?'}, StringSplitOptions.RemoveEmptyEntries);
        if (uWords.Length == 0) return false;
        int matches = 0;
        foreach (var w in uWords)
        {
            foreach (var rw in rWords)
            {
                if (string.Equals(w, rw, StringComparison.OrdinalIgnoreCase)) { matches++; break; }
            }
        }

        float overlap = (float)matches / (float)uWords.Length;
        return overlap >= 0.6f;
    }

    private IEnumerator SendCuratedRequestToGemini(string userMessage)
    {
        string url = $"{apiEndpoint}?key={apiKey}";

        // Gemini API only accepts user/model roles; prepend instruction to user content
        string strongInstruction = string.IsNullOrWhiteSpace(systemPrompt)
            ? "DO NOT repeat the user's exact words. Summarize, add helpful details, and provide a next step."
            : (systemPrompt + " Extra instruction: DO NOT repeat the user's exact words. Instead, summarize, add helpful details, and provide a next step or suggestion.");

        Content userContent = new Content
        {
            role = "user",
            parts = new Part[] { new Part { text = "Instruction: " + strongInstruction + "\nUser: " + userMessage } }
        };

        List<Content> temp = new List<Content> { userContent };
        SanitizeRoles(temp);
        ChatRequest chatRequest = new ChatRequest { contents = temp.ToArray() };
        string jsonData = JsonUtility.ToJson(chatRequest);
        Debug.Log("Gemini request JSON: " + jsonData);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error);
            }
            else
            {
                Debug.Log("Curated request complete!");
                Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);
                if (response.candidates.Length > 0 && response.candidates[0].content.parts.Length > 0)
                {
                    string reply = response.candidates[0].content.parts[0].text;
                    Debug.Log("Curated reply: " + reply);
                    googleServices.SendTextToGoogle(reply);

                    // add both user and model to persistent chat history
                    List<Content> contentsList = new List<Content>(chatHistory);
                    contentsList.Add(userContent);
                    contentsList.Add(new Content { role = "model", parts = new Part[] { new Part { text = reply } } });
                    chatHistory = contentsList.ToArray();
                }
                else
                {
                    Debug.Log("No text found in curated response.");
                }
            }
        }
    }

    // Functions for sending a new prompt, or a chat to Gemini
    private IEnumerator SendPromptRequestToGemini(string promptText)
    {
        string url = $"{apiEndpoint}?key={apiKey}";
        // Build a chat-style request to avoid accidental raw-echoes. Wrap the prompt as a user message.
        Content userContent = new Content
        {
            role = "user",
            parts = new Part[] { new Part { text = promptText } }
        };

        List<Content> contentsList = new List<Content>(chatHistory);
        contentsList.Add(userContent);
        SanitizeRoles(contentsList);
        ChatRequest chatRequest = new ChatRequest { contents = contentsList.ToArray() };

        string jsonData = JsonUtility.ToJson(chatRequest);
        Debug.Log("Gemini chat JSON: " + jsonData);

        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

        // Create a UnityWebRequest with the JSON data
        using (UnityWebRequest www = new UnityWebRequest(url, "POST")){
            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success) {
                Debug.LogError(www.error);
            } else {
                Debug.Log("Request complete! HTTP code: " + www.responseCode);
                Debug.Log("Raw Gemini response: " + www.downloadHandler.text);
                Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);
                if (response.candidates.Length > 0 && response.candidates[0].content.parts.Length > 0)
                    {
                        //This is the response to your request
                        string text = response.candidates[0].content.parts[0].text;
                        Debug.Log(text);
                    }
                else
                {
                    Debug.Log("No text found.");
                }
            }
        }
    }

    public void SendChat(string userMessage)
    {
        // string userMessage = inputField.text;
        if (requestInFlight)
        {
            if (dropIfBusy)
            {
                Debug.LogWarning("UnityAndGeminiV3: Request in flight; dropping message.");
                return;
            }

            pendingMessage = userMessage;
            Debug.Log("UnityAndGeminiV3: Request in flight; queued latest message.");
            return;
        }

        StartCoroutine(SendChatRequestToGemini(userMessage));
    }

    private IEnumerator SendChatRequestToGemini(string newMessage)
    {
        requestInFlight = true;
        string url = $"{apiEndpoint}?key={apiKey}";
     
        string promptWithInstruction = string.IsNullOrWhiteSpace(systemPrompt)
            ? newMessage
            : ("Instruction: " + systemPrompt + "\nUser: " + newMessage);

        Content userContent = new Content
        {
            role = "user",
            parts = new Part[]
            {
                new Part { text = promptWithInstruction }
            }
        };

        List<Content> contentsList = new List<Content>(chatHistory);
        contentsList.Add(userContent);
        chatHistory = contentsList.ToArray(); 

        SanitizeRoles(contentsList);

        if (maxHistoryContents > 0 && contentsList.Count > maxHistoryContents)
        {
            int start = Mathf.Max(0, contentsList.Count - maxHistoryContents);
            contentsList = contentsList.GetRange(start, contentsList.Count - start);
        }

        ChatRequest chatRequest = new ChatRequest { contents = chatHistory };

        string jsonData = JsonUtility.ToJson(chatRequest);

        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

        // Create a UnityWebRequest with the JSON data
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(jsonToSend);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    bool retryable = www.responseCode == 429 || www.responseCode == 500 || www.responseCode == 503 || www.responseCode == 0;
                    if (retryable && attempt < maxRetries)
                    {
                        float delay = baseRetryDelaySeconds * Mathf.Pow(2f, attempt);
                        delay = Mathf.Min(delay, maxRetryDelaySeconds);

                        var retryAfter = www.GetResponseHeader("Retry-After");
                        if (!string.IsNullOrEmpty(retryAfter) && float.TryParse(retryAfter, out var headerSeconds))
                        {
                            delay = Mathf.Max(delay, headerSeconds);
                        }

                        Debug.LogWarning($"Gemini request failed (HTTP {www.responseCode}). Retrying in {delay:0.0}s...");
                        yield return new WaitForSeconds(delay);
                        continue;
                    }

                    Debug.LogError("Gemini request failed. HTTP code: " + www.responseCode + " Error: " + www.error);
                    Debug.LogError("Gemini error body: " + www.downloadHandler.text);
                }
                else
                {
                    Debug.Log("Curated request complete! HTTP code: " + www.responseCode);
                    Debug.Log("Raw curated response: " + www.downloadHandler.text);
                    Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);
                    if (response.candidates.Length > 0 && response.candidates[0].content.parts.Length > 0)
                    {
                        string reply = response.candidates[0].content.parts[0].text;
                        Content botContent = new Content
                        {
                            role = "model",
                            parts = new Part[]
                            {
                                new Part { text = reply }
                            }
                        };

                        Debug.Log(reply);

                        // If the model simply repeated the user's words, retry with a stronger instruction
                        if (IsEcho(reply, newMessage))
                        {
                            Debug.LogWarning("Detected echo from model. Requesting curated response.");
                            StartCoroutine(SendCuratedRequestToGemini(newMessage));
                        }
                        else
                        {
                            googleServices.SendTextToGoogle(reply);

                            //This part shows the text in the Canvas
                            // uiText.text = reply;
                            //This part adds the response to the chat history, for your next message
                            contentsList.Add(botContent);
                            chatHistory = contentsList.ToArray();
                        }
                    }
                    else
                    {
                        Debug.Log("No text found.");
                    }
                }
            }

            break;
        }

        requestInFlight = false;
        if (!string.IsNullOrWhiteSpace(pendingMessage))
        {
            var queued = pendingMessage;
            pendingMessage = null;
            StartCoroutine(SendChatRequestToGemini(queued));
        }
    }
}




